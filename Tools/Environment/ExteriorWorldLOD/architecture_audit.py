from pathlib import Path
import json,math,hashlib,numpy as np
ROOT=Path(__file__).resolve().parents[3];ART=ROOT/'Assets/Art/Environment/Opening/ArchitectureLOD';LOG=ROOT/'Logs/exterior-world-lod-20261109/architecture'
report={'mesh_checks':{},'doorway_checks':[],'lod_checks':{}}
report['A02_roof_visibility_checks']=[]
def hit_ray(vertices,triangles,origin):
 a=vertices[triangles[:,0]];b=vertices[triangles[:,1]];c=vertices[triangles[:,2]];d=np.array((0.,0.,-1.));e1=b-a;e2=c-a;p=np.cross(d,e2);det=np.einsum('ij,ij->i',e1,p);ok=np.abs(det)>1e-8;inv=np.zeros_like(det);inv[ok]=1/det[ok];s=np.array(origin)-a;u=np.einsum('ij,ij->i',s,p)*inv;q=np.cross(s,e1);v=q@d*inv;t=np.einsum('ij,ij->i',e2,q)*inv
 return bool(np.any(ok&(u>=0)&(v>=0)&(u+v<=1)&(t>1e-5)&(t<1.8)))
def front_facing_vertical_hit(vertices,triangles,origin,sign):
 a=vertices[triangles[:,0]];b=vertices[triangles[:,1]];c=vertices[triangles[:,2]];direction=np.array((0.,float(sign),0.));e1=b-a;e2=c-a;n=np.cross(e1,e2)
 p=np.cross(direction,e2);det=np.einsum('ij,ij->i',e1,p);ok=(np.abs(det)>1e-8)&((n@direction)<-1e-8);inv=np.zeros_like(det);inv[ok]=1/det[ok];s=np.array(origin)-a;u=np.einsum('ij,ij->i',s,p)*inv;q=np.cross(s,e1);v=q@direction*inv;t=np.einsum('ij,ij->i',e2,q)*inv
 return bool(np.any(ok&(u>=0)&(v>=0)&(u+v<=1)&(t>1e-5)&(t<5)))
for file in sorted(ART.glob('*.json')):
 d=json.loads(file.read_text(encoding='utf8'));v=np.array(d['vertices']).reshape(-1,3);uv=np.array(d['uv']).reshape(-1,2);tri=np.array(d['triangles']).reshape(-1,3)
 assert len(v)==len(uv) and len(v)<65535 and tri.max()<len(v) and tri.min()>=0
 area=np.linalg.norm(np.cross(v[tri[:,1]]-v[tri[:,0]],v[tri[:,2]]-v[tri[:,0]]),axis=1)/2
 assert np.all(area>1e-10),file
 assert np.isfinite(v).all() and uv.min()>=0 and uv.max()<=1
 report['mesh_checks'][file.stem]={'triangles':len(tri),'vertices':len(v),'uv_bounds':[float(uv.min()),float(uv.max())],'degenerate':int(np.sum(area<1e-10)),'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}
 if file.stem.startswith('CalibrationHall'):
  variant=int(file.stem.split('_')[1]);width=(6.,6.8,7.3)[variant];upper=lower=old_upper=0
  old=json.loads((LOG/'soffit-before'/file.name).read_text(encoding='utf8'));ov=np.array(old['vertices']).reshape(-1,3);ot=np.array(old['triangles']).reshape(-1,3)
  # Performance revision also removes tile ribs; confirm the actual downward slope
  # boards instead of confusing net triangle delta with the 16-triangle closed shell.
  eave=(2.85,2.7,2.95)[variant];rise=(1.,.9,1.02)[variant];points=v[tri];normals=np.cross(points[:,1]-points[:,0],points[:,2]-points[:,0])
  on_board=np.max(np.abs(points[:,:,1]-(eave+rise*(1-np.abs(points[:,:,2])/2.58)-.075)),axis=1)<1e-6
  downward_board_faces=int(np.sum(on_board&(normals[:,1]<-1e-6)));assert downward_board_faces==4,(file,'Missing two downward slope quads',downward_board_faces)
  for x in (-.31*width,.13*width,.36*width):
   for z in (-1.23,.47,1.38):
    up=front_facing_vertical_hit(v,tri,(x,2.2,z),1);down=front_facing_vertical_hit(v,tri,(x,6,z),-1)
    assert up and down,(file,x,z,'Roof lacks front-facing underside or top')
    upper+=up;lower+=down;old_upper+=front_facing_vertical_hit(ov,ot,(x,2.2,z),1)
  report['A02_roof_visibility_checks'].append({'mesh':file.stem,'front_facing_wood_underside_hits':int(upper),'front_facing_top_hits':int(lower),'before_underside_hits':int(old_upper),'ray_grid':9,'soffit_shell_triangles':16,'downward_board_triangles':downward_board_faces,'net_triangle_delta_since_before_soffit':len(tri)-len(ot),'thickness_m':.075,'method':'Triangle frontface test with normal dot ray < 0. Does not count a backface as a blocker.'})
  old_bench_region=(np.abs(v[:,0])<1.4)&(v[:,1]>.1)&(v[:,1]<1.2)&(v[:,2]>-.2)&(v[:,2]<1.2)
  assert not np.any(old_bench_region),(file,'Former combined bench geometry still present')
  for x in (-.95,0,.95):
   for y in (.35,1.3,2.3):
    assert not hit_ray(v,tri,(x,y,3.1)),(file,x,y,'Doorway occluded before foyer')
  report['doorway_checks'].append({'mesh':file.stem,'clear_rays':9,'clear_width_tested':1.9,'opening_designed_width':2.1,'front_to_foyer_z':[3.1,1.3]})
for prefix in ['CalibrationHall_'+str(i) for i in range(3)]+['Roof_Wake','Roof_Corridor','Roof_Trial']:
 counts=[report['mesh_checks'][prefix+'_LOD'+str(i)]['triangles'] for i in range(3)];assert counts[0]>counts[1]>counts[2];report['lod_checks'][prefix]=counts
report['near_sum_source']=sum(r['triangles'] for name,r in report['mesh_checks'].items() if name.endswith('LOD0'))
report['near_sum_with_support_expected']=report['near_sum_source']+3*4*(28+28+28+12)+3*4*28
report['independent_A07_near_triangles']=3*3400
report['near_sum_including_A07']=report['near_sum_with_support_expected']+report['independent_A07_near_triangles']
assert report['near_sum_including_A07']<18000
report['bench_selection']={'old_combined_table_geometry_absent':True,'world_dimensions_approx':[2,.9,.85],'local_position':[0,0,.5],'local_scale_compensation':{'1.25':.8,'1.35':1/1.35,'1.6':.625},'independent_LOD_thresholds':[.22,.08,.005],'fixed_colliders_per_table':5,'tables':3,'collider_method':'Separate sibling under compensated bench root: tabletop box 1.97 x .14 x .82 centered y.825; four legs .14 x .78 x .14 centered x±.82,z±.30,y.39; no visual LOD dependency'}
report['main_roof_performance_changes']={}
for file in sorted(ART.glob('Roof_*.json')):
 old=json.loads((LOG/'performance-before'/file.name).read_text(encoding='utf8'));new=json.loads(file.read_text(encoding='utf8'));before_tri=len(old['triangles'])//3;after_tri=len(new['triangles'])//3;assert after_tri<before_tri
 report['main_roof_performance_changes'][file.name]={'before':before_tri,'after':after_tri,'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}
report['accepted_A07_atlas_unchanged']=True
for entry in json.loads((LOG/'performance-before/a07-atlas-hashes.json').read_text(encoding='utf-8-sig')):
 assert hashlib.sha256(Path(entry['path']).read_bytes()).hexdigest().lower()==entry['sha256'].lower()
for entry in json.loads((LOG/'bench-selection-before/atlas-hashes.json').read_text(encoding='utf-8-sig')):
 assert hashlib.sha256(Path(entry['Path']).read_bytes()).hexdigest().lower()==entry['Hash'].lower()
assert report['near_sum_with_support_expected']<18000
report['passed']=True;report['scope']='Numerical source-mesh/doorway/LOD audit. Actual Scene bounds, terrain samples, Unity import and Quest performance remain parent verification.'
(LOG/'mesh-audit.json').write_text(json.dumps(report,indent=2),encoding='utf8')
files=[]
for folder in (ART,ROOT/'ArtSource/Environment/Opening/ArchitectureLOD',ROOT/'Tools/Environment/ExteriorWorldLOD'):
 for p in sorted(folder.rglob('*')):
  if p.is_file() and (folder!=ROOT/'Tools/Environment/ExteriorWorldLOD' or p.name.startswith(('Architecture','architecture_'))):files.append({'path':str(p),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(LOG/'delivery-manifest.json').write_text(json.dumps(files,indent=2),encoding='utf8')
print(json.dumps({'passed':True,'meshes':len(report['mesh_checks']),'doorway_rays':len(report['doorway_checks'])*9,'near_triangles_with_A07':report['near_sum_including_A07'],'files':len(files)}))

"""Final static Unity binding audit and FBX clearance check. Run headless from any cwd."""
from pathlib import Path
from datetime import datetime
import hashlib
import json
import re
import sys
import struct
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import roof
sys.path.insert(0,str(Path.home()/'.codex/skills/blender-asset-qa/scripts'))
import qa_check

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def guid(path):
    return re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]

def main():
    report={'time':datetime.now().astimezone().isoformat(timespec='seconds'),
            'blender':bpy.app.version_string,'reference':'docs/visuals/chapter01_preqin_architecture/山间校院_A1.png',
            'reference_sha256':sha(roof.ROOT/'docs/visuals/chapter01_preqin_architecture/山间校院_A1.png'),
            'review_scope':'Blender FBX re-import, Cycles renders, static Unity YAML and importer checks. Parent owns Unity runtime.',
            'source_materials':['Assets/Art/Environment/Opening/PhotoArchitecture/Timber_Color.jpg',
                'Assets/Art/Environment/Opening/PhotoArchitecture/Timber_Normal.png',
                'ArtSource/Environment/SuspendedPath/Surfaces/Workshop/sources.json'],
            'uv_note':'Surface charts intentionally reuse the trim sheet. Lightmap is an independent second unwrap.',
            'models':{},'scene_links':{}}
    for model in roof.MODELS:
        dest=roof.DEST/model
        fbx=dest/(model+'.fbx')
        prefab=dest/(model+'.prefab')
        p=prefab.read_text()
        base=json.loads((HERE/'Review'/(model+'_identity.json')).read_text())
        assert sha(prefab)==base['prefab_sha256'] and sha(Path(str(fbx)+'.meta'))==base['mesh_meta_sha256']
        assert [list(row) for row in roof.fbx_ids(fbx)]==base['fbx']
        record={'fbx_guid':guid(fbx),'prefab_guid':guid(prefab),'fbx_sha256':sha(fbx),'baseline_identity_match':True,
                'prefab_unchanged':True,'lod_relative_heights':re.findall(r'screenRelativeHeight: (.+)',p),
                'box_colliders':p.count('\nBoxCollider:'),'tiers':{}}
        report['models'][model]=record
        expected_colliders=9 if model=='CourtGatehouse' else 0
        assert record['box_colliders']==expected_colliders and len(record['lod_relative_heights'])==3
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(fbx))
        objects=sorted(bpy.context.scene.objects,key=lambda item:item.name)
        assert len(objects)==3
        qa=json.loads((HERE/'Review'/(model+'_FBX_QA.json')).read_text())
        surface=json.loads((HERE/'Review'/(model+'_surface_after.json')).read_text())
        record['surface_audit']=model+'_surface_after.json'
        for i,o in enumerate(objects):
            mesh=o.data
            assert len(mesh.materials)==1 and mesh.materials[0].name==o.name+'_Baked'
            mat=dest/(o.name+'.mat')
            material=mat.read_text()
            assert guid(mat) in p
            gameobject=re.search(r'--- !u!1 &(\d+)\s+GameObject:\n(?:(?!--- !u!).)*?m_Name: '+o.name+r'\s',p,re.S)[1]
            components=[chunk for chunk in p.split('--- !u!') if 'm_GameObject: {fileID: '+gameobject+'}' in chunk]
            mf=next(c for c in components if c.startswith('33 '))
            mr=next(c for c in components if c.startswith('23 '))
            assert guid(fbx) in mf and guid(mat) in mr
            mesh_id=re.search(r'm_Mesh: {fileID: (-?\d+)',mf)[1]
            size=2048>>i
            for prop,kind in [('_BaseMap','BaseColor'),('_BumpMap','Normal'),('_MetallicGlossMap','Mask')]:
                path=dest/(o.name+'_'+kind+'.png')
                binding=re.search(r'- '+prop+r':\n\s+m_Texture: ([^\n]+)',material)
                assert binding and guid(path) in binding[1],(mat,prop)
                assert struct.unpack('>II',path.read_bytes()[16:24])==(size,size)
                meta=Path(str(path)+'.meta').read_text()
                assert ('sRGBTexture: '+('1' if kind=='BaseColor' else '0')) in meta
                if kind=='Normal':assert 'textureType: 1\n' in meta
            result=qa[o.name]
            assert result['pass'],(o.name,result['errors'])
            vertices=np.array([tuple(o.matrix_world@v.co) for v in mesh.vertices])
            tri=np.array([tuple(poly.vertices) for poly in mesh.polygons])
            assert len(tri)==result['objects'][o.name]['tris']
            assert list(mesh.uv_layers.keys())==['Surface','Lightmap']
            # Use the named second channel for a coarse coverage and overlap probe.
            bm=bmesh.new()
            bm.from_mesh(mesh)
            bm.loops.layers.uv.remove(bm.loops.layers.uv['Surface'])
            lightmap=qa_check._uv_stats(o,bm)
            bm.free()
            item={'triangles':len(tri),'prefab_mesh_fileID':mesh_id,'material_guid':guid(mat),
                  'size_px':size,'bounds_blender_m':[vertices.min(0).round(5).tolist(),vertices.max(0).round(5).tolist()],
                  'lightmap_uv':lightmap,'game_qa':result['pass'],'warnings':result['warnings']}
            for k in ('smoothness_multiplier','roof_mask_A_p05_p50_p95',
                      'roof_effective_smoothness_p05_p50_p95','non_end_triangles_on_detailed_rafter_end_panels'):
                item[k]=surface[o.name][k]
            x,y,eave,crown,ridge=roof.MODELS[model]
            tree=BVHTree.FromPolygons(vertices.tolist(),tri.tolist(),all_triangles=True)
            misses=[]
            for xx in np.linspace(-x+.085,x-.085,83):
                for yy in np.linspace(-y+.085,y-.085,67):
                    t=1-abs(yy)/y
                    if ridge<x:t=min(t,(x-abs(xx))/(x-ridge))
                    nominal=eave+(crown-eave)*(.92*t+.08*t*t)
                    hit=tree.ray_cast((xx,yy,crown+1),(0,0,-1))[0]
                    if hit is None or hit.z<nominal-.14:misses.append((xx,yy))
            item['roof_surface_probe']={'vertical_rays':83*67,'missed_upper_surface':len(misses)}
            assert not misses,(o.name,misses[:4])
            if model=='CourtGatehouse':
                # Conservative triangle-AABB broad phase. An empty set proves the passage is clear.
                points=vertices[tri]
                inside=((points.max(1)>=[-1.2,-3.0,.01]) & (points.min(1)<=[1.2,3.0,2.66])).all(1)
                item['triangles_intersecting_2_4m_by_2_66m_passage_broad_phase']=int(inside.sum())
                assert not inside.any()
            record['tiers'][o.name]=item
        counts=[v['triangles'] for v in record['tiers'].values()]
        assert counts[0]>counts[1]>counts[2]
    for scene in ('Chapter01_Awakening','Chapter01_Courtyard'):
        text=(roof.ROOT/'Assets/Scene'/(scene+'.unity')).read_text(encoding='utf-8-sig')
        static={}
        for path in (roof.DEST/'CourtHallRoofSet.prefab',roof.DEST/'CourtGatehouse/CourtGatehouse.prefab'):
            blocks=[c for c in text.split('--- !u!') if c.startswith('1001 ') and 'm_SourcePrefab: {fileID: 100100000, guid: '+guid(path) in c]
            static[path.stem]={'instances':len(blocks),'material_or_mesh_overrides':
                [s for c in blocks for s in re.findall(r'propertyPath: ([^\n]+)',c) if 'material' in s.lower() or 'mesh' in s.lower()]}
        report['scene_links'][scene]=static
    report['historical_tier0_centres_unity_xyz_m']={'CourtMainRoof':[[0,6.70,26]],
            'CourtLowWing':[[0,5.53,3.55],[0,4.22,14]],'CourtGatehouse':[[17.64,2.80,49.50]]}
    report['smoothness_probe']='150000 area-weighted barycentric samples per LOD, bilinear mip 0 of the bound linear metallic/gloss PNG. Effective smoothness = mask alpha × URP Lit _Smoothness, texture channel 0. Platform compression and scene lighting require Unity review.'
    report['rebuild']=['python -B surfaces.py','blender -b --factory-startup --python-exit-code 1 -P roof.py -- MODEL',
        'blender -b --factory-startup --python-exit-code 1 -P surface_check.py -- MODEL after',
        'blender -b --factory-startup --python-exit-code 1 -P review.py -- MODEL qa',
        'blender -b --factory-startup --python-exit-code 1 -P review.py -- MODEL shots',
        'blender -b --factory-startup --python-exit-code 1 -P audit.py']
    (HERE/'Review'/'A1_delivery.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf8')
    print('COURT_A1_DELIVERY_PASS')

if __name__=='__main__':main()


"""Read the delivered FBX, check protected volumes and render the assigned surfaces."""
import sys
import json
import hashlib
import re
from statistics import median
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
sys.dont_write_bytecode=True
sys.path.insert(0,str(HERE))
from build_pavilion import ASSET, PHOTO, SURFACES, fresh_material

REVIEW=HERE/'Review'
R2=REVIEW/'R2'


def write(name,data):
    (REVIEW/name).write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf8')


def overlaps(tree,low,high):
    corners=[(x,y,z) for z in (low[2],high[2]) for x,y in
             [(low[0],low[1]),(high[0],low[1]),(high[0],high[1]),(low[0],high[1])]]
    box=BVHTree.FromPolygons(corners,[(0,1,2,3),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    return len(tree.overlap(box))


def asset_guid(path):
    return re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]


def unity_surfaces(before):
    prefab=(ASSET/'TianyiPavilion.prefab').read_text()
    importer=(ASSET/'TianyiPavilion.fbx.meta').read_text()
    mats=[PHOTO/'Flags.mat',ASSET/'EarthPlinth.mat',ASSET/'Timber.mat',ASSET/'Tile.mat']
    expected=[asset_guid(mat) for mat in mats]
    slots=re.findall(r'  m_Materials:\n((?:  - \{fileID: 2100000, guid: [a-f0-9]+, type: 2\}\n)+)',prefab)
    assert len(slots)==3
    assert all(re.findall(r'guid: (\w+)',row)==expected for row in slots)
    bindings={}
    for path,(slot,base,normal,bump,smooth) in zip(mats,SURFACES):
        data=path.read_text()
        assert re.search(r'name: '+slot+r'\n    second: \{fileID: 2100000, guid: '+asset_guid(path),importer)
        assert '  - _NORMALMAP' in data
        for prop,tex in [('_BaseMap',base),('_BumpMap',normal)]:
            assert re.search(prop+r':\n\s+m_Texture: \{fileID: 2800000, guid: '+asset_guid(tex),data)
        assert float(re.search(r'_BumpScale: ([\d.]+)',data)[1])==bump
        assert float(re.search(r'_Smoothness: ([\d.]+)',data)[1])==smooth
        meta=Path(str(normal)+'.meta').read_text()
        assert 'textureType: 1\n' in meta and 'sRGBTexture: 0\n' in meta
        bindings[slot]={'material':path.name,'guid':asset_guid(path),'albedo':base.name,'normal':normal.name,
                        'bump':bump,'smoothness':smooth}
    for name in ['TianyiPavilion.prefab','TianyiPavilion.fbx.meta']:
        assert hashlib.sha256((ASSET/name).read_bytes()).hexdigest()==before[name]
    return bindings


def roof_measurements(mesh,verts):
    surfaces=[BVHTree.FromPolygons(verts,[p.vertices[:] for p in mesh.polygons if p.material_index==slot]) for slot in (2,3)]
    wood,tile=surfaces
    edge=max(abs(v.y) for v in verts)
    def top(tree,x,y):
        point,_,_,_=tree.ray_cast((x,y,8),(0,0,-1))
        return point.z if point is not None else None
    def bed(t,r):
        return median(top(tile,t+i*.025,-r) for i in range(-4,5))
    front=edge-.145
    centre=bed(0.,front)
    ends=[bed(sign*(front-.21),front) for sign in (-1,1)]
    # Probe the full hip and the exposed timber/roof interface in all four
    # corners. Use both front and side projections over a 1.35 m square.
    checks={}
    for sx,sy in [(-1,-1),(1,-1),(-1,1),(1,1)]:
        gaps=[]
        for i in range(42):
            for j in range(42):
                x=sx*(edge-.14-i*.032);y=sy*(edge-.14-j*.032)
                roof=top(tile,x,y);timber=top(wood,x,y)
                assert roof is not None,(mesh.name,'missing hip cover',x,y)
                if timber is not None:gaps.append(roof-timber)
        checks[str((sx,sy))]={'samples':len(gaps),'minimum_tile_over_timber_m':round(min(gaps),4)}
    return {'width_m':round(edge*2,4),'eave_bed_centre_z_m':round(centre,4),
            'left_right_corner_lift_m':[round(z-centre,4) for z in ends],
            'inner_to_eave_rise_m':round(bed(0,1.78)-centre,4),'four_corner_raycast':checks}


def inspect_joints(mesh,verts):
    # Separate the continuous roof bed and its four hip caps using topology.
    # Opposed raycasts then measure the actual cap/bed overlap, including
    # between modeled stations, independently of the generator's formula.
    parent=list(range(len(verts)))
    def root(i):
        while parent[i]!=i:
            parent[i]=parent[parent[i]];i=parent[i]
        return i
    for edge in mesh.edges:
        a,b=edge.vertices;parent[root(a)]=root(b)
    islands={}
    for poly in mesh.polygons:islands.setdefault(root(poly.vertices[0]),[]).append(poly)
    bed_faces=[];caps=[]
    for polys in islands.values():
        mats={p.material_index for p in polys}
        if mats=={2,3}:bed_faces=[p.vertices[:] for p in polys if p.material_index==3]
        if mats=={3}:caps.append(BVHTree.FromPolygons(verts,[p.vertices[:] for p in polys]))
    assert bed_faces and len(caps)==4
    roof=BVHTree.FromPolygons(verts,bed_faces)
    extent=max(v.x for v in verts)
    overlap={}
    for sx,sy in ((-1,-1),(1,-1),(-1,1),(1,1)):
        samples=[]
        for k in range(130):
            r=1.7+(extent-.22-1.7)*k/129
            x=sx*(r+.003);y=sy*r
            top=roof.ray_cast((x,y,8),(0,0,-1))[0]
            bottoms=[hit.z for cap in caps if (hit:=cap.ray_cast((x,y,2),(0,0,1))[0]) is not None]
            assert top is not None and len(bottoms)==1,(mesh.name,x,y)
            samples.append(top.z-bottoms[0])
        overlap[str((sx,sy))]={'sample_count':len(samples),'min_seating_depth_m':round(min(samples),4)}
    timber_ids={i for p in mesh.polygons if p.material_index==2 for i in p.vertices}
    # Exclude the fascia, which shares the shell's boundary vertices.
    shell_ids={i for face in bed_faces for i in face}
    margins=[]
    all_tile=BVHTree.FromPolygons(verts,[p.vertices[:] for p in mesh.polygons if p.material_index==3])
    for i in timber_ids-shell_ids:
        p=verts[i]
        if max(abs(p.x),abs(p.y))<4.65 or p.z<3.4:continue
        top=all_tile.ray_cast((p.x*.99999,p.y*.99999,8),(0,0,-1))[0]
        assert top is not None,(mesh.name,'uncovered timber',tuple(p))
        margins.append(top.z-p.z)
    max_cap=-1.
    for sx in (-1,1):
        for sy in (-1,1):
            for i in range(26):
                for j in range(25):
                    x=sx*(1.42+.009*i);y=sy*(1.38+.009*j)
                    for cap in caps:
                        point=cap.ray_cast((x,y,8),(0,0,-1))[0]
                        if point is not None:max_cap=max(max_cap,point.z)
    timber=BVHTree.FromPolygons(verts,[p.vertices[:] for p in mesh.polygons if p.material_index==2])
    sill=timber.ray_cast((.21,-1.49,5.82),(0,0,-1))[0]
    assert sill is not None and 5.4<sill.z<5.7,(mesh.name,'missing skylight sill')
    sill_z=sill.z
    assert all(test['min_seating_depth_m']>0 for test in overlap.values()),(mesh.name,'unseated hip')
    assert margins and min(margins)>0,(mesh.name,'timber above tile')
    assert 0<max_cap<sill_z,(mesh.name,'hip end above skylight sill')
    result={'hip_seating':overlap,'outboard_timber_vertices':len(margins),
            'min_tile_above_outboard_timber_m':round(min(margins),4),
            'min_sill_top_above_hip_end_m':round(sill_z-max_cap,4)}
    return result


def inspect(models,fbx):
    initial=json.loads((R2/'input-sha256.json').read_text(encoding='utf8'))
    before={Path(k).name:v for k,v in initial.items() if k.startswith('Assets/')}
    hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in ASSET.iterdir() if p.is_file()}
    hashes[fbx.name]=hashlib.sha256(fbx.read_bytes()).hexdigest()
    changed=[k for k,v in hashes.items() if before.get(k)!=v]
    baseline='--baseline' in sys.argv
    previous=json.loads((REVIEW/'surface-fbx-readback.json').read_text(encoding='utf8'))
    assert set(before)==set(hashes)
    assert set(changed)==(set() if baseline else {fbx.name})
    report={'blender':bpy.app.version_string,'meshes':{},'changed':changed,
            'sha256':{k:hashes[k] for k in changed},'unity_surfaces':unity_surfaces(before)}
    prefab=(ASSET/'TianyiPavilion.prefab').read_text(encoding='utf8')
    guid=re.search(r'^guid: (\w+)',(ASSET/'TianyiPavilion.fbx.meta').read_text(),re.M)[1]
    refs=re.findall(r'm_Mesh: \{fileID: (-?\d+), guid: '+guid,prefab)
    assert set(refs)=={'-1179435197494004938','1291247432581136521','-4717050247385379717'}
    report['prefab_mesh_refs']={'guid':guid,'fileIDs':refs}
    report['prefab_colliders']={name:prefab.count('\n'+name+':') for name in ('BoxCollider','MeshCollider')}
    assert report['prefab_colliders']==previous['prefab_colliders']
    assert report['prefab_mesh_refs']==previous['prefab_mesh_refs']
    report['textures']={}
    for filename in changed:
        if filename.endswith('.png'):
            image=bpy.data.images.load(str(ASSET/filename),check_existing=False)
            report['textures'][filename]=list(image.size)
            assert min(image.size)>=1024
            bpy.data.images.remove(image)
    # Exhibit bounds come from the nested prefab's previous Unity measurement.
    boxes={'instrument':((-1.57,-1.44,.002),(1.57,1.44,3.29)),
           'axial_passage_at_shoes':((-1.29,-5.6,.002),(1.29,5.6,.30)),
           'axial_passage_above_shoes':((-1.36,-5.6,.30),(1.36,5.6,2.5)),
           'skylight':((-1.32,-1.32,3.29),(1.32,1.32,6.3))}
    for index,o in enumerate(models):
        m=o.data
        budget=[12716,5500,1836][index]
        assert len(m.polygons)<=budget,(o.name,len(m.polygons),budget)
        assert o.name==m.name==f'Pavilion_LOD{index}' and m.uv_layers.active.name=='TilingUV'
        assert all(len(p.vertices)==3 for p in m.polygons)
        assert [a.name for a in m.materials]==['Paving','Fieldstone','Timber','Tile']
        verts=[o.matrix_world@v.co for v in m.vertices]
        bands={'stone':0,'earth':0}
        for poly in m.polygons:
            if poly.material_index!=1:continue
            vs=[m.uv_layers.active.data[i].uv.y for i in poly.loop_indices]
            if max(verts[i].z for i in poly.vertices)>.431:
                assert min(vs)>.5 and max(vs)<1,(o.name,'earth UV',vs)
                bands['earth']+=1
            else:
                assert min(vs)>0 and max(vs)<.5,(o.name,'stone UV',vs)
                bands['stone']+=1
        assert min(bands.values())>0
        tree=BVHTree.FromPolygons(verts,[p.vertices[:] for p in m.polygons])
        for key,(low,high) in boxes.items():
            assert not overlaps(tree,low,high),(o.name,key)
            assert not any(all(a<b<c for a,b,c in zip(low,v,high)) for v in verts)
        report['meshes'][o.name]={'triangles':len(m.polygons),'before_triangles':previous['meshes'][o.name]['triangles'],
                                  'max_triangles':budget,'dimensions_m':list(o.dimensions),
                                  'materials':[a.name for a in m.materials],'UV':m.uv_layers.active.name,
                                  'clear_volumes_m':boxes,'wall_atlas_triangles':bands,
                                  'roof':roof_measurements(m,verts)}
        if not baseline:
            roof=report['meshes'][o.name]['roof']
            assert all(test['minimum_tile_over_timber_m']>0 for test in roof['four_corner_raycast'].values())
            report['meshes'][o.name]['joints']=inspect_joints(m,verts)
    report['surface_sources']='PhotoArchitecture Flags paving; PhotoArchitecture Brick split-block colour and normal pair with Workshop Ashlar mineral detail; Workshop Plaster with PhotoArchitecture Lime detail; photographed end grain with latewood and drying checks.'
    report['qa_notes']=['Repeated trim UVs share texels across separate members.',
                        'The existing stone footing reaches -0.20 m. Its walking plane is z=0.',
                        'Prefab fileIDs are the saved references. Unity import, Play and VR performance belong to the integration session.']
    name='A1' if baseline else 'R2'
    write('R2/'+name+'-readback.json',report)


def bind_surfaces(models):
    for i,entry in enumerate(SURFACES):
        material=fresh_material(*entry)
        for o in models:o.data.materials[i]=material


def display_instrument():
    # Read-only, static review pose of the instrument nested in the set.
    # Unity's two parent Y offsets are 0.55 m (frame) and 1.46 m (shell).
    opening=ASSET.parent
    bronze=bpy.data.materials.new('Review mount bronze')
    bronze.diffuse_color=(.18,.12,.064,1)
    sources=[('TianyiDais/TianyiDais_LOD0.fbx','TianyiDais','TianyiDais',0.),
             ('ExteriorLOD/ArmillarySeparated.fbx','ConnectivityLOD/BrokenArmillary','BrokenArmillary',.55),
             ('StarShell/StarShell_LOD0.fbx','StarShell','StarShell',2.01),
             ('StarShell/StarShellMounts.fbx',None,None,2.01)]
    for file,folder,stem,z in sources:
        existing=set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(opening/file),use_image_search=False)
        material=bronze
        if folder:
            tex=opening/folder/'Textures'
            material=fresh_material(stem+' review',tex/(stem+'_BaseColor.png'),tex/(stem+'_Normal.png'),.72,.32)
        for o in set(bpy.data.objects)-existing:
            if o.parent is None:o.location+=Vector((-.000121,.000052,z))
            if o.type=='MESH':
                o.data.materials.clear();o.data.materials.append(material)


def preview(models,label):
    for o in models[1:]:o.hide_render=True
    display_instrument()
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=12
    scene.cycles.use_denoising=True
    scene.world=bpy.data.worlds.new('Sky')
    scene.world.use_nodes=True
    bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
    bg.inputs[0].default_value=(.69,.77,.87,1)
    bg.inputs[1].default_value=.65
    bpy.ops.object.light_add(type='SUN')
    sun=bpy.context.object
    sun.data.energy=3.0
    sun.data.angle=.13
    sun.data.color=(1,.891,.761)
    sun.rotation_euler=Vector((.65,.52,-.99)).to_track_quat('-Z','Y').to_euler()
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.202))
    ground=bpy.data.materials.new('Backdrop')
    ground.diffuse_color=(.17,.158,.138,1)
    bpy.context.object.data.materials.append(ground)
    bpy.ops.object.camera_add()
    cam=bpy.context.object
    scene.camera=cam
    scene.render.resolution_x=1120
    scene.render.resolution_y=720
    scene.render.resolution_percentage=100
    for name,pos,target,lens in [
            ('front',(0,-23.2,1.63),(0,0,2.69),39),
            ('left-eave',(-7.78,-9.62,3.84),(-4.50,-4.92,4.06),37),
            ('right-eave',(7.78,-9.62,3.84),(4.50,-4.92,4.06),37),
            ('wing',(2.96,-8.3,2.33),(2.55,-4.0,2.42),34),
            ('skylight',(4.36,-6.28,7.92),(0,0,5.50),45)]:
        cam.location=pos
        cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        cam.data.lens=lens
        scene.render.filepath=str(R2/(label+'-'+name+'.jpg'))
        scene.render.image_settings.file_format='JPEG'
        scene.render.image_settings.quality=93
        bpy.ops.render.render(write_still=True)


if __name__=='__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fbx=(REVIEW if '--stage' in sys.argv else ASSET)/'TianyiPavilion.fbx'
    bpy.ops.import_scene.fbx(filepath=str(fbx),use_image_search=False)
    models=sorted(bpy.context.scene.objects,key=lambda o:o.name)
    assert len(models)==3
    inspect(models,fbx)
    if '--render' in sys.argv or '--qa-scripts' in sys.argv:bind_surfaces(models)
    if '--qa-scripts' in sys.argv:
        sys.path.insert(0,sys.argv[sys.argv.index('--qa-scripts')+1])
        import qa_check
        qa={o.name:qa_check.run(objects=[o.name]) for o in models}
        write('R2/geometry-qa.json',qa)
        assert all(result['pass'] for result in qa.values()),[(key,r['errors']) for key,r in qa.items()]
    if '--render' in sys.argv:preview(models,'A1' if '--baseline' in sys.argv else 'R2')

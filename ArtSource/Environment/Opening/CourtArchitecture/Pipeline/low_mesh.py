"""Stage generic low meshes from a curated, already oriented meter-scale source."""
import bpy,bmesh,math,json
from mathutils import Vector

def shared_materials(obj):
    for mat in obj.data.materials:
        if mat is None:raise ValueError("Empty material slot in high")
    if not obj.data.materials:raise ValueError("Source missing surface")

def clean(mesh,epsilon):
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=epsilon)
    bmesh.ops.dissolve_degenerate(bm,dist=epsilon*.6,edges=list(bm.edges))
    loose=[v for v in bm.verts if not v.link_faces]
    if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    mesh.validate(clean_customdata=False);mesh.update()

def make_low(high,name,budget,collection):
    low=bpy.data.objects.new(name,high.data.copy());collection.objects.link(low)
    low.matrix_world=high.matrix_world.copy()
    # Input is already a meter-fit, photographed repair. Retain reconstructed silhouettes.
    clean(low.data,2e-6)
    t=sum(len(p.vertices)-2 for p in low.data.polygons)
    modifier=low.modifiers.new("Silhouette collapse","DECIMATE");modifier.ratio=min(1,budget/max(1,t));modifier.use_collapse_triangulate=True
    bpy.context.view_layer.objects.active=low
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    clean(low.data,1e-6)
    if name.startswith(('CourtGatehouse','CourtLowWing')):
        # The generator emitted one detached half-finial beyond the right ridge stop.
        # Keep the ridge-end support and remove only disconnected specks by topology.
        bm=bmesh.new();bm.from_mesh(low.data);seen=set();drop=[]
        for start in bm.verts:
            if start in seen:continue
            stack=[start];seen.add(start);island=[]
            while stack:
                cur=stack.pop();island.append(cur)
                for edge in cur.link_edges:
                    n=edge.other_vert(cur)
                    if n not in seen:seen.add(n);stack.append(n)
            x=sum(v.co.x for v in island)/len(island);z=sum(v.co.z for v in island)/len(island)
            if len(island)<=200 and x>2.6 and z>1.9:drop.extend(island)
        if drop:bmesh.ops.delete(bm,geom=drop,context='VERTS')
        if name.startswith('CourtGatehouse'):
            for sx in (-1,1):
                for sy in (-1,1):
                    feet=[v for v in bm.verts if v.co.x*sx>0 and v.co.y*sy>0 and v.co.z<.52]
                    if feet:
                        bottom=min(v.co.z for v in feet)
                        for v in feet:v.co.z-=bottom*max(0,min(1,(.52-v.co.z)/(.52-bottom)))
        bm.to_mesh(low.data);bm.free();low.data.update()
    low.data.materials.clear()
    material=bpy.data.materials.new(name+'_Baked');material.use_nodes=True;low.data.materials.append(material)
    for p in low.data.polygons:p.material_index=0
    return low

def validate_low(obj):
    m=obj.data;m.calc_loop_triangles()
    bm=bmesh.new();bm.from_mesh(m)
    metrics={'triangles':len(m.loop_triangles),'verts':len(m.vertices),'boundary_edges':sum(e.is_boundary for e in bm.edges),
             'overfull_edges':sum(len(e.link_faces)>2 for e in bm.edges),'zero_area':sum(f.calc_area()<1.e-12 for f in bm.faces),
             'zero_length':sum(e.calc_length()<1e-7 for e in bm.edges),'UV_layers':len(m.uv_layers)}
    bm.free();return metrics


def strip_clip_slivers(obj):
    # The four clipped brackets can leave an isolated single triangle below the wing.
    # Remove them after projection, preserving every surviving face's baked atlas UV.
    bm=bmesh.new();bm.from_mesh(obj.data)
    debris=[f for f in bm.faces if len(f.verts)==3 and all(len(v.link_faces)==1 for v in f.verts)]
    if debris:bmesh.ops.delete(bm,geom=list({v for f in debris for v in f.verts}),context='VERTS')
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    return len(debris)

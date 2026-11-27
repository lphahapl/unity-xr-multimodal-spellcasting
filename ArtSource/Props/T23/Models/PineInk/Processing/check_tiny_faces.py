import bpy,bmesh
for obj in bpy.context.scene.objects:
    if obj.type!="MESH" or "_Joined" not in obj.name:continue
    mesh=bmesh.new()
    mesh.from_mesh(obj.data)
    bad=[f for f in mesh.faces if f.calc_area()<1e-10]
    print(obj.name,[(round(f.calc_area()*1e10,4),sorted(round(e.calc_length()*1e6,2) for e in f.edges)) for f in bad],flush=True)
    mesh.free()


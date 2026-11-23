def material(name, lo, hi, metal, smooth, grain=(12, 12, 12), glow=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*hi, 1)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = 1 - smooth
    pos = nodes.new("ShaderNodeAttribute"); pos.attribute_name="WorkshopCraftPosition"
    mapping = nodes.new("ShaderNodeVectorMath"); mapping.operation = "MULTIPLY"
    links.new(pos.outputs["Vector"], mapping.inputs[0])
    mapping.inputs[1].default_value = grain
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 3.5
    noise.inputs["Detail"].default_value = 3.0
    links.new(mapping.outputs[0], noise.inputs["Vector"])
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = .20; ramp.color_ramp.elements[0].color = (*lo,1)
    ramp.color_ramp.elements[1].position = .82; ramp.color_ramp.elements[1].color = (*hi,1)
    links.new(noise.outputs["Fac"], ramp.inputs[0])
    links.new(ramp.outputs[0], shader.inputs["Base Color"])
    bump = nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = .2
    bump.inputs["Distance"].default_value = .00038
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs[0], shader.inputs["Normal"])
    if glow:
        shader.inputs["Emission Color"].default_value = (*hi, 1)
        shader.inputs["Emission Strength"].default_value = .22
    shades[mat] = (shader, ramp, metal, smooth, glow)
    return mat

def active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True); bpy.context.view_layer.objects.active = obj

def bake_mod(obj, mod):
    active(obj); bpy.ops.object.modifier_apply(modifier=mod.name)

def soften(obj, width, segments):
    if not width: return
    m = obj.modifiers.new("Chamfer", "BEVEL")
    m.width = width; m.segments = segments; m.affect = "EDGES"
    bake_mod(obj,m)

def box(name, pos, dims, mat, high, radius=.004, coarse=False, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj=bpy.context.object; obj.name=name
    obj.dimensions=dims
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rot is not None:
        obj.rotation_mode="QUATERNION"; obj.rotation_quaternion=rot
    if high or coarse: soften(obj, radius, 4 if high else 1)
    obj.data.materials.append(mat); parts.append(obj)
    return obj

def beam(name, a,b,w,d,mat,high, radius=.004,coarse=False):
    a,b=Vector(a),Vector(b); span=b-a
    return box(name,(a+b)*.5,(w,d,span.length),mat,high,radius,coarse,span.to_track_quat("Z","Y"))

def profile(name, rings, ring_mats, segments, plane="dial", centre=(0,0,0)):
    # A solid of revolution. Each ring is (radius, axial coordinate).
    verts=[]; faces=[]; mats=[]; stacks=[]
    for r,h in rings:
        row=[]
        for i in range(1 if r==0 else segments):
            x,z=r*cos(2*pi*i/segments),r*sin(2*pi*i/segments)
            pt=(x,h,z) if plane=="dial" else (x,z,h)
            row.append(len(verts)); verts.append(tuple(Vector(centre)+Vector(pt)))
        stacks.append(row)
    for i,(a,b) in enumerate(zip(stacks,stacks[1:])):
        for j in range(segments):
            k=(j+1)%segments
            ids=[a[j%len(a)],a[k%len(a)],b[k%len(b)],b[j%len(b)]]
            faces.append(list(dict.fromkeys(ids)));mats.append(ring_mats[i])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces)
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
    for mat in dict.fromkeys(mats):mesh.materials.append(mat)
    for p,m in zip(mesh.polygons,mats):p.material_index=list(mesh.materials).index(m)
    import bmesh
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(mesh);bm.free()
    parts.append(obj);return obj

def join_asset(name, collection):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        host="Gauge plate" if name.startswith("TargetGaugeFace") else obj.get("bake_host",obj.name.split(".")[0])
        group=group_ids.setdefault(host,len(group_ids)+1)
        attribute=obj.data.attributes.new("WorkshopBakePart","INT","FACE")
        for value in attribute.data:value.value=group
        obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1:bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name;obj.data.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    coords=obj.data.attributes.new("WorkshopCraftPosition","FLOAT_VECTOR","POINT")
    for v,p in zip(obj.data.vertices,coords.data):p.vector=v.co
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj); parts.clear()
    return obj
wood = material('Blackened elm', (.022,.010,.005), (.21,.092,.034), .025,.32,(.8,42,70))
endwood = material('Smoked upright', (.029,.012,.005), (.23,.105,.040), .025,.31,(54,45,1.1))
ruler = material('Oiled boxwood', (.18,.09,.034), (.57,.37,.16), .015,.43,(1.7,46,78))
bronze = material('Weathered measuring bronze', (.085,.064,.031), (.43,.30,.126), .7,.38)
light = material('Polished gauge edge', (.25,.17,.07), (.59,.42,.23), .77,.47)
ink = material('Scored numeral marks', (.015,.011,.006), (.047,.027,.012), .015,.31)
cord = material('Waxed flax', (.13,.078,.033), (.42,.288,.137), .0,.23,(15,15,55))
felt = material('Muted indigo felt', (.013,.030,.033), (.06,.095,.088), .0,.16,(90,90,45))
lowmat=bpy.data.materials.new('Pavilion_Baked');lowmat.use_nodes=True


def solid(name,verts,faces,mat):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(mesh);bm.free()
    ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);mesh.materials.append(mat);parts.append(ob)
    return ob

def arc(name,x,y,z,r,thickness,width,high):
    seg=64 if high else 23
    verts=[]
    for j in range(seg+1):
        a=.055*pi+j*.89*pi/seg
        for radius,yy in [(r,y-width/2),(r-thickness,y-width/2),(r-thickness,y+width/2),(r,y+width/2)]:
            verts.append((x+radius*cos(a),yy,z+radius*sin(a)))
    faces=[(0,1,2,3),(seg*4,seg*4+1,seg*4+2,seg*4+3)]
    for j in range(seg):
        for i in range(4):faces.append((j*4+i,j*4+(i+1)%4,(j+1)*4+(i+1)%4,(j+1)*4+i))
    ob=solid(name,verts,faces,bronze)
    if high:
        soften(ob,.0015,2)
        for j in range(1,48):
            a=.055*pi+j*.89*pi/48
            height=thickness*(.7 if j%4==0 else .37)
            outer=r-.002
            v=Vector((cos(a),0,sin(a)))
            center=Vector((x,y-width/2-.0004,z))
            etch=beam('Arc cut',center+v*(outer-height),center+v*outer,.0028,.001,ink,False)
            etch['bake_host']=name
def ring(name,x,y,z,r,high):
    ns=26 if high else 12
    nt=9 if high else 5
    verts=[];faces=[]
    for i in range(ns):
        a=2*pi*i/ns
        for j in range(nt):
            b=2*pi*j/nt; rr=r+.004*cos(b)
            verts.append((x+rr*cos(a),y+.004*sin(b),z+rr*sin(a)))
    for i in range(ns):
        for j in range(nt):faces.append((i*nt+j,((i+1)%ns)*nt+j,((i+1)%ns)*nt+(j+1)%nt,i*nt+(j+1)%nt))
    solid(name,verts,faces,bronze)

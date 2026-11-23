using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class CourtyardInfillAuthor
{
    const string Root="Assets/Art/Environment/Opening/CourtyardInfill/";
    static readonly List<string> Log=new List<string>();
    static float xSign,zSign;
    static readonly string[] Groups={"B01","B02","B03","B04","B05"};
    static readonly string[][] Modules={
        new[]{"WallStraight","WallCorner","WallEnd"},new[]{"GalleryStraight","GalleryCorner"},
        new[]{"StairFlight","PlatformEdge","PlatformCorner"},new[]{"CliffGateCrown"},new[]{"CanalLantern"}};
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    // Model source uses Blender X,Y horizontally, Z up. Derive import handedness from the asymmetric L wall.
    static Vector3 Pos(float x,float y,float z){return new Vector3(x*xSign,z,y*zSign);}
    static BoxCollider Box(GameObject go,string name,Vector3 at,Vector3 dims){
        var child=new GameObject(name);child.transform.SetParent(go.transform,false);
        child.transform.localPosition=at;var col=child.AddComponent<BoxCollider>();col.size=dims;return col;
    }
    static T SaveData<T>(T item,string path) where T:UnityEngine.Object {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(item,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(item);return old;}
        AssetDatabase.CreateAsset(item,path);return item;
    }
    static Mesh Ramp(){
        var m=new Mesh{name="Continuous stair ramp"};
        var v=new Vector3[6];int i=0;
        foreach(float x in new[]{-1.5f,1.5f}){
            v[i++]=Pos(x,-.48f,0);v[i++]=Pos(x,.48f,0);v[i++]=Pos(x,.48f,.45f);
        }
        m.vertices=v;int[] tri={0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,0,3,5,0,5,2};
        // For the top to be walkable, the ramp triangles must face toward the air.
        if(Vector3.Cross(v[tri[19]]-v[tri[18]],v[tri[20]]-v[tri[18]]).y<0)
            for(int a=0;a<tri.Length;a+=3)(tri[a+1],tri[a+2])=(tri[a+2],tri[a+1]);
        m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();
        return SaveData(m,Root+"B03/StairRampCollision.asset");
    }
    static void Collisions(GameObject obj,string n,Bounds b){
        if(n=="CliffGateCrown")return;
        if(n=="StairFlight"){obj.AddComponent<MeshCollider>().sharedMesh=Ramp();return;}
        if(n=="WallCorner" || n=="PlatformCorner"){
            bool wall=n=="WallCorner";float w=wall?.34f:.32f,h=wall?1.62f:.45f;
            Box(obj,"West leg",Pos(-.5f,0,h*.5f),new Vector3(1,h,w));
            Box(obj,"North leg",Pos(0,.5f,h*.5f),new Vector3(w,h,1));
            return;
        }
        if(n.StartsWith("Gallery")){
            Box(obj,"Walkable bay",Pos(0,0,.0475f),new Vector3(3f,.095f,n=="GalleryCorner"?3f:2.94f));
            Vector2[] sites=n=="GalleryStraight"?
                new[]{new Vector2(-.78f,-1.365f),new Vector2(-.78f,1.365f),new Vector2(.78f,-1.365f),new Vector2(.78f,1.365f)}:
                new[]{new Vector2(-.83f,-1.365f),new Vector2(-.83f,1.365f),new Vector2(1.365f,-.81f),new Vector2(1.365f,.82f)};
            foreach(var s in sites)Box(obj,"Timber post",Pos(s.x,s.y,1.405f),new Vector3(.182f,2.62f,.182f));
            return;
        }
        Box(obj,"Fixed body",b.center,b.size);
    }
    static void ValidateRamp(GameObject prefab){
        var inst=UnityEngine.Object.Instantiate(prefab);inst.transform.position=new Vector3(1000,1000,1000);Physics.SyncTransforms();
        try{
            var c=inst.GetComponent<MeshCollider>();
            for(int i=1;i<=5;i++){
                var t=i/6f;var sample=inst.transform.position+Pos(0,-.48f+t*.96f,1);
                Check(c.Raycast(new Ray(sample,Vector3.down),out var hit,1.1f),"Missing stair collision");
                Check(Mathf.Abs(hit.point.y-inst.transform.position.y-t*.45f)<.004f,"Stepped stair collider");
            }
        }finally{UnityEngine.Object.DestroyImmediate(inst);}
    }
    static void MakeProp(string key,string n){
        var folder=Root+key+"/";var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+key+"_Infill.mat");
        Check(mat&&mat.shader.name=="Universal Render Pipeline/Lit","Missing URP material "+n);
        foreach(var slot in new[]{"_BaseMap","_BumpMap","_OcclusionMap","_MetallicGlossMap"})
            Check(mat.GetTexture(slot),"Missing bake slot "+n+slot);
        var obj=new GameObject(n);var lod=obj.AddComponent<LODGroup>();
        try{
            var levels=new LOD[3];var tris=new List<int>();
            for(int t=0;t<3;t++){
                var fbx=AssetDatabase.LoadAssetAtPath<GameObject>(folder+n+"_LOD"+t+".fbx");
                Check(fbx,"Missing FBX "+n);var child=(GameObject)PrefabUtility.InstantiatePrefab(fbx);child.transform.SetParent(obj.transform,false);
                var r=child.GetComponentsInChildren<MeshRenderer>().Single();
                Check(r.sharedMaterial==mat,"Missing remap "+n);
                int tri=(int)(r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3);
                Check(tri>0 && (t==0 || tri<tris[t-1]),"Invalid LOD "+n);tris.Add(tri);
                levels[t]=new LOD(new[]{.25f,.095f,.0035f}[t],new Renderer[]{r});
            }
            lod.SetLODs(levels);lod.RecalculateBounds();var bounds=levels[0].renderers[0].bounds;Collisions(obj,n,bounds);
            var path=folder+n+".prefab";PrefabUtility.SaveAsPrefabAsset(obj,path);
            var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path);var colliders=saved.GetComponentsInChildren<Collider>();
            Check(saved.GetComponent<LODGroup>().GetLODs().Length==3,"Missing LODGroup");
            if(n=="CliffGateCrown")Check(colliders.Length==0 && bounds.size.x>3.1 && bounds.min.y>-.012f,"Wrong crown bearing or collision");
            if(n=="StairFlight")ValidateRamp(saved);
            if(n.StartsWith("Gallery")){
                Check(colliders.Length==5,"Wrong gallery collision");
                Check(!colliders.Any(c=>c.bounds.Contains(new Vector3(0,1.6f,0))),"Blocked central gallery route");
            }
            Log.Add(key+" "+n+" triangles "+string.Join("/",tris)+" size "+bounds.size.ToString("F3")+" colliders="+colliders.Length);
        }finally{UnityEngine.Object.DestroyImmediate(obj);}
    }
    static Mesh CloudMesh(){
        var verts=new List<Vector3>();var uv=new List<Vector2>();var cols=new List<Color>();var ix=new List<int>();
        const int nx=8,nz=6;
        for(int k=0;k<2;k++){
            int offset=verts.Count;float theta=k*.59f;
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){
                float u=(float)x/nx,v=(float)z/nz,dx=(u-.5f)*(k==0?84:70),dz=(v-.5f)*(k==0?45:40);
                float xx=dx*Mathf.Cos(theta)-dz*Mathf.Sin(theta)+k*26,zz=dx*Mathf.Sin(theta)+dz*Mathf.Cos(theta)+k*9;
                verts.Add(new Vector3(xx,k*-4f+Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI)*1.12f,zz));
                uv.Add(new Vector2(k==0?u:1-u,v));cols.Add(new Color(1,1,1,k==0?.84f:.72f));
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){
                int a=offset+x+z*(nx+1),b=a+nx+1;ix.AddRange(new[]{a,b,b+1,a,b+1,a+1});
            }
        }
        var m=new Mesh{name="Offset cloud banks"};m.SetVertices(verts);m.SetUVs(0,uv);m.SetColors(cols);m.SetTriangles(ix,0);m.RecalculateBounds();return m;
    }
    static void CloudProbe(Mesh mesh,Material material){
        const int dim=256;var rt=RenderTexture.GetTemporary(dim,dim,24,RenderTextureFormat.ARGB32);
        var old=RenderTexture.active;var read=new Texture2D(dim,dim,TextureFormat.RGB24,false,true);
        try{
            for(int i=0;i<4;i++){
                float eye=(i%2==0?-.032f:.032f);float side=i<2?1:-1;
                var pos=new Vector3(eye,side*50,-33);
                var rotation=Quaternion.LookRotation(Vector3.zero-pos,Vector3.up);
                var view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(pos,rotation,Vector3.one).inverse;
                var proj=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-68,68,-55,55,.1f,300),true);
                using(var cmd=new CommandBuffer()){
                    cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(true,true,Color.black);cmd.SetViewProjectionMatrices(view,proj);
                    cmd.DrawMesh(mesh,Matrix4x4.identity,material,0,0);Graphics.ExecuteCommandBuffer(cmd);
                }
                RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,dim,dim),0,0);read.Apply();
                var px=read.GetPixels32();int lit=px.Count(c=>c.b>3);int max=px.Max(c=>(int)c.b);
                Check(lit>250 && lit<dim*dim*.65f && max>30 && max<255,"Cloud probe failed "+lit+"/"+max);
                File.WriteAllBytes("InfillCloudEye"+i+".png",read.EncodeToPNG());
                Log.Add("C01 "+(side>0?"above":"below")+" eye="+eye+" simulated, coloredPixels="+lit+", maxBlue="+max);
            }
        }finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(read);RenderTexture.ReleaseTemporary(rt);}
    }
    static void Cloud(){
        var f=Root+"C01/";var shader=AssetDatabase.LoadAssetAtPath<Shader>(f+"ValleyCloud.shader");
        Check(shader && shader.isSupported,"Unsupported cloud shader");
        var m=new Material(shader){name="Valley cloud",enableInstancing=true};
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(f+"ValleyCloud.png"));
        m.SetFloat("_Opacity",.72f);m=SaveData(m,f+"ValleyCloud.mat");
        var mesh=SaveData(CloudMesh(),f+"ValleyCloud.asset");
        var obj=new GameObject("ValleyCloudBank");
        try{
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=m;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            // Fog above a ravine is viewed from above: lower sheet draws before upper sheet.
            var inds=mesh.triangles;int half=inds.Length/2;
            mesh.triangles=inds.Skip(half).Concat(inds.Take(half)).ToArray();EditorUtility.SetDirty(mesh);
            var path=f+"ValleyCloudBank.prefab";PrefabUtility.SaveAsPrefabAsset(obj,path);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab.GetComponentsInChildren<Renderer>().Length==1&&prefab.GetComponentsInChildren<Collider>().Length==0&&prefab.GetComponentsInChildren<Light>().Length==0,"Cloud components");
            CloudProbe(mesh,m);Log.Add("C01 ValleyCloudBank: two offset cards; 192 triangles; one draw; no collider/light/shadows; static material; opacity=.72");
        }finally{UnityEngine.Object.DestroyImmediate(obj);}
    }
    public static void Run(){
        Log.Clear();
        var corner=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"B01/WallCorner_LOD0.fbx");
        var b=corner.GetComponentInChildren<MeshRenderer>().bounds;
        xSign=-Mathf.Sign(b.center.x);zSign=Mathf.Sign(b.center.z);
        for(int i=0;i<Groups.Length;i++)foreach(var n in Modules[i])MakeProp(Groups[i],n);
        Cloud();AssetDatabase.SaveAssets();File.WriteAllLines("CourtyardInfillReport.txt",Log);
        Debug.Log("COURTYARD_INFILL_VERIFIED\n"+string.Join("\n",Log));
    }
}

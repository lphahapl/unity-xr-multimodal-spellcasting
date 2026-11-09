using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Offline authoring only. Parent invokes in isolated Unity; no scene saves, old-object edits,
// existing colliders, behaviours, cameras or lighting. Accepted A07 tables own fixed new boxes.
// Source mesh data was authored/rendered in Blender.
public static class ArchitectureBuilder
{
    const string Art="Assets/Art/Environment/Opening/ArchitectureLOD/";
    const string Log="Logs/exterior-world-lod-20261109/architecture/";
    [Serializable] sealed class Data { public float[] vertices; public float[] uv; public int[] triangles; public string name; }
    static readonly List<string> evidence=new List<string>();
    static Material material;
    static int nearTriangles;

    public static void Build(Transform root,Scene scene,Func<float,float,float> groundHeight)
    {
        if(!root||root.gameObject.scene!=scene||groundHeight==null)throw new ArgumentException("Architecture requires a new root in target scene and a real terrain sampler");
        if(root.GetComponentsInChildren<LODGroup>(true).Any(g=>g.name.StartsWith("工官建筑")))throw new InvalidOperationException("Architecture already authored beneath supplied root; parent should replace its new-content root before rebuilding");
        evidence.Clear();nearTriangles=0;
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        string[] names={"室顶","通廊顶","试炼间顶"},kits={"Wake","Corridor","Trial"};
        var source=new MeshRenderer[3];
        // Validate all original roof references before creating anything.
        for(int i=0;i<3;i++)
        {
            var found=all.Where(r=>r.name==names[i]&&Hierarchy(r.transform).Contains("第一章 · 初醒校验室")).ToArray();
            if(found.Length!=1)throw new Exception("Expected one original roof "+names[i]+", got "+found.Length);
            source[i]=found[0];var b=source[i].bounds;
            if(b.size.y>.8f||b.min.y<2.9f)throw new Exception("Unexpected original roof bounds "+names[i]+": "+b);
            for(int j=0;j<3;j++)Require(kits[i],j,true);
        }
        for(int i=0;i<3;i++)for(int j=0;j<3;j++)Require(i.ToString(),j,false);
        for(int lod=0;lod<3;lod++)if(!File.Exists(Art+"A07Workbench/SurveyWorkbench_LOD"+lod+".fbx"))throw new FileNotFoundException("Accepted A07 workbench LOD missing: "+lod);
        Directory.CreateDirectory(Art+"Meshes");Directory.CreateDirectory(Log);AssetDatabase.Refresh();
        material=MakeMaterial();
        float[] widths={13.2f,6.6f,14.9f},depths={13.35f,8f,16.6f};
        for(int i=0;i<3;i++)
        {
            var b=source[i].bounds;
            var go=Group(root,scene,"工官建筑 · "+names[i],new Vector3(b.center.x,b.max.y+.025f,b.center.z),0,1);
            var renderers=new Renderer[3];
            for(int lod=0;lod<3;lod++)
            {
                var mesh=new Geometry();mesh.Add(Read("Roof_"+kits[i]+"_LOD"+lod),new Vector3(b.size.x/widths[i],1,b.size.z/depths[i]));
                // New outside corner columns only. No bars or walls cross the ten existing windows/doors.
                foreach(float sx in new[]{-1f,1f})foreach(float sz in new[]{-1f,1f})
                {
                    float x=sx*(b.size.x*.5f+.12f),z=sz*(b.size.z*.5f+.12f);
                    var wp=go.transform.TransformPoint(new Vector3(x,0,z));float bottom=Sample(groundHeight,wp.x,wp.z)-go.transform.position.y-.28f;
                    float top=-.06f;mesh.Box(new Vector3(x,(bottom+top)*.5f,z),new Vector3(.40f,top-bottom,.40f),0,lod<2?.035f:0);
                    mesh.Box(new Vector3(x,bottom+.13f,z),new Vector3(.65f,.26f,.65f),0,lod<2?.04f:0);
                    mesh.Box(new Vector3(x,-.10f,z),new Vector3(.73f,.22f,.73f),1,lod<2?.025f:0);
                    if(lod<2)mesh.Box(new Vector3(x,-.28f,z),new Vector3(.43f,.09f,.43f),3);
                }
                renderers[lod]=Emit(go.transform,mesh,"Roof_"+kits[i]+"_Support_LOD"+lod,lod);
            }
            SetLOD(go,renderers);
            evidence.Add(names[i]+" original="+Hierarchy(source[i].transform)+" bounds="+b+" eaveWorldY="+go.transform.position.y+" rise="+(i==1?1.2f:1.45f)+"; source renderer/collider untouched; lowest new roof fabric="+(go.transform.position.y-.21f));
        }
        AddHall(root,scene,groundHeight,0,"西窗外 · 校矩作坊",new Vector3(-22,0,8),90,1.25f);
        AddHall(root,scene,groundHeight,1,"东窗外 · 规尺长坊",new Vector3(26,0,25),-90,1.35f);
        AddHall(root,scene,groundHeight,2,"测候台远景 · 校器工坊",new Vector3(28,0,66),-115,1.6f);
        if(nearTriangles>=18000)throw new Exception("Architecture near budget exceeded (target <18000): "+nearTriangles);
        evidence.Add("NEAR_TOTAL="+nearTriangles+" triangles; six building plus three independent bench LODGroups; one Renderer/material per active building or bench. New bench colliders=15 fixed boxes, unchanged by visual LOD; no runtime behaviours.");
        evidence.Add("Existing ten window openings/doors and original colliders are not modified. Parent disables old Workshop geometry and old GreyTileRoofCover renderer before final validation.");
        File.WriteAllLines(Log+"unity-build.txt",evidence);AssetDatabase.SaveAssets();
    }
    static void AddHall(Transform root,Scene scene,Func<float,float,float> ground,int variant,string name,Vector3 at,float yaw,float scale)
    {
        float w=new[]{6f,6.8f,7.3f}[variant]+.7f;
        var rotation=Quaternion.Euler(0,yaw,0);float[] xs={-w/2,0,w/2},zs={-2.4f,.3f,3f};var h=new float[3,3];float highest=float.NegativeInfinity,lowest=float.PositiveInfinity;
        for(int i=0;i<3;i++)for(int j=0;j<3;j++)
        {
            Vector3 p=at+rotation*(new Vector3(xs[i],0,zs[j])*scale);h[i,j]=Sample(ground,p.x,p.z);highest=Mathf.Max(highest,h[i,j]);lowest=Mathf.Min(lowest,h[i,j]);
            evidence.Add(name+" sample["+i+","+j+"]="+p.x+","+p.z+" height="+h[i,j]);
        }
        at.y=Mathf.Max(0,highest+.12f);var go=Group(root,scene,"工官建筑 · "+name,at,yaw,scale);var rs=new Renderer[3];
        for(int lod=0;lod<3;lod++)
        {
            var m=new Geometry();m.Add(Read("CalibrationHall_"+variant+"_LOD"+lod),Vector3.one);
            if(lod<2)
            {
                for(int i=0;i<2;i++)for(int j=0;j<2;j++)
                {
                    float min=Mathf.Min(h[i,j],h[i+1,j],h[i,j+1],h[i+1,j+1]);float bottom=(min-at.y)/scale-.30f,top=-.18f;
                    m.Box(new Vector3((xs[i]+xs[i+1])*.5f,(top+bottom)*.5f,(zs[j]+zs[j+1])*.5f),new Vector3(xs[i+1]-xs[i],top-bottom,zs[j+1]-zs[j]),0,lod==0?.045f:0);
                }
            }
            else
            {
                float bottom=(lowest-at.y)/scale-.30f;m.Box(new Vector3(0,(bottom-.18f)*.5f,.3f),new Vector3(w,-.18f-bottom,5.4f),0);
            }
            rs[lod]=Emit(go.transform,m,"CalibrationHall_"+variant+"_Grounded_LOD"+lod,lod);
        }
        SetLOD(go,rs);
        AddWorkbench(go.transform);
        foreach(var pair in new[]{new KeyValuePair<string,float>("Entrance",2.10f),new KeyValuePair<string,float>("WalkwayConnection",3f)})
        {
            var anchor=new GameObject(pair.Key);anchor.transform.SetParent(go.transform,false);anchor.transform.localPosition=new Vector3(0,0,pair.Value);
            evidence.Add(name+" "+pair.Key+" world="+anchor.transform.position.ToString("F4")+" forward="+anchor.transform.forward.ToString("F4")+" clear width="+(2.10f*scale)+" floorY="+at.y);
        }
    }
    static void AddWorkbench(Transform hall)
    {
        var table=new GameObject("A07 · 校器工作台");table.transform.SetParent(hall,false);table.transform.localPosition=new Vector3(0,0,.5f);
        Vector3 inherited=hall.lossyScale;if(Mathf.Min(inherited.x,inherited.y,inherited.z)<=0)throw new Exception("Workbench needs positive hall scale");
        table.transform.localScale=new Vector3(1/inherited.x,1/inherited.y,1/inherited.z);
        var benchMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"A07Workbench/SurveyWorkbench_Baked.mat");if(!benchMaterial)throw new Exception("Accepted A07 material missing");
        var rs=new Renderer[3];
        for(int lod=0;lod<3;lod++)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"A07Workbench/SurveyWorkbench_LOD"+lod+".fbx");if(!model)throw new Exception("A07 model not imported: "+lod);
            // Clone regular visual objects; asset meshes/materials remain referenced, never mutated.
            var instance=Object.Instantiate(model,table.transform,false);instance.name="视觉LOD"+lod;instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;instance.transform.localScale=Vector3.one;
            foreach(var nested in instance.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(nested);
            if(instance.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("A07 FBX unexpectedly imported colliders");
            var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);if(renderers.Length!=1)throw new Exception("A07 must have exactly one Renderer per LOD");
            var r=renderers[0];r.enabled=true;r.sharedMaterial=benchMaterial;r.shadowCastingMode=lod==2?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;r.reflectionProbeUsage=ReflectionProbeUsage.Off;rs[lod]=r;
            foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.isStatic=true;
            if(lod==0)
            {
                var benchMesh=r.GetComponent<MeshFilter>().sharedMesh;int count=Enumerable.Range(0,benchMesh.subMeshCount).Sum(s=>(int)benchMesh.GetIndexCount(s)/3);nearTriangles+=count;
                Vector3 expected=Abs(table.transform.right)*2f+Abs(table.transform.up)*.9f+Abs(table.transform.forward)*.85f;
                if((r.bounds.size-expected).magnitude>.10f)throw new Exception("A07 world size failed compensation: "+r.bounds.size+" expected "+expected);
                evidence.Add(hall.name+" A07 worldBounds="+r.bounds+" worldScale="+table.transform.lossyScale+" localScale="+table.transform.localScale+" triangles="+count);
            }
        }
        var lodGroup=table.AddComponent<LODGroup>();lodGroup.fadeMode=LODFadeMode.None;lodGroup.SetLODs(new[]{new LOD(.22f,new[]{rs[0]}),new LOD(.08f,new[]{rs[1]}),new LOD(.005f,new[]{rs[2]})});lodGroup.RecalculateBounds();
        // Fixed collision root is outside every visual level and never registered in the LODGroup.
        var physics=new GameObject("固定碰撞 · 台面与四脚");physics.transform.SetParent(table.transform,false);physics.isStatic=true;
        var top=physics.AddComponent<BoxCollider>();top.center=new Vector3(0,.825f,0);top.size=new Vector3(1.97f,.14f,.82f);
        foreach(float x in new[]{-.82f,.82f})foreach(float z in new[]{-.30f,.30f})
        {
            var leg=physics.AddComponent<BoxCollider>();leg.center=new Vector3(x,.39f,z);leg.size=new Vector3(.14f,.78f,.14f);
        }
        table.isStatic=true;evidence.Add(hall.name+" A07 local=(0,0,.5), world="+table.transform.position.ToString("F4")+" fixedColliders=5, visualLOD .22/.08/.005, independent of hall LOD; former merged table absent.");
    }
    static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
    static float Sample(Func<float,float,float> f,float x,float z){float v=f(x,z);if(float.IsNaN(v)||float.IsInfinity(v))throw new Exception("Terrain sampler returned invalid value at "+x+","+z);return v;}
    static string Hierarchy(Transform t)=>t.parent?Hierarchy(t.parent)+"/"+t.name:t.name;
    static void Require(string key,int lod,bool roof){string p=Art+(roof?"Roof_":"CalibrationHall_")+key+"_LOD"+lod+".json";if(!File.Exists(p))throw new FileNotFoundException("Run architecture_generate.py first",p);}
    static Data Read(string name){var d=JsonUtility.FromJson<Data>(File.ReadAllText(Art+name+".json"));if(d.vertices.Length%3!=0||d.uv.Length/2!=d.vertices.Length/3||d.triangles.Length%3!=0)throw new Exception("Invalid mesh data "+name);return d;}
    static GameObject Group(Transform root,Scene scene,string name,Vector3 p,float yaw,float scale)
    {
        var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));go.transform.localScale=Vector3.one*scale;go.transform.SetParent(root,true);return go;
    }
    static MeshRenderer Emit(Transform parent,Geometry g,string name,int lod)
    {
        var mesh=g.Mesh(name);string p=Art+"Meshes/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);}else{old=mesh;AssetDatabase.CreateAsset(old,p);}
        var go=new GameObject("LOD"+lod);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=old;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=lod==2?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;r.reflectionProbeUsage=ReflectionProbeUsage.Off;go.isStatic=true;
        if(lod==0)nearTriangles+=g.triangles.Count/3;evidence.Add(name+" triangles="+g.triangles.Count/3+" renderer=1 material=1");return r;
    }
    static void SetLOD(GameObject go,Renderer[] r)
    {
        var group=go.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;group.SetLODs(new[]{new LOD(.32f,new[]{r[0]}),new LOD(.13f,new[]{r[1]}),new LOD(.012f,new[]{r[2]})});group.RecalculateBounds();
        if(r.Select(x=>x.GetComponent<MeshFilter>().sharedMesh).Distinct().Count()!=3)throw new Exception("LOD meshes must differ");
    }
    static Material MakeMaterial()
    {
        string p=Art+"Architecture_Atlas.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}
        foreach(string role in new[]{"BaseColor","Normal","Mask"})
        {
            string path=Art+"Textures/Architecture_"+role+".png";AssetDatabase.ImportAsset(path);var t=(TextureImporter)AssetImporter.GetAtPath(path);
            if(t==null)throw new Exception("Missing atlas "+path);t.textureType=role=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;t.sRGBTexture=role=="BaseColor";t.mipmapEnabled=true;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Trilinear;t.maxTextureSize=1024;t.anisoLevel=2;
            var android=t.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=1024;android.format=TextureImporterFormat.ASTC_6x6;t.SetPlatformTextureSettings(android);t.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(role=="BaseColor")m.SetTexture("_BaseMap",texture);else if(role=="Normal")m.SetTexture("_BumpMap",texture);else{m.SetTexture("_MetallicGlossMap",texture);m.SetTexture("_OcclusionMap",texture);}
        }
        m.SetColor("_BaseColor",Color.white);m.SetFloat("_BumpScale",.6f);m.SetFloat("_Metallic",1);m.SetFloat("_Smoothness",1);m.SetFloat("_SmoothnessTextureChannel",0);m.SetFloat("_OcclusionStrength",.6f);
        m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    sealed class Geometry
    {
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> triangles=new List<int>();
        public void Add(Data d,Vector3 scale)
        {
            int offset=vertices.Count;for(int i=0;i<d.vertices.Length;i+=3)vertices.Add(Vector3.Scale(new Vector3(d.vertices[i],d.vertices[i+1],d.vertices[i+2]),scale));
            for(int i=0;i<d.uv.Length;i+=2)uv.Add(new Vector2(d.uv[i],d.uv[i+1]));foreach(int t in d.triangles)triangles.Add(t+offset);
        }
        void Face(IList<Vector3> points,int region)
        {
            int offset=vertices.Count;vertices.AddRange(points);
            for(int i=0;i<points.Count;i++)
            {
                Vector2 p=points.Count==4?new[]{new Vector2(.02f,.02f),new Vector2(.98f,.02f),new Vector2(.98f,.98f),new Vector2(.02f,.98f)}[i]:new Vector2(.5f+.48f*Mathf.Cos(i*2*Mathf.PI/points.Count),.5f+.48f*Mathf.Sin(i*2*Mathf.PI/points.Count));
                uv.Add(new Vector2((region%3+p.x)/3,(region/3+p.y)/2));
            }
            for(int i=1;i<points.Count-1;i++){triangles.Add(offset);triangles.Add(offset+i);triangles.Add(offset+i+1);}
        }
        public void Box(Vector3 p,Vector3 s,int region,float bevel=0)
        {
            float w=s.x/2,h=s.y/2,d=s.z/2,b=Mathf.Min(bevel,w*.25f,d*.25f);
            var ring=b>0?new[]{new Vector2(-w+b,-d),new Vector2(w-b,-d),new Vector2(w,-d+b),new Vector2(w,d-b),new Vector2(w-b,d),new Vector2(-w+b,d),new Vector2(-w,d-b),new Vector2(-w,-d+b)}:new[]{new Vector2(-w,-d),new Vector2(w,-d),new Vector2(w,d),new Vector2(-w,d)};
            var bottom=ring.Select(a=>p+new Vector3(a.x,-h,a.y)).ToArray();var top=ring.Select(a=>p+new Vector3(a.x,h,a.y)).ToArray();Face(bottom,region);Face(top.Reverse().ToArray(),region);
            for(int i=0;i<ring.Length;i++){int j=(i+1)%ring.Length;Face(new[]{bottom[j],bottom[i],top[i],top[j]},region);}
        }
        public Mesh Mesh(string name)
        {
            if(vertices.Count>=65535)throw new Exception("Unexpected architectural vertex count");var m=new Mesh{name=name};m.SetVertices(vertices);m.SetUVs(0,uv);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;
        }
    }
}

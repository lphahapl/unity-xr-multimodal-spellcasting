#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Install only in the isolated verification project's Assets/Editor. The scene
// is transient. Pass -b02Stage <directory containing the Blender audit>.
public static class B02R2Readback
{
    [Serializable] public class Placement { public string type; public float[] position, quaternion, scale; }
    [Serializable] public class Assembly { public Placement[] scene_instances_in_shared_parent; }
    [Serializable] public class Probe {
        public string unity;
        public int[] prefab_triangles;
        public int collider_count, route_samples;
        public float collider_floor_min, collider_floor_max, min_post_distance, tested_capsule_radius, capsule_height;
        public Vector3 local_reference, actual_bounds_size, actual_bounds_center;
        public float size;
        public string[] colliders, bindings;
    }
    static Vector3 V(float[] p) => new Vector3(p[0],p[1],p[2]);
    static Vector3 World(float x,float y,float z) => new Vector3(29.7f-y,z,45.95f+x);
    static void Need(bool ok,string issue) { if(!ok) throw new Exception(issue); }

    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();
        var stage=args[Array.IndexOf(args,"-b02Stage")+1];
        var placements=JsonUtility.FromJson<Assembly>(File.ReadAllText(Path.Combine(stage,"B02_R2_verification.json"))).scene_instances_in_shared_parent;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var groups=new List<LODGroup>();
        var ids=new List<string>();
        foreach(var path in Directory.GetFiles("Assets/B02","*.fbx").Select(p=>p.Replace('\\','/'))) {
            foreach(var o in AssetDatabase.LoadAllAssetsAtPath(path).Where(o=>o!=null)) {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long id);
                ids.Add(path+" "+o.GetType().Name+" "+o.name+" "+guid+" "+id);
            }
        }
        ids.Sort(StringComparer.Ordinal);
        File.WriteAllLines(Path.Combine(stage,"r2-identity.txt"),ids);
        foreach(var p in placements) {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/B02/"+p.type+".prefab");
            var inst=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            var q=p.quaternion;
            inst.transform.SetPositionAndRotation(V(p.position),new Quaternion(q[0],q[1],q[2],q[3]));
            inst.transform.localScale=V(p.scale);
            groups.Add(inst.GetComponent<LODGroup>());
        }
        var root=groups[0];
        var data=new Probe { unity=Application.unityVersion };
        var clips=root.GetComponentsInChildren<BoxCollider>();
        data.collider_count=clips.Length;
        Need(clips.Length==5 && clips.All(c=>c.enabled && !c.isTrigger),"corner collision rig");
        data.colliders=clips.Select(c=>c.name+" | "+root.transform.InverseTransformPoint(c.bounds.center).ToString("F4")+" | "+c.size.ToString("F4")).ToArray();
        var lods=root.GetLODs();
        data.prefab_triangles=new int[lods.Length];
        var binds=new List<string>();
        for(int i=0;i<lods.Length;i++) {
            var r=lods[i].renderers.Single();
            var m=r.GetComponent<MeshFilter>().sharedMesh;
            Need(m!=null && m.subMeshCount==1 && m.HasVertexAttribute(VertexAttribute.TexCoord1),"LOD mesh/UV2");
            Need(r.sharedMaterials.Length==1 && r.sharedMaterial.name=="B02_Infill","material remap");
            data.prefab_triangles[i]=(int)m.GetIndexCount(0)/3;
            binds.Add(AssetDatabase.GetAssetPath(m)+" | "+m.name+" | "+lods[i].screenRelativeTransitionHeight+" | "+r.sharedMaterial.shader.name);
        }
        data.bindings=binds.ToArray();
        root.RecalculateBounds();
        data.size=root.size;
        data.local_reference=root.localReferencePoint;
        var renderer=lods[0].renderers[0];
        data.actual_bounds_size=renderer.localBounds.size;
        data.actual_bounds_center=renderer.localBounds.center;
        Physics.SyncTransforms();
        var posts=groups.SelectMany(g=>g.GetComponentsInChildren<BoxCollider>()).Where(c=>c.name=="Timber post").ToArray();
        data.collider_floor_min=100;data.collider_floor_max=-100;
        data.min_post_distance=100;data.tested_capsule_radius=.30f;data.capsule_height=1.8f;
        var route=new[]{new Vector2(-5,0),new Vector2(.33f,0),new Vector2(.74f,.34f),new Vector2(.75f,1.0f),new Vector2(1.5f,1.55f),new Vector2(1.5f,5)};
        for(int i=1;i<route.Length;i++) {
            int steps=Mathf.CeilToInt(Vector2.Distance(route[i-1],route[i])/.025f);
            for(int k=0;k<=steps;k++) {
                var v=Vector2.Lerp(route[i-1],route[i],(float)k/steps);
                var floor=World(v.x,v.y,.3f);
                Need(Physics.Raycast(floor,Vector3.down,out var hit,.35f),"missing walkable bay");
                data.collider_floor_min=Mathf.Min(data.collider_floor_min,hit.point.y);
                data.collider_floor_max=Mathf.Max(data.collider_floor_max,hit.point.y);
                var lo=World(v.x,v.y,.402f);
                var hi=World(v.x,v.y,1.595f);
                Need(!Physics.CheckCapsule(lo,hi,.30f),"capsule struck a post at "+v);
                foreach(var c in posts) data.min_post_distance=Mathf.Min(data.min_post_distance,Vector3.Distance(lo,c.ClosestPoint(lo)));
                data.route_samples++;
            }
        }
        File.WriteAllText(Path.Combine(stage,"r2-unity.json"),JsonUtility.ToJson(data,true));
        if(args.Contains("-nographics")) return;

        var rendererData=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
        var urp=UniversalRenderPipelineAsset.Create(rendererData);
        urp.shadowDistance=40;
        GraphicsSettings.defaultRenderPipeline=urp;
        QualitySettings.renderPipeline=urp;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.70f,.74f,.79f);
        RenderSettings.ambientEquatorColor=new Color(.55f,.54f,.51f);
        RenderSettings.ambientGroundColor=new Color(.38f,.37f,.35f);
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;
        sun.transform.rotation=Quaternion.Euler(44,-33,0);sun.intensity=1.4f;sun.color=new Color(1,.93f,.82f);
        sun.shadows=LightShadows.Soft;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.position=World(0,0,-.015f);
        ground.transform.localScale=Vector3.one*20;
        var groundMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));groundMat.color=new Color(.51f,.49f,.45f);groundMat.SetFloat("_Smoothness",0);
        ground.GetComponent<Renderer>().sharedMaterial=groundMat;
        var cam=new GameObject("Readback camera").AddComponent<Camera>();
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.70f,.75f,.79f);
        cam.nearClipPlane=.08f;cam.farClipPlane=200;cam.allowHDR=false;
        void Shoot(string name,Vector3 eye,Vector3 focus,float span,int level) {
            foreach(var g in groups)g.ForceLOD(level);
            cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(focus-eye);
            cam.orthographic=true;cam.orthographicSize=span/2/1.6f;
            var rt=new RenderTexture(1280,800,24,RenderTextureFormat.ARGB32);
            cam.targetTexture=rt;
            cam.Render();cam.Render();
            RenderTexture.active=rt;
            var t=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();
            File.WriteAllBytes(Path.Combine(stage,name+".png"),t.EncodeToPNG());
            cam.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(t);rt.Release();Object.DestroyImmediate(rt);
        }
        Shoot("Unity_near",World(-10,13,6.4f),World(-.84f,.98f,2.29f),13.0f,0);
        Shoot("Unity_far",World(-21,26,10.5f),World(-.84f,.98f,2.29f),18.2f,2);
    }
}
#endif

using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Editor-only authoring in an isolated Unity project. No game scripts or scene edits.
public static class TianyiPavilionAuthor {
    const string Folder="Assets/Art/Environment/Opening/TianyiPavilionKit/";
    const string Model=Folder+"TianyiPavilion.fbx", Prefab=Folder+"TianyiPavilion.prefab";
    static Vector3 P(int side,float t,float r,float y) {
        switch(side){ case 0:return new Vector3(t,y,-r);case 1:return new Vector3(r,y,t);case 2:return new Vector3(-t,y,r);default:return new Vector3(-r,y,-t);}
    }
    static Material Baked(string kind) {
        foreach(string suffix in new[]{"Color","Normal"}) {
            var imp=(TextureImporter)AssetImporter.GetAtPath(Folder+kind+suffix+".png");
            imp.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            imp.sRGBTexture=suffix!="Normal";imp.wrapMode=TextureWrapMode.Repeat;imp.mipmapEnabled=true;
            imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=4;
            var android=imp.GetPlatformTextureSettings("Android");android.name="Android";android.overridden=true;
            android.maxTextureSize=kind=="EarthPlinth"?1024:512;android.format=TextureImporterFormat.ASTC_6x6;imp.SetPlatformTextureSettings(android);imp.SaveAndReimport();
        }
        string path=Folder+kind+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+kind+"Color.png"));
        m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+kind+"Normal.png"));
        m.SetFloat("_BumpScale",kind=="EarthPlinth"?.93f:kind=="Tile"?.86f:.74f);
        m.SetFloat("_Smoothness",kind=="EarthPlinth"?.13f:kind=="Tile"?.31f:.19f);
        m.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(m);return m;
    }
    static void Box(Transform root,string n,Vector3 center,Vector3 size) {
        var go=new GameObject(n);go.transform.SetParent(root,false);go.transform.localPosition=center;
        var box=go.AddComponent<BoxCollider>();box.size=size;
    }
    static void Collisions(Transform root) {
        Box(root,"石坪",new Vector3(0,-.1f,0),new Vector3(9.22f,.2f,9.22f));
        for(int s=0;s<4;s++){
            bool door=(s%2)==0;int face=s;
            void Wall(string label,float t,float r,float y,float span,float thick,float h){
                Box(root,label,P(face,t,r,y),face%2==0?new Vector3(span,h,thick):new Vector3(thick,h,span));
            }
            if(door)foreach(float t in new[]{-2.87f,2.87f})Wall("门侧石裙",t,4.06f,.54f,2.73f,.28f,1.08f);
            else Wall("窗下石墙",0,4.06f,.54f,8.15f,.28f,1.08f);
            Wall("上梁",0,4.06f,3.45f,8.27f,.26f,.81f);
            foreach(float t in (door?new[]{-4.075f,-1.59f,1.59f}:new[]{-4.075f,-1.415f,1.415f})){
                Wall("承重柱",t,4.075f,1.73f,.34f,.34f,3.46f);
            }
        }
        // A ring around the unobstructed square skylight; one two-sided static physics mesh.
        var pts=new System.Collections.Generic.List<Vector3>();var tris=new System.Collections.Generic.List<int>();
        float[] r={5.38f,3.82f,1.40f}, y={3.81f,4.30f,5.47f};
        foreach(bool bottom in new[]{false,true})for(int j=0;j<3;j++)for(int s=0;s<4;s++)
            pts.Add(P(s,-r[j],r[j],y[j]-(bottom?.15f:0)));
        void Quad(int a,int b,int c,int d){tris.AddRange(new[]{a,b,c,a,c,d});}
        for(int s=0;s<4;s++) {
            int next=(s+1)%4;
            for(int j=0;j<2;j++){
                int a=s+4*j,b=next+4*j,c=b+4,d=a+4;
                Quad(a,d,c,b);Quad(a+12,b+12,c+12,d+12);
            }
            Quad(s,next,next+12,s+12);Quad(s+8,s+20,next+20,next+8);
        }
        string path=Folder+"RoofCollision.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!mesh){mesh=new Mesh{name="RoofCollision"};AssetDatabase.CreateAsset(mesh,path);}
        mesh.Clear();mesh.SetVertices(pts);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var roof=new GameObject("屋面 · 天窗开口");roof.transform.SetParent(root,false);roof.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    public static void Run() {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var wood=Baked("Timber");var tile=Baked("Tile");
        var matMap=new System.Collections.Generic.Dictionary<string,Material>{
            ["Paving"]=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Opening/PhotoArchitecture/Flags.mat"),
            ["Fieldstone"]=Baked("EarthPlinth"),["Timber"]=wood,["Tile"]=tile};
        var imp=(ModelImporter)AssetImporter.GetAtPath(Model);
        imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;imp.generateSecondaryUV=false;
        imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        foreach(var kv in matMap)imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),kv.Key),kv.Value);
        imp.SaveAndReimport();
        var root=new GameObject("天仪阁");var render=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        render.transform.SetParent(root.transform,false);render.name="梁架、墙体与屋面";
        var children=render.GetComponentsInChildren<MeshRenderer>(true);
        // Model names use _LOD0..2. Unity can create an automatic LODGroup on FBX import.
        // Remove the nested controller via an unpacked hierarchy before installing our own.
        PrefabUtility.UnpackPrefabInstance(render,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(var lod in render.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(lod);
        var group=root.AddComponent<LODGroup>();var levels=new LOD[3];
        for(int k=0;k<3;k++) {
            var r=children.Single(x=>x.name.EndsWith("LOD"+k));r.enabled=true;r.gameObject.SetActive(true);
            levels[k]=new LOD(new[]{.42f,.16f,.0025f}[k],new Renderer[]{r});
        }
        group.SetLODs(levels);group.RecalculateBounds();
        var physics=new GameObject("固定碰撞");physics.transform.SetParent(root.transform,false);Collisions(physics.transform);
        AssetDatabase.SaveAssets();
        PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        Object.DestroyImmediate(root);
        Audit(scene);
    }
    static void Audit(Scene scene) {
        var saved=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(saved);
        var group=instance.GetComponent<LODGroup>();
        var log=new StringBuilder();var levels=group.GetLODs();
        if(levels.Length!=3)throw new Exception("Missing LOD");int last=int.MaxValue;
        foreach(var level in levels){
            var r=level.renderers.Single();var m=r.GetComponent<MeshFilter>().sharedMesh;
            int tris=(int)Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i))/3;
            if(tris>=last||r.sharedMaterials.Any(x=>!x)||r.sharedMaterials.Length!=4)throw new Exception("Bad LOD/materials "+m.name);
            last=tris;log.AppendLine(m.name+" "+tris+" tris / "+r.bounds.size.ToString("F3")+" / "+string.Join(" + ",r.sharedMaterials.Select(x=>x.name)));
        }
        Physics.SyncTransforms();
        if(Physics.Raycast(new Vector3(0,.75f,0),Vector3.up,8))throw new Exception("Blocked skylight");
        foreach(float x in new[]{-1.12f,0f,1.12f})
            if(Physics.CapsuleCast(new Vector3(x,.4f,-5.55f),new Vector3(x,1.45f,-5.55f),.24f,Vector3.forward,11.1f))
                throw new Exception("Blocked axial passage at "+x);
        foreach(float z in new[]{-4.25f,-3f,0f,3f,4.25f})if(!Physics.Raycast(new Vector3(0,1,z),Vector3.down,out var hit,1.05f)||Mathf.Abs(hit.point.y)>.005f)throw new Exception("Broken floor");
        if(!Physics.Raycast(new Vector3(0,1.0f,0),Vector3.right,4.3f))throw new Exception("Missing wall");
        if(!Physics.Raycast(new Vector3(2.7f,2.2f,0),Vector3.up,5))throw new Exception("Missing roof");
        var deps=AssetDatabase.GetDependencies(Prefab,true);foreach(var dep in deps)if(!AssetDatabase.LoadMainAssetAtPath(dep))throw new Exception("Missing dependency "+dep);
        log.AppendLine("Measured capsule sweep centre and x=±1.12 m, 0.24 m radius, z -5.55 to +5.55 passed. Zenith ray clear; side wall and roof blocked; 5 floor samples at y=0.");
        log.AppendLine("Fixed colliders "+instance.GetComponentsInChildren<Collider>(true).Length+", asset dependencies "+deps.Length);
        Directory.CreateDirectory("PavilionReview");File.WriteAllText("PavilionReview/unity-checks.txt",log.ToString());
        var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.35f;
        light.transform.rotation=Quaternion.Euler(48,-24,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.59f,.62f,.64f);
        var cam=new GameObject("Review").AddComponent<Camera>();cam.enabled=false;cam.clearFlags=CameraClearFlags.SolidColor;
        cam.backgroundColor=new Color(.59f,.66f,.69f);cam.nearClipPlane=.025f;cam.farClipPlane=100;cam.fieldOfView=49;
        cam.stereoTargetEye=StereoTargetEyeMask.None;cam.Additional().renderPostProcessing=false;
        int w=1400,h=900;var rt=new RenderTexture(w,h,24);rt.Create();cam.targetTexture=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);group.ForceLOD(0);
        void Shot(string name,Vector3 p,Vector3 looking,float lens){
            cam.fieldOfView=lens;cam.transform.position=p;cam.transform.LookAt(looking);cam.Render();cam.Render();RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes("PavilionReview/"+name+".jpg",tex.EncodeToJPG(91));
        }
        Shot("00_阁外",new Vector3(8.85f,6.8f,-12.6f),new Vector3(0,2.2f,0),49);
        Shot("01_阁内",new Vector3(-2.55f,1.40f,-3.24f),new Vector3(.35f,2.2f,2.35f),79);
        Shot("02_天窗",new Vector3(8f,14.5f,-10.2f),new Vector3(0,1.45f,0),50);
        cam.targetTexture=null;RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
        Debug.Log("PAVILION_ASSET_PASS "+log);
    }
    static UniversalAdditionalCameraData Additional(this Camera cam)=>cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
}

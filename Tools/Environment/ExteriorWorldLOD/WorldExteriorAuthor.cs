using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.SceneArt;
using Object=UnityEngine.Object;

public static class WorldExteriorAuthor
{
    const string Prefab="Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab";
    const string Source="Assets/Scene/Chapter01_Awakening.unity";
    const string Art="Assets/Art/Environment/Opening/ExteriorLOD/";
    const string Log="D:/UnityProjects/NarutoVR/Logs/exterior-world-lod-20261109/integration";
    static Scene scene;
    static readonly List<string> proof=new List<string>();
    static readonly List<ExteriorPolishAuthor.Change> changes=new List<ExteriorPolishAuthor.Change>();
    static T[] All<T>() where T:Component =>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    static string Sha(string p){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    public static void Review()=>ReviewCore(false);
    public static void ReviewAndAccept()=>ReviewCore(true);
    static void ReviewCore(bool runAcceptance)
    {
        if(!Application.dataPath.StartsWith("D:/Temp/NarutoVR-Exterior-20261108",StringComparison.OrdinalIgnoreCase))throw new Exception("Isolated project only");
        try
        {
            scene=EditorSceneManager.OpenScene("Assets/Scene/QA_WorldExteriorMerged.unity",OpenSceneMode.Single);
            Photos();DetailPhotos(scene.GetRootGameObjects().Single(g=>g.name=="首章外景细化").transform);
            WorldExteriorBudget.Write(scene,Log);
            File.WriteAllText(Log+"/fresh-process-visual.txt",DateTimeOffset.Now.ToString("O")+"\nRead-only fresh-process scene load; saved mesh assets reloaded independently of authoring GPU caches.\nScene="+Sha("Assets/Scene/QA_WorldExteriorMerged.unity")+"\nPrefab="+Sha(Prefab));
            if(runAcceptance)WorldExteriorAcceptance.Run();else EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Log+"/review-FAIL.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Run()
    {
        Directory.CreateDirectory(Log);proof.Clear();changes.Clear();
        if(File.Exists(Log+"/FAIL.txt"))File.Move(Log+"/FAIL.txt",Log+"/previous-failure-"+DateTime.Now.ToString("HHmmssfff")+".txt");
        try
        {
            if(!Application.dataPath.StartsWith("D:/Temp/NarutoVR-Exterior-20261108",StringComparison.OrdinalIgnoreCase))throw new Exception("Isolated project only");
            File.Copy(Source,Log+"/source-scene.unity",true);
            scene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);string assetName=asset.name;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.transform,out string guid,out long originalRootID);
            var root=scene.GetRootGameObjects().Single(g=>g.name=="首章外景细化");
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var land=root.GetComponentsInChildren<MeshFilter>(true).Single(f=>AssetDatabase.GetAssetPath(f.sharedMesh).EndsWith("/ValleyLandscape.asset"));
            var probe=new GameObject("Temporary exact-ground sampler");SceneManager.MoveGameObjectToScene(probe,scene);
            var surfaces=new List<MeshCollider>();
            foreach(var terrain in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f==land||AssetDatabase.GetAssetPath(f.sharedMesh).Contains("/MountainPolish/Ridge_")))
            {
                var patch=new GameObject("Actual surface "+terrain.name);patch.transform.SetParent(probe.transform,false);
                patch.transform.SetPositionAndRotation(terrain.transform.position,terrain.transform.rotation);patch.transform.localScale=terrain.transform.lossyScale;
                var collider=patch.AddComponent<MeshCollider>();collider.sharedMesh=terrain.sharedMesh;surfaces.Add(collider);
            }
            Physics.SyncTransforms();
            float Ground(float x,float z)
            {
                float highest=float.NegativeInfinity;var ray=new Ray(new Vector3(x,300,z),Vector3.down);
                foreach(var surface in surfaces)if(surface.Raycast(ray,out var hit,600))highest=Mathf.Max(highest,hit.point.y);
                if(float.IsNegativeInfinity(highest))throw new Exception("Ground miss at "+x+","+z);
                return highest;
            }
            foreach(var t in root.transform.Cast<Transform>().ToArray())
                if(t.name=="西窗外 · 天工偏坊"||t.name=="东窗外 · 山间工坊"||t.name=="测候台远景 · 校器坊"||t.name=="主工坊 · 薄灰瓦覆顶"||t.name=="工坊砌石台基")t.gameObject.SetActive(false);
            ArchitectureBuilder.Build(root.transform,scene,Ground);
            NatureBuilder.Build(root.transform,scene,Ground);
            NatureBuilder.ApplyExistingLandscapeLOD(root.transform);
            ConnectivityBuilder.Build(root.transform,scene,Ground);
            SurfaceMaterials(root.transform);
            HallCollisions(root.transform);
            // Re-evaluate broad-footed source props against the actual triangulated surface.
            int rockIndex=0;
            foreach(var t in root.transform.Cast<Transform>().ToArray())
            {
                string kind=t.name=="层岩 · 岸壁"?"StrataRock":t.name.Contains("山松")||t.name=="门庭侧松"?"MountainPine":t.name=="量水道尽头 · 残规门庭"?"PortalGate":null;
                if(kind==null)continue;
                if(kind=="StrataRock")
                {
                    // Clear the newly connected side routes and the northern workshop approach.
                    if(rockIndex==6)t.position=new Vector3(-28,0,34);
                    if(rockIndex==7)t.position=new Vector3(29,0,43);
                    rockIndex++;
                }
                if(kind!="PortalGate")
                {
                    var rs=t.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
                    float bottom=float.PositiveInfinity;
                    for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)bottom=Mathf.Min(bottom,Ground(b.center.x+x*b.extents.x*.75f,b.center.z+z*b.extents.z*.75f));
                    t.position+=Vector3.up*(bottom-(kind=="StrataRock"?.26f:.12f)-b.min.y);
                }
                ModelLOD(t,kind);
            }
            AddMovingRing(root.transform);
            AddGate(root.transform);
            OpenSidePassages(root.transform);
            TuneLOD(root.transform);
            Object.DestroyImmediate(probe);
            root.name=assetName;
            var saved=PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(saved.transform,out string finalGuid,out long finalRootID);
            if(finalGuid!=guid||finalRootID!=originalRootID)throw new Exception("Existing prefab root identity changed");
            root.name="首章外景细化";
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,"Assets/Scene/QA_WorldExteriorLOD.unity");
            File.WriteAllText(Log+"/changes.json",JsonUtility.ToJson(new ExteriorPolishAuthor.Report{source=Source,sourceSHA256=Sha(Source),changes=changes.ToArray()},true));
            proof.Add("Original scene bytes unchanged="+(Sha(Source)==Sha(Log+"/source-scene.unity")));
            proof.Add("Prefab root preserved="+guid+":"+originalRootID);
            Audit(root);
            Photos();
            DetailPhotos(root.transform);
            WorldExteriorBudget.Write(scene,Log);
            File.WriteAllLines(Log+"/proof.txt",proof);
            File.WriteAllText(Log+"/PASS.txt","Authoring completed, pending independent scene merge, runtime and visual acceptance.");
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Log+"/FAIL.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Disable(MeshRenderer renderer)
    {
        var method=typeof(ExteriorPolishAuthor).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic);
        var change=(ExteriorPolishAuthor.Change)method.Invoke(null,new object[]{renderer,"m_Enabled",renderer.enabled?"1":"0","0"});
        changes.Add(change);renderer.enabled=false;
        if(PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
    }
    static void OpenSidePassages(Transform parent)
    {
        // Replace only this simple continuous stone parapet with physical, visible openings.
        // The original floor collider and all progression triggers stay in place.
        var old=All<MeshRenderer>().Single(r=>PathOf(r.transform)=="第一章 · 初醒校验室/关外石坪/门廊 · 连续石面");
        Disable(old);
        var capture=typeof(ExteriorPolishAuthor).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic);
        foreach(var collider in All<BoxCollider>().Where(c=>PathOf(c.transform)=="第一章 · 初醒校验室/关外石坪/左收边碰撞"||PathOf(c.transform)=="第一章 · 初醒校验室/关外石坪/右收边碰撞"))
        {
            if(!collider.enabled)throw new Exception("Unexpected already-disabled original parapet");
            changes.Add((ExteriorPolishAuthor.Change)capture.Invoke(null,new object[]{collider,"m_Enabled","1","0"}));
            collider.enabled=false;
            if(PrefabUtility.IsPartOfPrefabInstance(collider))PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }
        var material=old.sharedMaterial;
        var group=new GameObject("关外石坪 · 两侧支路开口");group.transform.SetParent(parent,false);
        var filters=new List<MeshFilter>();
        void Part(string name,Vector3 center,Vector3 size,bool collision)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(group.transform,false);
            go.transform.position=center;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=material;go.isStatic=true;
            if(!collision)Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            filters.Add(go.GetComponent<MeshFilter>());
        }
        Part("连续石面",new Vector3(0,-.35f,36.65f),new Vector3(4.64f,.64f,5.5f),false);
        foreach(float x in new[]{-2.18f,2.18f})
        {
            Part("开口前分段护栏",new Vector3(x,.4f,(34+35.95f)*.5f),new Vector3(.28f,.94f,35.95f-34),true);
            Part("开口后分段护栏",new Vector3(x,.4f,(38.45f+39.4f)*.5f),new Vector3(.28f,.94f,39.4f-38.45f),true);
        }
        var mesh=new Mesh{name="StonePorchSideOpenings"};mesh.CombineMeshes(filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=group.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray());
        string path=Art+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
        group.AddComponent<MeshFilter>().sharedMesh=mesh;group.AddComponent<MeshRenderer>().sharedMaterial=material;group.isStatic=true;
        foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
        proof.Add("Two porch parapets replaced by four solid sections; matching visible 2.5m openings z35.95..38.45; original floor and progression unchanged.");
    }
    static void ModelLOD(Transform target,string kind)
    {
        if(target.GetComponent<LODGroup>())return;
        var rs=target.GetComponentsInChildren<Renderer>();var levels=new LOD[3];levels[0]=new LOD(.24f,rs);
        var reference=rs[0].bounds;foreach(var r in rs.Skip(1))reference.Encapsulate(r.bounds);
        for(int level=1;level<=2;level++)
        {
            int sourceLevel=level;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+kind+"_LOD"+sourceLevel+".fbx");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);instance.transform.SetParent(target,false);instance.name="简化LOD"+level;
            var renderers=instance.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);
            if(Vector3.Distance(reference.center,b.center)>.7f||Vector3.Distance(reference.size,b.size)>1.4f)throw new Exception("LOD geometry alignment differs: "+kind+" "+reference+" vs "+b);
            foreach(var r in renderers){r.sharedMaterials=rs[0].sharedMaterials;r.shadowCastingMode=level==1?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=level==1;}
            levels[level]=new LOD(level==1?.09f:.004f,renderers);
        }
        var group=target.gameObject.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;group.SetLODs(levels);group.RecalculateBounds();
    }
    static void AddMovingRing(Transform parent)
    {
        var old=All<MeshRenderer>().Single(r=>r.name=="断轴天仪 · 铜仪"&&r.enabled);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"ArmillarySeparated.fbx");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);go.name="测候天仪 · 完整环独立转轴";
        go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);go.transform.localScale=old.transform.lossyScale;
        var rs=go.GetComponentsInChildren<MeshRenderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
        if(Vector3.Distance(b.center,old.bounds.center)>.025f||Vector3.Distance(b.size,old.bounds.size)>.025f)throw new Exception("Armillary split bounds mismatch: "+b+" vs "+old.bounds);
        foreach(var r in rs){r.sharedMaterial=old.sharedMaterial;r.gameObject.isStatic=false;}
        var ring=go.GetComponentsInChildren<Transform>().Single(t=>t.name=="IntactCircle");
        var motion=go.AddComponent<ArmillaryRingMotion>();var serialized=new SerializedObject(motion);
        serialized.FindProperty("intactRing").objectReferenceValue=ring;
        serialized.FindProperty("localAxis").vector3Value=new Vector3(-.7790773f,-.4742685f,.4100099f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Disable(old);proof.Add("Armillary bounds="+b+"; rotating ring="+ring.localPosition+"; broken frame stationary; shared original baked atlas.");
    }
    static void AddGate(Transform parent)
    {
        var portal=parent.Cast<Transform>().Single(t=>t.name=="量水道尽头 · 残规门庭");
        var timber=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/WorkshopGateTimber.mat");
        var bronze=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/旧铜.mat");
        var doorParts=new List<MeshFilter>();
        foreach(float side in new[]{-1f,1f})
        {
            var hinge=new GameObject(side<0?"左门扇 · 开启":"右门扇 · 开启");hinge.transform.SetParent(portal,false);
            hinge.transform.localPosition=new Vector3(side*.86f,0,0);hinge.transform.localRotation=Quaternion.Euler(0,-side*82,0);
            void Piece(string name,Vector3 p,Vector3 size,Material material)
            {
                var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(hinge.transform,false);part.transform.localPosition=p;part.transform.localScale=size;
                part.GetComponent<Renderer>().sharedMaterial=material;part.isStatic=true;doorParts.Add(part.GetComponent<MeshFilter>());
                if(name=="哑铜门带")Object.DestroyImmediate(part.GetComponent<BoxCollider>());
            }
            Piece("榫木门板",new Vector3(-side*.405f,1.24f,0),new Vector3(.81f,2.40f,.12f),timber);
            foreach(float y in new[]{.33f,2.18f})Piece("哑铜门带",new Vector3(-side*.405f,y,-.072f),new Vector3(.82f,.085f,.026f),bronze);
        }
        // Merge per material; doors remain an openly traversable static frame, not six draws per leaf.
        foreach(var material in new[]{timber,bronze})
        {
            var filters=doorParts.Where(f=>f&&f.GetComponent<Renderer>().sharedMaterial==material).ToArray();
            var mesh=new Mesh{name="PortalOpenLeaves_"+material.name};mesh.CombineMeshes(filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=portal.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray());
            string path=Art+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            var visual=new GameObject(mesh.name);visual.transform.SetParent(portal,false);visual.AddComponent<MeshFilter>().sharedMesh=mesh;visual.AddComponent<MeshRenderer>().sharedMaterial=material;visual.isStatic=true;
            foreach(var f in filters){Object.DestroyImmediate(f.GetComponent<Renderer>());Object.DestroyImmediate(f);}
        }
        var go=new GameObject("传送门 · 场景加载触发区");go.transform.SetParent(portal,false);go.transform.localPosition=new Vector3(0,1.25f,0);
        var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(1.75f,2.5f,.70f);
        go.AddComponent<SceneTravelGate>();
        foreach(var name in new[]{"ArrivalAnchor","ExitAnchor"})
        {var anchor=new GameObject(name);anchor.transform.SetParent(portal,false);anchor.transform.localPosition=new Vector3(0,.05f,name=="ArrivalAnchor"?1.2f:-1.2f);}
        proof.Add("Portal load disabled and destination empty pending a real target scene; tracked-head entry region 1.75x2.5x.7m.");
    }
    static void HallCollisions(Transform parent)
    {
        foreach(var hall in parent.Cast<Transform>().Where(t=>t.name.StartsWith("工官建筑")&&t.Find("WalkwayConnection")))
        {
            var connection=hall.Find("WalkwayConnection");
            float width=hall.name.Contains("校矩")?6.7f:hall.name.Contains("规尺")?7.5f:8f;
            void Box(string name,Vector3 center,Vector3 size)
            {var child=new GameObject(name);child.transform.SetParent(hall,false);child.transform.localPosition=center;child.AddComponent<BoxCollider>().size=size;}
            Box("工坊地面碰撞",new Vector3(0,-.14f,.3f),new Vector3(width,.28f,5.6f));
            float wallWidth=width-.7f;
            Box("坊后石墙碰撞",new Vector3(0,1.23f,-2.02f),new Vector3(wallWidth,2.46f,.22f));
            foreach(float side in new[]{-1f,1f})
            {
                Box("坊侧石墙碰撞",new Vector3(side*(wallWidth*.5f-.1f),.57f,0),new Vector3(.22f,1.14f,4.2f));
                Box("坊侧上木墙碰撞",new Vector3(side*(wallWidth*.5f-.1f),2,0),new Vector3(.17f,1,4.2f));
                foreach(float z in new[]{-2.1f,0,2.1f})Box("坊侧木柱碰撞",new Vector3(side*(wallWidth*.5f-.04f),1.43f,z),new Vector3(.26f,2.86f,.28f));
                Box("坊前石墙碰撞",new Vector3(side*(wallWidth*.25f+.525f),.56f,2.1f),new Vector3((wallWidth-2.1f)*.5f,1.12f,.22f));
                Box("坊前门柱碰撞",new Vector3(side*1.2f,1.4f,2.1f),new Vector3(.27f,2.8f,.30f));
            }
            proof.Add("Workshop walkable floor: "+hall.name+" connection="+connection.position+" width="+width*hall.lossyScale.x);
        }
    }
    static void TuneLOD(Transform parent)
    {
        // User prioritizes standalone performance: reserve the most detailed mesh for close viewing.
        // LODGroup.size affects selection, not renderer bounds, collision, or world geometry.
        foreach(var group in parent.GetComponentsInChildren<LODGroup>(true))
        {
            var levels=group.GetLODs();if(levels.Length!=3)continue;
            string path=PathOf(group.transform);float scale=Mathf.Max(Mathf.Abs(group.transform.lossyScale.x),Mathf.Abs(group.transform.lossyScale.y),Mathf.Abs(group.transform.lossyScale.z));
            float oldSize=group.size*scale,maxWorld=5f,near=.75f,middle=.20f,far=.001f;
            if(path.Contains("/MountainPolish/")||path.Contains("/ValleyLandscape"))
            {maxWorld=path.Contains("ValleyLandscape")?16f:12f;near=.90f;middle=.10f;far=.0001f;}
            else if(path.Contains("自然区块")){maxWorld=6f;near=.75f;middle=.22f;far=.001f;}
            else if(path.Contains("A07 · 校器工作台")){maxWorld=1.8f;near=.34f;middle=.14f;far=.003f;}
            else if(path.Contains("工官建筑")){maxWorld=6f;near=.75f;middle=.20f;far=.001f;}
            else if(path.Contains("支路")||path.Contains("横接")||path.Contains("台基")||path.Contains("石基")||path.Contains("岸基"))
            {maxWorld=6f;near=.60f;middle=.10f;far=.0001f;}
            group.size=Mathf.Min(oldSize,maxWorld)/Mathf.Max(scale,.0001f);
            levels[0].screenRelativeTransitionHeight=near;levels[1].screenRelativeTransitionHeight=middle;levels[2].screenRelativeTransitionHeight=far;
            group.SetLODs(levels);group.fadeMode=LODFadeMode.None;group.animateCrossFading=false;
            float effective=group.size*scale;
            proof.Add("PERF_LOD|"+path+"|selectionWorldSize="+oldSize+"->"+effective+"|screen="+near+","+middle+","+far+"|90deg_lodBias1_distance_to_ref="+(effective/(2*near))+","+(effective/(2*middle))+"|no_crossfade|geometry_and_collision_unchanged");
        }
    }
    static void SurfaceMaterials(Transform parent)
    {
        const string maps="Assets/Art/Environment/Opening/SurfaceMaterialsLOD/";
        var rock=AssetDatabase.LoadAssetAtPath<Material>(maps+"M01_QingHuiStrata.mat");
        var moss=AssetDatabase.LoadAssetAtPath<Material>(maps+"M02_MossScree.mat");
        if(!rock||!moss)throw new Exception("Expected reviewed rock/scree material delivery");
        Material Variant(Material original,string name,float scale,bool normal)
        {
            string path=Art+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(original);AssetDatabase.CreateAsset(material,path);}else EditorUtility.CopySerialized(original,material);
            material.name=name;material.SetTextureScale("_BaseMap",Vector2.one*scale);material.SetFloat("_SpecularHighlights",0);material.SetFloat("_EnvironmentReflections",0);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if(!normal){material.DisableKeyword("_NORMALMAP");material.SetTexture("_BumpMap",null);}
            material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
        }
        var ridge=Variant(rock,"RidgeStrata",11.11f,true);var distant=Variant(rock,"DistantStrata",7f,false);
        var valley=Variant(moss,"ValleyMossScree",4.1667f,true);var nature=Variant(rock,"NatureStrata",1.7857f,true);
        var terrainShader=Shader.Find("NarutoVR/Exterior Terrain");if(!terrainShader)throw new Exception("Exterior terrain shader missing");
        foreach(var terrain in new[]{ridge,distant,valley})
        {
            terrain.shader=terrainShader;terrain.shaderKeywords=new string[0];
            terrain.SetColor("_RockTint",terrain==valley?new Color(.67f,.69f,.61f):new Color(.64f,.70f,.73f));
            terrain.SetColor("_MossTint",new Color(.36f,.43f,.29f));
            terrain.SetFloat("_TextureMetres",terrain==valley?2.5f:4.5f);
            terrain.SetFloat("_MacroMetres",terrain==valley?11f:20f);
            terrain.SetFloat("_MossAmount",terrain==valley?.85f:.75f);
            terrain.SetFloat("_MacroContrast",.35f);EditorUtility.SetDirty(terrain);
        }
        foreach(var r in parent.GetComponentsInChildren<MeshRenderer>(true))
        {
            var path=PathOf(r.transform);
            if(path.Contains("/ValleyLandscape")){r.sharedMaterial=valley;r.shadowCastingMode=ShadowCastingMode.Off;}
            else if(path.Contains("/MountainPolish/")){r.sharedMaterial=path.Contains("远")||path.Contains("回望")?distant:ridge;r.shadowCastingMode=ShadowCastingMode.Off;}
            else if(r.sharedMaterials.Length==1&&r.sharedMaterial&&r.sharedMaterial.name.Contains("NatureSlate"))r.sharedMaterial=nature;
        }
    }
    static void Audit(GameObject root)
    {
        foreach(var c in root.GetComponentsInChildren<Transform>(true))
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(c.gameObject)>0)throw new Exception("Missing script "+PathOf(c));
        foreach(var g in root.GetComponentsInChildren<LODGroup>(true))
        {
            var counts=g.GetLODs().Select(l=>l.renderers.Sum(r=>{var m=r.GetComponent<MeshFilter>();return m&&m.sharedMesh?m.sharedMesh.triangles.Length/3:0;})).ToArray();
            if(counts.Length!=3||!(counts[0]>counts[1]&&counts[1]>counts[2]))throw new Exception("LOD does not simplify "+PathOf(g.transform)+": "+string.Join(",",counts));
            proof.Add("LOD|"+PathOf(g.transform)+"|"+string.Join(",",counts));
        }
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterials.Any(m=>!m))throw new Exception("Missing material "+r.name);
        proof.Add("LODGroupCount="+root.GetComponentsInChildren<LODGroup>(true).Length);
        proof.Add("EnabledAudioSources="+All<AudioSource>().Count(a=>a.enabled&&a.gameObject.activeInHierarchy));
    }
    static void Photos()
    {
        var type=typeof(ExteriorPolishAuthor);const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        type.GetField("Out",flags).SetValue(null,Log);type.GetField("work",flags).SetValue(null,scene);
        type.GetMethod("Photos",flags).Invoke(null,new object[]{"world",true});
        int quality=QualitySettings.GetQualityLevel();
        try{QualitySettings.SetQualityLevel(0,true);type.GetMethod("Photos",flags).Invoke(null,new object[]{"mobile-settings",false});}
        finally{QualitySettings.SetQualityLevel(quality,true);}
        var stats=(List<ExteriorPolishAuthor.CameraStat>)type.GetField("cameraStats",flags).GetValue(null);
        File.WriteAllText(Log+"/camera-statistics.json",JsonUtility.ToJson(new ExteriorPolishAuthor.CameraStats{views=stats.ToArray(),caution="Static frustum whole-mesh estimates only; includes all enabled LOD renderers in hierarchy; not actual submitted GPU geometry/draws or headset timings."},true));
    }
    static void DetailPhotos(Transform root)
    {
        var go=new GameObject("Temporary exterior detail camera");SceneManager.MoveGameObjectToScene(go,scene);
        var camera=go.AddComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.fieldOfView=72;camera.nearClipPlane=.03f;camera.farClipPlane=320;
        camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=false;camera.allowMSAA=false;
        var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=true;
        data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
        var old=RenderTexture.active;var rt=new RenderTexture(1440,900,24);rt.Create();var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;
            void Shot(string name,Vector3 position,Vector3 target)
            {camera.transform.position=position;camera.transform.LookAt(target);camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(Log+"/"+name+".png",image.EncodeToPNG());}
            Shot("world-side-passages",new Vector3(0,1.65f,35.3f),new Vector3(0,.8f,38.5f));
            var hall=root.Cast<Transform>().Single(t=>t.name=="工官建筑 · 西窗外 · 校矩作坊");
            Shot("world-workbench",hall.TransformPoint(new Vector3(0,1.32f,2.7f)),hall.TransformPoint(new Vector3(0,.65f,.3f)));
            Shot("world-armillary",new Vector3(4.7f,2.2f,61.5f),new Vector3(2.67f,2.3f,64.65f));
            Shot("world-pine-root-contact",new Vector3(15f,1.4f,77f),new Vector3(18f,-.55f,81f));
        }
        finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);}
    }
}

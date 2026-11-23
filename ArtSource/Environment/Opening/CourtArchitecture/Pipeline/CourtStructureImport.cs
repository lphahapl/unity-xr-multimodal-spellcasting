using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class CourtStructureImport {
    const string Root="Assets/Art/Environment/Opening/CourtArchitecture/";
    public static void Import(){
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        string name=Environment.GetEnvironmentVariable("COURT_STRUCTURE");
        if(string.IsNullOrWhiteSpace(name))throw new Exception("Specify COURT_STRUCTURE");
        string folder=Root+name+"/";string fbx=folder+name+".fbx";
        var map=new System.Collections.Generic.Dictionary<string,Material>();
        for(int tier=0;tier<3;tier++){
            string kind=name+"_Tier"+tier;
            string albedoPath=folder+kind+"_BaseColor.png",normalPath=folder+kind+"_Normal.png",maskPath=folder+kind+"_Mask.png";
            foreach(string path in new[]{albedoPath,normalPath,maskPath}){
                var ti=(TextureImporter)AssetImporter.GetAtPath(path);
                if(ti==null)throw new Exception("Missing baked texture "+path);
                ti.textureType=path==normalPath?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=path==albedoPath; if(path==maskPath){ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=false;}
                ti.mipmapEnabled=true;ti.wrapMode=TextureWrapMode.Clamp;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=4;
                var android=ti.GetPlatformTextureSettings("Android");android.name="Android";android.overridden=true;
                android.maxTextureSize=new[]{2048,1024,512}[tier];android.format=TextureImporterFormat.ASTC_6x6;ti.SetPlatformTextureSettings(android);ti.SaveAndReimport();
            }
            string mp=folder+kind+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,mp);}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));mat.SetFloat("_Metallic",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_BumpScale",.89f);mat.SetFloat("_Smoothness",.9f);mat.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(mat);
            map.Add(kind+"_Baked",mat);
        }
        var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
        importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
        importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
        importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        foreach(var kv in map)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),kv.Key),kv.Value);
        importer.SaveAndReimport();
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        instance.name=name;
        var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
        if(renderers.Length!=3)throw new Exception("Expected exactly 3 LOD renderers; got "+renderers.Length);
        var group=instance.AddComponent<LODGroup>();var lods=new LOD[3];
        for(int k=0;k<3;k++){var r=renderers.Single(x=>x.name.EndsWith("Tier"+k));r.enabled=true;r.gameObject.SetActive(true);
           if(r.sharedMaterials.Length!=1||r.sharedMaterial!=map[name+"_Tier"+k+"_Baked"])throw new Exception("Missing material assignment "+r.name);
           lods[k]=new LOD(new[]{.42f,.14f,.0035f}[k],new Renderer[]{r});}
        group.SetLODs(lods);group.RecalculateBounds();
        if(name=="CourtGatehouse")ApplyGateColliders(instance,folder);
        AssetDatabase.SaveAssets();
        PrefabUtility.SaveAsPrefabAsset(instance,folder+name+".prefab");Object.DestroyImmediate(instance);
        var read=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder+name+".prefab"));
        int last=int.MaxValue;var report=new System.Text.StringBuilder();
        foreach(var l in read.GetComponent<LODGroup>().GetLODs()){
            var r=l.renderers.Single();var m=r.GetComponent<MeshFilter>().sharedMesh;int count=(int)m.GetIndexCount(0)/3;
            if(count<=0||count>=last)throw new Exception("Unexpected LOD ratio");last=count;
            report.AppendLine(m.name+" triangles="+count+" bounds="+r.bounds+" material="+r.sharedMaterial.name);
        }
        Directory.CreateDirectory("CourtStructureCheck");
        if(name=="CourtGatehouse"){
            // Validate the architectural envelope, not just simplified physics. This temporary
            // mesh collider is never saved into the prefab.
            var r=read.GetComponent<LODGroup>().GetLODs()[0].renderers[0];
            var probe=r.gameObject.AddComponent<MeshCollider>();probe.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;
            float z=r.bounds.extents.z+1.8f;Physics.SyncTransforms();
            foreach(float x in Enumerable.Range(0,13).Select(i=>-.90f+i*.15f))foreach(float side in new[]{-1f,1f}){
                var p=new Vector3(x,0,z*side);var dir=Vector3.forward*(-side);
                if(Physics.CapsuleCast(p+Vector3.up*.34f,p+Vector3.up*2.36f,.30f,dir,out var hit,z*2,~0,QueryTriggerInteraction.Ignore))throw new Exception("Visual/physics door blocked "+x+"/"+side+"/"+hit.collider.name+" at "+hit.point);
            }
            foreach(float x in new[]{-.90f,0,.90f})foreach(float side in new[]{-1f,1f}){
                var player=new GameObject("Physical capsule walk");player.transform.position=new Vector3(x,0,z*side);
                var controller=player.AddComponent<CharacterController>();controller.height=2.62f;controller.radius=.30f;
                controller.center=new Vector3(0,1.35f,0);controller.stepOffset=.08f;controller.skinWidth=.025f;
                controller.minMoveDistance=0;Physics.SyncTransforms();
                for(int step=0;step<80;step++)controller.Move(Vector3.forward*(-side*z*2/80));
                if(Mathf.Abs(player.transform.position.z+side*z)>.06f||Mathf.Abs(player.transform.position.x-x)>.035f)
                   throw new Exception("Physical CharacterController pass failed "+player.transform.position);
                Object.DestroyImmediate(player);
            }
            report.AppendLine("Fixed colliders + detailed render mesh: actual 0.60m-diameter CharacterController reached exit for six traversals.");
            report.AppendLine("Visual LOD0 + fixed colliders: 2.4m swept width, 2.66m player envelope below lintel, both directions pass.");
            Object.DestroyImmediate(probe);
        }
        File.WriteAllText("CourtStructureCheck/"+name+".txt",report.ToString());
        Debug.Log("COURT_STRUCTURE_PASS "+name+" "+report);
    }
    [Serializable] public class BoxItem{public string label;public Vector3 position,size;}
    [Serializable] public class BoxList{public BoxItem[] boxes;}
    static void ApplyGateColliders(GameObject root,string folder){
        string path=Environment.GetEnvironmentVariable("COURT_GATE_COLLIDERS");
        if(string.IsNullOrEmpty(path)||!File.Exists(path))throw new Exception("Missing collider-fit data");
        var data=JsonUtility.FromJson<BoxList>(File.ReadAllText(path));foreach(var item in data.boxes){
            var go=new GameObject(item.label);go.transform.SetParent(root.transform,false);
            var box=go.AddComponent<BoxCollider>();box.center=item.position;box.size=item.size;
        }
    }
}

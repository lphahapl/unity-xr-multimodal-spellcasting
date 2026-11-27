using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.WorkshopTenonReview;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class WorkshopTenonVisualSupplements
{
    const string Root="Assets/Review/WorkshopMasterTenonR01";
    const string Output="D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-r01-20261125/unity";
    const string Delivery="D:/UnityProjects/NarutoVR/ArtSource/Props/WorkshopMasterTenon/R01/UnityReview";
    const string Key="WorkshopTenonR01.VisualSupplements";
    static WorkshopTenonLabController lab;
    static GameObject tickObject;
    static Camera camera;
    static RenderTexture target;
    static Texture2D texture;
    static Light keyLight;
    static BoxCollider tenonEnvelope;
    static GameObject[] pins;
    static float closedPenetration,openedPenetration;
    static int frame;
    static WorkshopTenonVisualSupplements()=>EditorApplication.update+=Boot;
    public static void Run()
    {
        if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-T01Seal-20261124/Assets")throw new Exception("Exclusive review project only");
        EditorSceneManager.OpenScene(Root+"/WorkshopMasterTenonR01_Lab.unity");
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Boot()
    {
        if(!Application.isPlaying || EditorApplication.isCompiling || !SessionState.GetBool(Key,false))return;
        SessionState.SetBool(Key,false);
        try {
            lab=Object.FindFirstObjectByType<WorkshopTenonLabController>();lab.keyboardEnabled=false;lab.showPanel=false;
            camera=Camera.main;camera.enabled=false;camera.aspect=4f/3;camera.allowHDR=false;camera.allowMSAA=false;
            target=new RenderTexture(1280,960,24);target.Create();texture=new Texture2D(1280,960,TextureFormat.RGB24,false);
            NeutralLights();keyLight=GameObject.Find("Review key light").GetComponent<Light>();
            if(!lab.Take())throw new Exception("Cannot pose frozen tenon");lab.Inspect();lab.handScaleReference.gameObject.SetActive(true);
            tickObject=new GameObject("Visual supplements observer");tickObject.AddComponent<WorkshopTenonReviewTick>();WorkshopTenonReviewTick.Tick=Tick;
            Time.captureFramerate=30;QualitySettings.vSyncCount=0;
            var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"/Model/Textures/MasterTenon_BaseColor.png");
            var normal=(TextureImporter)AssetImporter.GetAtPath(Root+"/Model/Textures/MasterTenon_Normal.png");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/MasterTenon_R01.mat");
            File.WriteAllText(Output+"/neutral-lighting-settings.txt",$"BaseColor sRGB={importer.sRGBTexture}, Normal sRGB={normal.sRGBTexture}, Normal type={normal.textureType}, flipGreen={normal.flipGreenChannel}\nBaseColor tint={material.GetColor("_BaseColor")}\nBump scale={material.GetFloat("_BumpScale")}, metallic={material.GetFloat("_Metallic")}, smoothness={material.GetFloat("_Smoothness")}\nNormal keyword={material.IsKeywordEnabled("_NORMALMAP")}, Metallic keyword={material.IsKeywordEnabled("_METALLICSPECGLOSSMAP")}, emission keyword={material.IsKeywordEnabled("_EMISSION")}\nOriginal: key RGB=(1,.94,.82), intensity2.1; fill RGB=(.66,.79,1), intensity .55; chromatic ambient.\nNeutral comparison: key and fill RGB white; key1.1 fill.35; ambient sky(.22,.22,.22), equator(.18,.18,.18), ground(.08,.08,.08); postprocessing=false; HDR=false. No exposure/postprocess, texture/material/geometry mutations.\n");
        }catch(Exception e){Fail(e);}
    }
    static void Tick()
    {
        try {
            frame++;
            if(frame==12) {
                var center=lab.inspectionOrigin.position+Vector3.up*.17f;
                View(center+new Vector3(.58f,.25f,-.78f),center+Vector3.right*.07f);Shot("NeutralLighting/01_NearHand_Front45_Neutral");
            }
            if(frame==14) {
                var center=lab.inspectionOrigin.position+Vector3.up*.17f;
                keyLight.transform.rotation=Quaternion.Euler(12,78,0);View(center+new Vector3(.65f,.05f,-.72f),center);Shot("NeutralLighting/02_SideLight_Neutral");
                keyLight.transform.rotation=Quaternion.Euler(38,-30,0);lab.handScaleReference.gameObject.SetActive(false);lab.MoveTo(lab.socket.seat.position,Quaternion.identity);
            }
            if(frame==16) {
                View(lab.socket.opening.position+new Vector3(.53f,.42f,-.67f),lab.socket.opening.position+Vector3.up*.10f);Shot("NeutralLighting/03_Inserted_Front45_Neutral");
            }
            if(frame==18) {
                foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())if(go!=tickObject)Object.Destroy(go);
                lab=null;BuildProposal();
            }
            if(frame==24) {
                closedPenetration=LiftPenetration(.008f);
                if(closedPenetration<.005f)throw new Exception("Closed top stops did not geometrically block 8 mm lift");
                Shot("Proposal/01_Closed_TopEdgeStops");
                for(int i=0;i<2;i++)pins[i].transform.position=new Vector3(i==0?-.190f:.190f,1.012f,0);
            }
            if(frame==26) {
                openedPenetration=LiftPenetration(.008f);
                if(openedPenetration>0.00001f)throw new Exception("Retracted pins still block upward lift");
                Shot("Proposal/02_Retracted_ClearTop");
                File.WriteAllText(Output+"/proposal-lock-geometry.txt",$"PROPOSAL ONLY: pair of seat-mounted top-edge stops, not an existing shoulder/groove latch.\nFrozen tenon envelope .120x.350x.120 m unchanged; bottomY=.650, topY=1.000.\nTwo stop bars each .110x.020x.050 m. Closed centers X=+/- .105, bottomY=1.002: 2 mm initial gap and 10 mm overlap onto each top edge.\nRetracted centers X=+/- .190, 85 mm travel; inner edge X=+/- .135 gives 75 mm lateral clearance from each tenon side.\nComputePenetration for hypothetical 8 mm vertical lift: closed={closedPenetration:F8} m, retracted={openedPenetration:F8} m.\nNo enemy/clearance event, formal Take lock, chapter connection or approval asserted.\n");
                WorkshopTenonReviewTick.Tick=null;File.WriteAllText(Output+"/visual-supplements-result.txt","PASS neutral material captures and proposal closed/retracted geometric control\n");EditorApplication.Exit(0);
            }
        }catch(Exception e){Fail(e);}
    }
    static void NeutralLights()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Color.white*.22f;RenderSettings.ambientEquatorColor=Color.white*.18f;RenderSettings.ambientGroundColor=Color.white*.08f;
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {light.color=Color.white;light.intensity=light.name=="Review key light"?1.1f:.35f;}
    }
    static void BuildProposal()
    {
        var stone=new Material(Shader.Find("Universal Render Pipeline/Lit"));stone.SetColor("_BaseColor",new Color(.16f,.19f,.20f));stone.SetFloat("_Smoothness",.25f);
        var bronze=new Material(Shader.Find("Universal Render Pipeline/Lit"));bronze.SetColor("_BaseColor",new Color(.48f,.29f,.105f));bronze.SetFloat("_Metallic",.75f);bronze.SetFloat("_Smoothness",.38f);
        var iron=new Material(Shader.Find("Universal Render Pipeline/Lit"));iron.SetColor("_BaseColor",new Color(.11f,.15f,.16f));iron.SetFloat("_Metallic",.6f);iron.SetFloat("_Smoothness",.3f);
        Cube("Proposal calibration pedestal",new Vector3(0,.325f,0),new Vector3(.85f,.65f,.85f),stone);
        var root=new GameObject("Frozen tenon: no shoulder modification");root.transform.position=new Vector3(0,.65f,0);
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Model/MasterTenon_LOD0.fbx"),root.transform,false);
        var m=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/MasterTenon_R01.mat");foreach(var r in model.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=m;
        tenonEnvelope=root.AddComponent<BoxCollider>();tenonEnvelope.center=new Vector3(0,.175f,0);tenonEnvelope.size=new Vector3(.12f,.35f,.12f);
        pins=new GameObject[2];
        for(int i=0;i<2;i++) {
            float sign=i==0?-1:1;
            Cube("Fixture foot "+i,new Vector3(sign*.18f,.672f,0),new Vector3(.10f,.044f,.13f),iron);
            Cube("Fixture support "+i,new Vector3(sign*.18f,.837f,0),new Vector3(.038f,.33f,.045f),iron);
            Cube("Pin guide housing "+i,new Vector3(sign*.18f,1.012f,0),new Vector3(.075f,.044f,.085f),iron);
            pins[i]=Cube("Visible bronze top-edge stop "+i,new Vector3(sign*.105f,1.012f,0),new Vector3(.11f,.020f,.05f),bronze);
        }
        camera=new GameObject("Proposal camera (identical two states)").AddComponent<Camera>();camera.enabled=false;camera.aspect=4f/3;camera.allowHDR=false;camera.allowMSAA=false;camera.nearClipPlane=.02f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.10f,.12f);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        View(new Vector3(.62f,1.28f,-.80f),new Vector3(0,.855f,0));camera.fieldOfView=37;
        var key=new GameObject("Proposal neutral key").AddComponent<Light>();key.type=LightType.Directional;key.color=Color.white;key.intensity=1.1f;key.transform.rotation=Quaternion.Euler(38,-30,0);key.shadows=LightShadows.Soft;
        var fill=new GameObject("Proposal neutral fill").AddComponent<Light>();fill.type=LightType.Directional;fill.color=Color.white;fill.intensity=.35f;fill.transform.rotation=Quaternion.Euler(25,140,0);
    }
    static GameObject Cube(string name,Vector3 p,Vector3 scale,Material m)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=scale;go.GetComponent<MeshRenderer>().sharedMaterial=m;return go;}
    static float LiftPenetration(float lift)
    {
        Physics.SyncTransforms();float maximum=0;
        foreach(var p in pins)if(Physics.ComputePenetration(tenonEnvelope,tenonEnvelope.transform.position+Vector3.up*lift,Quaternion.identity,p.GetComponent<BoxCollider>(),p.transform.position,p.transform.rotation,out _,out float depth))maximum=Mathf.Max(maximum,depth);
        return maximum;
    }
    static void View(Vector3 p,Vector3 at){camera.transform.position=p;camera.transform.LookAt(at);camera.fieldOfView=42;}
    static void Shot(string name)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var previous=RenderTexture.active;RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,960),0,0);texture.Apply(false);RenderTexture.active=previous;
        File.WriteAllBytes(Delivery+"/"+name+".png",texture.EncodeToPNG());
    }
    static void Fail(Exception e){WorkshopTenonReviewTick.Tick=null;File.WriteAllText(Output+"/visual-supplements-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(4);}
}

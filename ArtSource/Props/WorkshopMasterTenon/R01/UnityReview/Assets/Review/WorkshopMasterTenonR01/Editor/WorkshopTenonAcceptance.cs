using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NarutoVR.SealCombat.Items;
using NarutoVR.WorkshopTenonReview;
using NeuGameMRTools.Runtime.Components;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class WorkshopTenonAcceptance
{
    const string Key="WorkshopTenonAcceptance.R01";
    const string Output=WorkshopTenonLabBuild.Output;
    const string Delivery=WorkshopTenonLabBuild.Delivery;
    static WorkshopTenonLabController lab;
    static Camera camera;static RenderTexture target;static Texture2D image;
    static IEnumerator steps;static int frame,videoFrame;static bool record;
    static float maxRuntimePenetration;static readonly List<string> report=new();
    static WorkshopTenonAcceptance()=>EditorApplication.update+=Boot;
    public static void Run()
    {
        if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-T01Seal-20261124/Assets")throw new Exception("Use exclusive physical review project");
        try {
            WorkshopTenonLabBuild.Build();
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void Boot()
    {
        if(!Application.isPlaying || EditorApplication.isCompiling || !SessionState.GetBool(Key,false))return;
        SessionState.SetBool(Key,false);
        try {
            Directory.CreateDirectory(Output+"/frames");Directory.CreateDirectory(Delivery+"/Images");
            File.WriteAllText(Output+"/timeline.tsv","frame\ttime\theld\tsnapping\tstarted\tcompleted\tposition\trotation\tpenetration\n");
            lab=Object.FindFirstObjectByType<WorkshopTenonLabController>();lab.keyboardEnabled=false;lab.showPanel=false;
            camera=Camera.main;camera.enabled=false;camera.aspect=4f/3;camera.allowHDR=false;camera.allowMSAA=false;
            target=new RenderTexture(1280,960,24);target.Create();image=new Texture2D(1280,960,TextureFormat.RGB24,false);
            Time.captureFramerate=30;Time.timeScale=1;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;Application.runInBackground=true;
            new GameObject("R01 acceptance frame observer").AddComponent<WorkshopTenonReviewTick>();WorkshopTenonReviewTick.Tick=Step;
            steps=Checks();
        }catch(Exception e){Fail(e);}
    }
    static void Step()
    {
        try {
            frame++;
            if(frame>1600)throw new Exception("Review play test exceeded frame limit");
            var zone=lab.socket;var item=lab.tenon;
            float pen=zone.PenetrationAt(item.transform.position,item.transform.rotation);
            if(zone.IsSnapping) {
                maxRuntimePenetration=Mathf.Max(maxRuntimePenetration,pen);
                if(pen>.0003f)throw new Exception("Actual snap trajectory penetrated console by "+pen+" m");
                if(!item.GetComponent<BoxCollider>().enabled)throw new Exception("Collider disabled during insertion");
            }
            File.AppendAllText(Output+"/timeline.tsv",$"{frame}\t{Time.time:F4}\t{item.IsHeld}\t{zone.IsSnapping}\t{zone.startedCount}\t{zone.completedCount}\t{item.transform.position.ToString("F6")}\t{item.transform.eulerAngles.ToString("F3")}\t{pen:F8}\n");
            if(record)CaptureFrame();
            if(steps.MoveNext())return;
            WorkshopTenonReviewTick.Tick=null;
            report.Add("PASS actual per-frame maximum console penetration during all snaps = "+maxRuntimePenetration.ToString("F8")+" m");
            report.Add("BOUNDARY: desktop/API synthetic control; no live Meta/Quest hand tracking, user ergonomics or artistic acceptance asserted.");
            report.Add("BOUNDARY: independent review only. No chapter layout, unlock, generic inventory code or production assets modified.");
            File.WriteAllLines(Output+"/result.txt",new[]{"PASS R01"}.Concat(report));
            WorkshopTenonLabBuild.Export();
            EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static void Require(bool pass,string note)
    {if(!pass)throw new Exception(note);report.Add("PASS "+note);File.WriteAllLines(Output+"/progress.txt",report);}
    static void Fail(Exception e)
    {
        WorkshopTenonReviewTick.Tick=null;Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/failure.txt",e.ToString()+"\n"+string.Join("\n",report));
        Debug.LogException(e);EditorApplication.Exit(3);
    }
    static IEnumerator Wait(int count) {for(int i=0;i<count;i++)yield return null;}
    static IEnumerator Checks()
    {
        var item=lab.tenon;var zone=lab.socket;var body=item.GetComponent<Rigidbody>();var shape=item.GetComponent<BoxCollider>();
        for(int i=0;i<30;i++)yield return null;
        Require(Vector3.Distance(item.transform.localScale,Vector3.one)<.00001f,"Real model root remains scale 1; imported .120 x .350 x .120 m envelope");
        Require(item.grip && item.insertionTip && !item.keyItem && !item.home,"Explicit grip and insertion anchors; no belt home / key-item auto-return");
        Require(!Object.FindObjectsByType<ToolBeltInteraction>(FindObjectsSortMode.None).Any(x=>x.items!=null&&x.items.Contains(item)),"Main tenon excluded from generic ToolBeltInteraction item registry (review scope)");
        Require(Mathf.Abs(item.transform.position.y-.65f)<.008f,"Initial 03 display supports the item using physical collision");
        lab.handScaleReference.gameObject.SetActive(false);
        View(new Vector3(2.65f,2.2f,-3.6f),new Vector3(0,.75f,0));Shot("01_TwoSeparateStations");
        Require(lab.Take() && item.IsHeld && body.isKinematic && !body.useGravity,"GameItemBase.Take acquires and freezes item");
        Require(!lab.Take(),"Duplicate take rejected while already held");
        lab.Inspect();yield return null;
        Require(Vector3.Distance(item.GripPosition,lab.inspectionOrigin.position+item.transform.rotation*item.grip.localPosition)<.00001f,"GameItemBase.MoveHeld places the grip at the requested point");
        lab.handScaleReference.gameObject.SetActive(true);
        Vector3 center=lab.inspectionOrigin.position+Vector3.up*.17f;
        View(center+new Vector3(.58f,.25f,-.78f),center+Vector3.right*.07f);Shot("02_NearHand_Front45_1to1");
        View(center+new Vector3(-.58f,.17f,.75f),center);Shot("03_BackSide_1to1");
        var key=GameObject.Find("Review key light").GetComponent<Light>();var oldRotation=key.transform.rotation;key.transform.rotation=Quaternion.Euler(12,78,0);
        View(center+new Vector3(.65f,.05f,-.72f),center);Shot("04_SideLight_1to1");key.transform.rotation=oldRotation;
        lab.handScaleReference.gameObject.SetActive(false);
        lab.MoveTo(lab.displayOrigin.position+Vector3.up*.17f,Quaternion.identity);lab.Drop();
        Require(!item.IsHeld && !body.isKinematic && body.useGravity && shape.enabled,"Drop restores dynamic rigidbody, gravity and physical collider");
        for(int i=0;i<55;i++)yield return null;
        Require(Mathf.Abs(item.transform.position.y-.65f)<.009f && body.linearVelocity.magnitude<.06f,"Released item settles on separate display without passing through");
        View(new Vector3(-.35f,1.25f,-.75f),new Vector3(-.85f,.83f,0));Shot("05_PhysicalDropOnDisplay");
        Require(lab.Take(),"Item can be picked up again after free drop");
        lab.AboveOpening();
        Require(!zone.TrySnapObject(item.gameObject) && zone.startedCount==0,"Dedicated socket refuses held item");
        View(zone.opening.position+new Vector3(.22f,.68f,-.30f),zone.opening.position);
        lab.MoveTo(zone.alignment.position+Vector3.right*.25f,Quaternion.identity);yield return null;Shot("06_ActualSocketTop_EmptyOpening");
        // The actual generic ToolBeltSlot is intentionally not taught a new identity rule.
        // Its five current dimensions are exercised; purpose exclusion is the separate registry above.
        lab.Drop();
        var sizes=new[]{new Vector3(.084f,.124f,.022f),new Vector3(.037f,.265f,.038f),new Vector3(.086f,.122f,.081f),new Vector3(.037f,.257f,.038f),new Vector3(.084f,.255f,.077f)};
        for(int i=0;i<sizes.Length;i++) {
            var opening=new GameObject("Actual belt dimension test "+i).transform;opening.position=new Vector3(0,3+i,0);
            var go=new GameObject("ToolBeltSlot test");go.transform.SetParent(opening,false);var slot=go.AddComponent<ToolBeltSlot>();slot.opening=opening;slot.width=sizes[i].x;slot.depth=sizes[i].y;slot.thickness=sizes[i].z;
            Require(!slot.TrySnapObject(item.gameObject),"Unmodified generic ToolBeltSlot rejects master tenon envelope at actual slot "+i+" dimensions "+sizes[i]);Object.Destroy(go);Object.Destroy(opening.gameObject);
        }
        var foreign=new GameObject("Foreign test object");foreign.AddComponent<Rigidbody>();var foreignBox=foreign.AddComponent<BoxCollider>();foreignBox.size=Vector3.one*.02f;foreign.AddComponent<GameItemBase>();foreign.transform.position=zone.alignment.position;
        Require(!zone.TrySnapObject(foreign),"Purpose socket rejects a geometrically small foreign item");Object.Destroy(foreign);
        Require(lab.Take(),"Reacquire before pose rejection tests");
        lab.MoveTo(zone.alignment.position,Quaternion.Euler(0,45,0));
        Require(!lab.ReleaseAndInsert() && zone.startedCount==0,"45-degree yaw is rejected rather than forced through opening");
        Require(lab.Take(),"Reacquire after rejected yaw");
        lab.MoveTo(zone.alignment.position+Vector3.right*.03f,Quaternion.identity);
        Require(!lab.ReleaseAndInsert() && zone.startedCount==0,"30 mm off-center insertion is rejected");
        Require(lab.Take(),"Reacquire after rejected offset");
        lab.MoveTo(zone.alignment.position,Quaternion.Euler(90,0,0));
        Require(!lab.ReleaseAndInsert() && zone.startedCount==0,"Horizontal insertion axis is rejected");
        File.WriteAllText(Output+"/collision-oracle-diagnostics.txt",$"actualTriangles={zone.actualConsoleTriangles.Length/3}\nPhysics45={zone.PhysicsPenetrationAt(zone.seat.position,Quaternion.Euler(0,45,0))}\nGeometry45={zone.GeometryPenetrationAt(zone.seat.position,Quaternion.Euler(0,45,0))}\nPhysicsDeep={zone.PhysicsPenetrationAt(zone.seat.position-Vector3.up*.012f,Quaternion.identity)}\nGeometryDeep={zone.GeometryPenetrationAt(zone.seat.position-Vector3.up*.012f,Quaternion.identity)}\n"+string.Join("\n",zone.actualConsoleColliders.Select(c=>$"{c.name}: enabled={c.enabled}, active={c.gameObject.activeInHierarchy}, bounds={c.bounds}, scale={c.transform.lossyScale}")));
        Require(zone.GeometryPenetrationAt(zone.seat.position,Quaternion.Euler(0,45,0))>.001f,"Exact triangle SAT detects actual wall interference at 45 degrees");
        Require(zone.GeometryPenetrationAt(zone.seat.position-Vector3.up*.012f,Quaternion.identity)>.001f,"Exact triangle SAT detects unsafe depth into actual console body");
        for(int quarter=0;quarter<4;quarter++) {
            Require(lab.Take(),"Take / release SnapZone owner for quarter "+quarter);
            var rotation=Quaternion.Euler(0,quarter*90,0);
            if(quarter==0)rotation*=Quaternion.Euler(4,7,0);
            lab.MoveTo(zone.opening.position+new Vector3(.004f,.040f,.004f),rotation);
            View(zone.opening.position+new Vector3(.53f,.42f,-.67f),zone.opening.position+Vector3.up*.10f);
            if(quarter==0){record=true;for(int i=0;i<12;i++)yield return null;Shot("07_AboveActualOpening");}
            Require(lab.ReleaseAndInsert(),"Accepted centered quarter "+quarter+" through original SnapZone route: "+zone.LastFailure);
            Require(!lab.Take(),"Take blocked while actual SnapZone interpolation owns item");
            for(int i=0;i<48;i++)yield return null;
            Require(!zone.IsSnapping && zone.SnappedObject==item.gameObject && zone.completedCount==quarter+1 && zone.startedCount==quarter+1,"Quarter "+quarter+" completed exactly once");
            Require(Vector3.Distance(item.transform.position,zone.seat.position)<.00002f && Quaternion.Angle(item.transform.rotation,Quaternion.Euler(0,quarter*90,0))<.02f,"Quarter "+quarter+" returns to indexed pose and sample seat");
            Require(shape.enabled && zone.PenetrationAt(item.transform.position,item.transform.rotation)<.0003f,"Quarter "+quarter+" retains collision with no wall/body penetration");
            Require(!zone.TrySnapObject(item.gameObject),"Repeated request while occupied does not create second completion");
            for(int i=0;i<15;i++)yield return null;
            Require(zone.completedCount==quarter+1,"Held seat remains one completion for quarter "+quarter);
            if(quarter==0){Shot("08_Inserted_Front45");View(zone.opening.position+new Vector3(.67f,.12f,.16f),zone.opening.position+Vector3.up*.08f);Shot("09_Inserted_Side");record=false;}
        }
        Require(lab.Take() && !zone.SnappedObject && item.IsHeld,"Completed item can be released from socket and re-grabbed");
        lab.MoveTo(zone.opening.position+Vector3.up*.030f,Quaternion.identity);Require(lab.ReleaseAndInsert(),"Final return for the inspectable 0-degree seated view");
        for(int i=0;i<48;i++)yield return null;
        Require(zone.completedCount==5 && zone.startedCount==5,"Five deliberate complete insertions; no extra completion events");
        View(zone.opening.position+new Vector3(.43f,.58f,-.55f),zone.opening.position+Vector3.up*.06f);Shot("10_InsertedTop");
        View(new Vector3(2.65f,2.2f,-3.6f),new Vector3(0,.75f,0));Shot("11_FinalOverview");
    }
    static void View(Vector3 position,Vector3 at) {camera.transform.position=position;camera.transform.LookAt(at);camera.fieldOfView=42;}
    static void Render()
    {
        var previous=RenderTexture.active;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply(false);RenderTexture.active=previous;
    }
    static void Shot(string name)
    {Render();File.WriteAllBytes(Delivery+"/Images/"+name+".png",image.EncodeToPNG());File.AppendAllText(Output+"/screenshots.tsv",name+"\t"+frame+"\n");}
    static void CaptureFrame()
    {Render();File.WriteAllBytes(Output+"/frames/frame_"+(videoFrame++).ToString("D4")+".jpg",image.EncodeToJPG(92));}
}

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
using NarutoVR.QingGuiR01Review;
using NarutoVR.SealCombat.Items;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class QingGuiFacingCapture
{
    const string Key="QingGuiFacingCapture.Run";
    const string Output=QingGuiR01LabBuild.Output+"/orientation-final";
    const string Delivery=QingGuiR01LabBuild.Delivery+"/FacingReview";
    static QingGuiR01LabController lab;static Camera camera;static IEnumerator routine;static RenderTexture target;static Texture2D image;static int frame;
    [Serializable] class GripMeasurement {public float height,minimumX,maximumX,minimumZ,maximumZ;public Vector3 oldGrip,newGrip;public float adjustmentMetres;public int intersections;public string method,boundary;}
    static QingGuiFacingCapture(){EditorApplication.update+=Boot;}
    public static void Run()
    {
        try {
            if(Application.dataPath.Replace('\\','/')!="D:/Temp/NarutoVR-QingGuiR01-20261125/Assets")throw new Exception("Dedicated QingGui project only");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Delivery);
            EditorSceneManager.OpenScene(QingGuiR01LabBuild.ScenePath);
            lab=Object.FindFirstObjectByType<QingGuiR01LabController>();
            var item=lab.tenon;var points=new List<Vector3>();
            foreach(var filter in item.GetComponentsInChildren<MeshFilter>()) {
                if(!filter.name.Contains("LOD0"))continue;var vertices=filter.sharedMesh.vertices;var triangles=filter.sharedMesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)for(int e=0;e<3;e++) {
                    var a=item.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[i+e]]));var b=item.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[i+(e+1)%3]]));
                    if(Mathf.Abs(a.y-.230f)<.0000001f)points.Add(a);
                    if((a.y-.230f)*(b.y-.230f)<0)points.Add(a+(.230f-a.y)/(b.y-a.y)*(b-a));
                }
            }
            if(points.Count<4)throw new Exception("Actual imported grip section not found");
            // Preserve the imported-plane center explicitly accepted by the parent.
            var old=item.grip.localPosition;var corrected=new Vector3((points.Min(p=>p.x)+points.Max(p=>p.x))*.5f,.230f,(points.Min(p=>p.z)+points.Max(p=>p.z))*.5f);
            if(Vector3.Distance(old,corrected)>.0031f)throw new Exception("Grip correction exceeds parent's 3mm coordinate-conversion scope: "+old+" -> "+corrected);
            var prefab=PrefabUtility.LoadPrefabContents(QingGuiR01LabBuild.Root+"/QingGuiR01.prefab");prefab.GetComponent<GameItemBase>().grip.localPosition=corrected;PrefabUtility.SaveAsPrefabAsset(prefab,QingGuiR01LabBuild.Root+"/QingGuiR01.prefab");PrefabUtility.UnloadPrefabContents(prefab);
            item.grip.localPosition=corrected;PrefabUtility.RecordPrefabInstancePropertyModifications(item.grip);
            Camera.main.transform.position=new Vector3(2.65f,2.2f,3.6f);Camera.main.transform.LookAt(new Vector3(0,.75f,0));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),QingGuiR01LabBuild.ScenePath);AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"/grip-imported-section.json",JsonUtility.ToJson(new GripMeasurement{height=.230f,minimumX=points.Min(p=>p.x),maximumX=points.Max(p=>p.x),minimumZ=points.Min(p=>p.z),maximumZ=points.Max(p=>p.z),oldGrip=old,newGrip=corrected,adjustmentMetres=Vector3.Distance(old,corrected),intersections=points.Count,method="Actual Unity LOD0 triangle edges intersected with root-local Y=.230m; section AABB center",boundary="Independent grip anchor coordinate correction only. Bottom origin, mesh, colliders, insertion rotations and seat remain unchanged. No XR binding."},true));
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void Boot()
    {
        if(!Application.isPlaying||EditorApplication.isCompiling||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);
        try {
            lab=Object.FindFirstObjectByType<QingGuiR01LabController>();lab.keyboardEnabled=false;lab.showPanel=false;camera=Camera.main;camera.enabled=false;camera.aspect=4f/3;camera.allowMSAA=false;
            target=new RenderTexture(1280,960,24);target.Create();image=new Texture2D(1280,960,TextureFormat.RGB24,false);
            new GameObject("Correct-facing capture tick").AddComponent<QingGuiR01ReviewTick>();QingGuiR01ReviewTick.Tick=Step;routine=CaptureViews();
        }catch(Exception e){Fail(e);}
    }
    static void Step()
    {
        try {
            if(++frame>250)throw new Exception("Facing capture exceeded frame limit");if(routine.MoveNext())return;QingGuiR01ReviewTick.Tick=null;
            File.WriteAllText(Output+"/result.txt","PASS photography supplement and corrected grip MoveHeld interface check.\nActual C4 front=Unity +Z viewing toward -Z; C4 right=Unity -X. Only camera facing and grip center were corrected. Original geometric/optical regressions not rerun. No XR entry.\n");
            QingGuiR01LabBuild.Export();EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static IEnumerator CaptureViews()
    {
        for(int i=0;i<20;i++)yield return null;
        if(!lab.Take())throw new Exception("Corrected-grip Take failed");lab.Inspect();
        var expected=lab.inspectionOrigin.position+lab.tenon.transform.rotation*lab.tenon.grip.localPosition;
        if(Vector3.Distance(lab.tenon.GripPosition,expected)>.00001f)throw new Exception("Corrected grip MoveHeld world target mismatch");
        File.WriteAllText(Output+"/grip-interface-result.txt","PASS real GameItemBase.Take / MoveHeld with corrected imported-plane grip; world target residual <= 10um. No XR events wired.\n");
        var center=lab.inspectionOrigin.position+Vector3.up*.175f;lab.handScaleReference.gameObject.SetActive(false);
        var context=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>!r.transform.IsChildOf(lab.tenon.transform)&&!r.transform.IsChildOf(lab.handScaleReference)).ToArray();
        var contextEnabled=context.Select(r=>r.enabled).ToArray();foreach(var r in context)r.enabled=false;
        camera.orthographic=true;camera.orthographicSize=.215f;
        View(center+Vector3.forward*.8f,center);yield return null;Shot("01_C4_Front_Orthographic");
        View(center+Vector3.left*.8f,center);yield return null;Shot("02_C4_Right_Orthographic");
        View(center+Vector3.back*.8f,center);yield return null;Shot("03_C4_Back_Orthographic");
        camera.orthographic=false;camera.fieldOfView=42;lab.handScaleReference.gameObject.SetActive(true);
        View(center+new Vector3(-.58f,.25f,.78f),center+Vector3.right*.07f);yield return null;Shot("04_C4_Front45_NearHand");
        lab.handScaleReference.gameObject.SetActive(false);
        View(lab.inspectionOrigin.position+new Vector3(-.18f,.26f,.36f),lab.inspectionOrigin.position+Vector3.up*.235f);yield return null;Shot("05_C4_Front_GoldLines_Grip");
        View(lab.inspectionOrigin.position+new Vector3(-.12f,.37f,.24f),lab.inspectionOrigin.position+new Vector3(-.04f,.328f,0));yield return null;Shot("06_C4_CrownHole_Close");
        // Photograph the already-tested exact zero-degree seat pose. No new snap claim.
        for(int i=0;i<context.Length;i++)context[i].enabled=contextEnabled[i]&&!context[i].GetComponent<TextMesh>();
        lab.MoveTo(lab.socket.seat.position,Quaternion.identity);
        View(lab.socket.opening.position+new Vector3(-.53f,.42f,.67f),lab.socket.opening.position+Vector3.up*.10f);yield return null;Shot("07_C4_Front45_SeatedPose");
        View(lab.socket.opening.position+new Vector3(0,.20f,.83f),lab.socket.opening.position+Vector3.up*.10f);yield return null;Shot("08_C4_Front_SeatedPose");
        File.WriteAllText(Output+"/photography-pose.txt","Object root quaternion remains identity; seated photos use prior tested seat "+lab.socket.seat.position.ToString("F7")+". Camera moved to C4 front; object not rotated to repair naming. First 3 views are orthographic at size .215m, 1280x960, identical object pose and neutral light.\n");
    }
    static void View(Vector3 from,Vector3 at){camera.transform.position=from;camera.transform.LookAt(at);}
    static void Shot(string name)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply(false);RenderTexture.active=old;File.WriteAllBytes(Delivery+"/"+name+".png",image.EncodeToPNG());
    }
    static void Fail(Exception e){QingGuiR01ReviewTick.Tick=null;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(3);}
}

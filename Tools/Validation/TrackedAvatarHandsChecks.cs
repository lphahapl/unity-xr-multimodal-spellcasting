using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NarutoVR.Editor;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealLab;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in an isolated copy under Assets/.../Editor with -executeMethod TrackedAvatarHandsChecks.Run.
public static class TrackedAvatarHandsChecks
{
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        Debug.Log("[TrackedAvatar PASS] " + message);
    }

    public static void Run()
    {
        try
        {
            string input = Environment.GetEnvironmentVariable("AVATAR_INPUT");
            string output = Environment.GetEnvironmentVariable("AVATAR_OUTPUT");
            AssetDatabase.ImportAsset(PlayerHandRig.Package+"UnityXR/Prefabs/XROriginCameraRig.prefab",ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(input);
            var variant = TrackedAvatarBuilder.Build(model, output);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(variant, scene);
                var bridge = root.GetComponent<TrackedAvatarArms>();
                var origin = new GameObject("TrackingSpace").transform;
                origin.SetParent(root.transform, false);
                origin.SetPositionAndRotation(new Vector3(.81f,.27f,-1.32f), Quaternion.Euler(7,73,-3));
                var animator = root.GetComponentInChildren<Animator>(true);
                Check(animator.isHuman && animator.avatar.isValid, "Humanoid Avatar");
                Check(!bridge.source && !root.GetComponentInChildren<SealLab>(true), "no duplicate tracking source");
                foreach (bool left in new[] {true, false})
                {
                    string side = left ? "Left" : "Right";
                    var arm = bridge.Side(left);
                    var other = bridge.Side(!left);
                    var sdk = (GameObject)PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHandRig.Visual(side)),scene);
                    var visual = sdk.GetComponent<HandVisual>();
                    var rest = arm.targets.Select(b => b.localRotation).ToArray();
                    var otherRest = other.targets.Select(b => b.localRotation).ToArray();
                    Skin(root, arm, side);
                    float upperLength = Vector3.Distance(arm.upper.position,arm.lower.position);
                    float lowerLength = Vector3.Distance(arm.lower.position,arm.targets[0].position);
                    var sourceBones = Enumerable.Range(0,16)
                        .Select(i => visual.GetTransformByHandJointId(TrackedAvatarArms.Joint(i))).ToArray();
                    Quaternion Palm(Transform[] b) => Quaternion.LookRotation(b[4].position + b[13].position - 2*b[0].position,
                        Vector3.Cross(b[4].position-b[0].position,b[13].position-b[0].position));
                    Quaternion align = Palm(arm.targets) * Quaternion.Inverse(Palm(sourceBones));
                    var hand = new HandSnapshot { connected=true,tracked=true,fresh=true,observedAt=100 };
                    var frame = new SealFrame();
                    if (left) frame.left=hand; else frame.right=hand;
                    Quaternion inverseOrigin = Quaternion.Inverse(origin.rotation);
                    Vector3 goal = arm.upper.position + (left ? Vector3.left : Vector3.right)*.14f +
                        Vector3.down*.13f + Vector3.forward*.18f;
                    for (int i=0;i<16;++i)
                    {
                        int joint=(int)TrackedAvatarArms.Joint(i);
                        hand.jointValid[joint]=true;
                        hand.joints[joint]=new Pose(origin.InverseTransformPoint(goal + align*(sourceBones[i].position-sourceBones[0].position)),
                            inverseOrigin*align*sourceBones[i].rotation);
                    }
                    var wristRotation=arm.targets[0].rotation;
                    bridge.ApplyFrame(frame,origin,100,.12f);
                    float error=Vector3.Distance(arm.targets[0].position,goal);
                    Check(error<.00025f && Quaternion.Angle(wristRotation,arm.targets[0].rotation)<.1f,
                        $"{side} wrist in rotated/translated space: {error*1000:F3} mm");
                    Check(Enumerable.Range(1,15).All(i=>Quaternion.Angle(rest[i],arm.targets[i].localRotation)<.1f),
                        side+" neutral finger-axis calibration 15/15");
                    for (int slot=1;slot<16;++slot)
                    {
                        int joint=(int)TrackedAvatarArms.Joint(slot);
                        Pose original=hand.joints[joint];
                        hand.joints[joint].rotation=Quaternion.AngleAxis(35,Vector3.right)*original.rotation;
                        bridge.ApplyFrame(frame,origin,100,.12f);
                        Check(Mathf.Abs(Quaternion.Angle(rest[slot],arm.targets[slot].localRotation)-35)<.12f,
                            side+" joint responds "+TrackedAvatarArms.Joint(slot));
                        hand.jointValid[joint]=false;
                        bridge.ApplyFrame(frame,origin,100,.12f);
                        Check(Quaternion.Angle(rest[slot],arm.targets[slot].localRotation)<.1f,side+" missing joint returns to animation "+slot);
                        hand.jointValid[joint]=true;
                        hand.joints[joint]=original;
                    }
                    int wrist=(int)TrackedAvatarArms.Joint(0);
                    hand.joints[wrist].position=origin.InverseTransformPoint(arm.upper.position+Vector3.forward*2);
                    bridge.ApplyFrame(frame,origin,100,.12f);
                    Check(Mathf.Abs(Vector3.Distance(arm.targets[0].position,arm.upper.position)-upperLength-lowerLength)<.00025f &&
                        Mathf.Abs(Vector3.Distance(arm.upper.position,arm.lower.position)-upperLength)<.00025f &&
                        Mathf.Abs(Vector3.Distance(arm.targets[0].position,arm.lower.position)-lowerLength)<.00025f,side+" unreachable wrist clamped without stretching bones");
                    bridge.ApplyFrame(frame,origin,100.2,.12f);
                    Check(rest.Select((r,i)=>Quaternion.Angle(r,arm.targets[i].localRotation)).Max()<.1f &&
                        otherRest.Select((r,i)=>Quaternion.Angle(r,other.targets[i].localRotation)).Max()<.1f,side+" stale sample and unilateral isolation");
                }
                string recording = Environment.GetEnvironmentVariable("AVATAR_RECORDING");
                if (!string.IsNullOrEmpty(recording)) Replay(bridge, origin, recording);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            foreach (string name in new[]{"Chapter01_Awakening","Chapter01_Courtyard"}) Chapter(name,variant);
            Debug.Log("[TrackedAvatar COMPLETE] "+input+" -> "+output);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }

    static void Skin(GameObject root, TrackedAvatarArms.Arm arm, string side)
    {
        var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var baked=new Mesh();
        try
        {
            for (int slot=1;slot<16;++slot)
            {
                Transform bone=arm.targets[slot];
                int vertices=0;
                float peak=0;
                foreach (var renderer in renderers)
                {
                    int index=Array.IndexOf(renderer.bones,bone);
                    if(index<0) continue;
                    var weights=renderer.sharedMesh.boneWeights;
                    var affected=Enumerable.Range(0,weights.Length).Where(v=>{
                        var w=weights[v];
                        return w.boneIndex0==index && w.weight0>=.05f || w.boneIndex1==index && w.weight1>=.05f ||
                            w.boneIndex2==index && w.weight2>=.05f || w.boneIndex3==index && w.weight3>=.05f;
                    }).ToArray();
                    vertices+=affected.Length;
                    // With useScale=true the baked vertices can be taken through the renderer's
                    // transform exactly once, including FBX hierarchies with compensating 100x scale.
                    renderer.BakeMesh(baked,true);
                    var neutral=baked.vertices;
                    Quaternion saved=bone.localRotation;
                    try
                    {
                        bone.rotation=Quaternion.AngleAxis(35,root.transform.forward)*bone.rotation;
                        renderer.BakeMesh(baked,true);
                        var flex=baked.vertices;
                        foreach(int v in affected)
                            peak=Mathf.Max(peak,renderer.transform.TransformVector(flex[v]-neutral[v]).magnitude);
                    }
                    finally {bone.localRotation=saved;}
                }
                Check(vertices>0 && peak>.0003f && peak<.16f,$"{side} skin {TrackedAvatarArms.Joint(slot)}: {vertices} vertices >=5%, {peak*1000:F2} mm flex");
            }
        }
        finally {UnityEngine.Object.DestroyImmediate(baked);}
    }

    static void Replay(TrackedAvatarArms bridge, Transform origin, string recording)
    {
        origin.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        int samples=0, reachable=0;
        float peak=0;
        foreach (string line in File.ReadLines(recording))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var frame=JsonUtility.FromJson<SealFrame>(line);
            bridge.ApplyFrame(frame,origin,frame.time,.12f);
            foreach (bool left in new[]{true,false})
            {
                var h=left?frame.left:frame.right;
                if (!h.connected || !h.tracked || !h.fresh || !h.jointValid[1] ||
                    frame.time-h.observedAt>.12 || frame.time<h.observedAt) continue;
                var arm=bridge.Side(left);
                ++samples;
                Vector3 goal=h.joints[1].position, shoulder=arm.upper.position;
                float a=Vector3.Distance(shoulder,arm.lower.position), b=Vector3.Distance(arm.lower.position,arm.targets[0].position);
                float reach=Vector3.Distance(goal,shoulder);
                if(reach>Mathf.Abs(a-b) && reach<a+b) ++reachable;
                var expected=shoulder+(goal-shoulder).normalized*Mathf.Clamp(reach,Mathf.Abs(a-b),a+b);
                peak=Mathf.Max(peak,Vector3.Distance(expected,arm.targets[0].position));
            }
        }
        Check(samples>0 && peak<.0003f,$"historical replay {Path.GetFileName(recording)}: {samples} tracked hand samples, {reachable} in reach, max error to clamped goal {peak*1000:F3} mm");
        bridge.RestoreAnimation();
    }

    static void Chapter(string name,GameObject prefab)
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scene/"+name+".unity");
        var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LinearRpgController>(true)).Single();
        var originalMenu=player.wristMenu.wornDevice;
        var avatar=TrackedAvatarBuilder.Install(scene,prefab);
        Check(TrackedAvatarBuilder.Install(scene,prefab)==avatar,name+" repeat installation reuses avatar");
        var display=avatar.GetComponent<TrackedAvatarPresentation>();
        var bridge=avatar.GetComponent<TrackedAvatarArms>();
        var visuals=player.skills.lab.xrHandsSource.GetComponentsInChildren<HandVisual>(true)
            .Where(v=>v.transform.parent==player.skills.lab.xrHandsSource.transform).ToArray();
        Check(visuals.Length==2 && bridge.source==player.skills.lab && player.wristMenu.wornDevice==originalMenu &&
            player.wristMenu.playerWatchMount && !player.skills.lab.GetComponent<SealLabView>().showBoneLines,
            name+" V2 pair, original input and functional wrist case");
        var cam=new GameObject("QA observer").AddComponent<Camera>();
        display.spectator=cam;
        // Drive the same URP callbacks, checking actual scene renderers and restoration.
        var begin=typeof(TrackedAvatarPresentation).GetMethod("BeginCamera",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var end=typeof(TrackedAvatarPresentation).GetMethod("EndCamera",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var onEnable=typeof(TrackedAvatarPresentation).GetMethod("OnEnable",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var onDisable=typeof(TrackedAvatarPresentation).GetMethod("OnDisable",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        onEnable.Invoke(display,null);
        var hmd=player.locomotion.head.GetComponentInChildren<Camera>(true);
        Check(hmd,name+" actual XR rig camera");
        var body=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>!display.firstPersonSkin.Contains(r));
        var hands=visuals.SelectMany(v=>v.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToArray();
        var caseMesh=originalMenu.GetComponentsInChildren<MeshRenderer>(true).First();
        object[] Args(Camera camera)=>new object[]{default(UnityEngine.Rendering.ScriptableRenderContext),camera};
        try
        {
            begin.Invoke(display,Args(hmd));
            Check(body.forceRenderingOff && hands.All(h=>!h.forceRenderingOff) && !caseMesh.forceRenderingOff &&
                display.firstPersonSkin.Length>0 && display.firstPersonSkin.All(r=>r.enabled && !r.forceRenderingOff),
                name+" HMD has cropped torso, sleeves, V2 hands and functional case");
            begin.Invoke(display,Args(cam));
            Check(!body.forceRenderingOff && hands.All(h=>h.forceRenderingOff) && caseMesh.forceRenderingOff &&
                display.firstPersonSkin.All(r=>r.forceRenderingOff),name+" nested observer visibility");
            end.Invoke(display,Args(cam));
            Check(body.forceRenderingOff && !caseMesh.forceRenderingOff,name+" outer camera state restored");
            end.Invoke(display,Args(hmd));
            Check(!body.forceRenderingOff && hands.All(h=>!h.forceRenderingOff) && !caseMesh.forceRenderingOff,name+" original renderer states restored");
            if(name=="Chapter01_Courtyard") DrawBothViews(display,cam,visuals);
        }
        finally {onDisable.Invoke(display,null);}
    }

    static void DrawBothViews(TrackedAvatarPresentation display, Camera camera, HandVisual[] visuals)
    {
        // Isolate the character from the large backdrop of the real chapter. Only the unsaved
        // scene in the QA clone is touched. All render passes still use the project's URP.
        foreach(var r in display.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer=29;
        var origin=new GameObject("QA recording space").transform;
        display.transform.position=origin.position=new Vector3(2000,0,2000);
        var rest=display.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r=>r,r=>Baked(r));
        string recording=Environment.GetEnvironmentVariable("AVATAR_RECORDING");
        if(!string.IsNullOrEmpty(recording))
        {
            var frame=File.ReadLines(recording).Where(s=>!string.IsNullOrWhiteSpace(s))
                .Select(s=>JsonUtility.FromJson<SealFrame>(s)).First(f=>f.left.tracked && f.right.tracked);
            display.GetComponent<TrackedAvatarArms>().ApplyFrame(frame,origin,frame.time,.12f);
        }
        var target=display.transform.position+new Vector3(0,1.02f,0);
        camera.transform.position=target+new Vector3(.4f,.12f,2.65f);
        camera.transform.LookAt(target);
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.17f,.23f,.29f);
        camera.fieldOfView=46;
        camera.cullingMask=1<<29;
        camera.stereoTargetEye=StereoTargetEyeMask.None;
        var light=new GameObject("QA character light").AddComponent<Light>();
        light.type=LightType.Directional;
        light.transform.rotation=Quaternion.LookRotation(new Vector3(-.35f,-.6f,-1));
        light.intensity=1.6f;
        light.cullingMask=1<<29;
        var rt=RenderTexture.GetTemporary(512,512,24);
        var oldActive=RenderTexture.active;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;
            Color32[] Draw()
            {
                camera.Render();
                RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);
                tex.Apply();
                return tex.GetPixels32();
            }
            display.spectator=null;
            var local=Draw();
            display.spectator=camera;
            var observer=Draw();
            int changed=0;
            for(int i=0;i<local.Length;++i)
                if(Mathf.Abs(local[i].r-observer[i].r)+Mathf.Abs(local[i].g-observer[i].g)+Mathf.Abs(local[i].b-observer[i].b)>40) ++changed;
            Check(changed>1500,$"URP camera renders 512x512; {changed} different pixels when full body is selected");
            string image=Environment.GetEnvironmentVariable("AVATAR_IMAGE");
            if(!string.IsNullOrEmpty(image)) File.WriteAllBytes(image,tex.EncodeToPNG());
            var posed=new SealFrame();
            foreach(var visual in visuals)
            {
                bool left=AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject))==
                    PlayerHandRig.Art+"PlayerLeftHand.prefab";
                var wrist=visual.GetTransformByHandJointId(TrackedAvatarArms.Joint(0));
                var a=visual.GetTransformByHandJointId(TrackedAvatarArms.Joint(4)).position-wrist.position;
                var b=visual.GetTransformByHandJointId(TrackedAvatarArms.Joint(13)).position-wrist.position;
                var palm=Quaternion.LookRotation(a+b,Vector3.Cross(a,b));
                var desired=Quaternion.LookRotation(new Vector3(left?.25f:-.25f,-.12f,1),left?Vector3.down:Vector3.up);
                visual.transform.rotation=desired*Quaternion.Inverse(palm)*visual.transform.rotation;
                visual.transform.position+=origin.position+new Vector3(left?-.24f:.24f,1.16f,.26f)-wrist.position;
                var h=left?posed.left:posed.right;
                h.tracked=h.fresh=h.connected=true; h.observedAt=1;
                for(int i=0;i<16;i++)
                {
                    int slot=(int)TrackedAvatarArms.Joint(i);
                    var t=visual.GetTransformByHandJointId(TrackedAvatarArms.Joint(i));
                    h.joints[slot]=new Pose(origin.InverseTransformPoint(t.position),t.rotation);
                    h.jointValid[slot]=true;
                }
                foreach(var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {r.enabled=true; r.gameObject.layer=29;}
            }
            display.GetComponent<TrackedAvatarArms>().ApplyFrame(posed,origin,1,.12f);
            foreach(var pair in rest) EdgeStrain(pair.Key,pair.Value);
            var menu=display.player.wristMenu;
            var mount=menu.playerWatchMount.transform;
            menu.wornDevice.transform.SetPositionAndRotation(mount.position,mount.rotation);
            menu.wornDevice.gameObject.SetActive(true);
            foreach(var r in menu.wornDevice.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer=29;
            camera.nearClipPlane=.03f;
            camera.transform.position=origin.position+new Vector3(0,1.63f,display.eyesAheadOfTorso);
            camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,-1,.30f));
            camera.fieldOfView=100;
            if(!string.IsNullOrEmpty(image))
            {
                camera.transform.position=origin.position+new Vector3(.12f,1.40f,.90f);
                camera.transform.LookAt(origin.position+new Vector3(0,1.18f,.24f));
                camera.fieldOfView=54;
                display.spectator=camera;
                Draw();
                File.WriteAllBytes(Path.ChangeExtension(image,null)+"RawSamePose.png",tex.EncodeToPNG());
                display.spectator=null;
                Draw();
                File.WriteAllBytes(Path.ChangeExtension(image,null)+"LocalSamePose.png",tex.EncodeToPNG());
                camera.transform.position=origin.position+new Vector3(0,1.63f,display.eyesAheadOfTorso);
                camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,-1,.30f));
                camera.fieldOfView=100;
            }
            display.spectator=null;
            var down=Draw();
            foreach(var r in display.firstPersonSkin) r.enabled=false;
            var handsOnly=Draw();
            int garment=down.Where((v,i)=>Mathf.Abs(v.r-handsOnly[i].r)+Mathf.Abs(v.g-handsOnly[i].g)+Mathf.Abs(v.b-handsOnly[i].b)>40).Count();
            Check(garment>4000,$"downward HMD-equivalent camera: {garment} torso, clothes and sleeve pixels over the hands-only baseline");
            foreach(var r in display.firstPersonSkin) r.enabled=true;
            Draw();
            if(!string.IsNullOrEmpty(image)) File.WriteAllBytes(Path.ChangeExtension(image,null)+"Down.png",tex.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null;
            RenderTexture.active=oldActive;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
            UnityEngine.Object.DestroyImmediate(origin.gameObject);
        }
    }

    static Vector3[] Baked(SkinnedMeshRenderer r)
    {
        var m=new Mesh();
        try {r.BakeMesh(m,true);return m.vertices.Select(r.transform.TransformVector).ToArray();}
        finally {UnityEngine.Object.DestroyImmediate(m);}
    }

    static void EdgeStrain(SkinnedMeshRenderer r,Vector3[] rest)
    {
        var posed=Baked(r); var edges=r.sharedMesh.triangles;
        var ratios=new List<float>(); int severe=0;
        for(int i=0;i<edges.Length;i+=3)
            for(int k=0;k<3;++k)
            {
                int a=edges[i+k],b=edges[i+(k+1)%3];
                float length=Vector3.Distance(rest[a],rest[b]);
                if(length<.004f)continue;
                float ratio=Vector3.Distance(posed[a],posed[b])/length;
                ratios.Add(ratio);
                if(ratio>2 && (ratio-1)*length>.01f) ++severe;
            }
        ratios.Sort();
        if(ratios.Count>0) Check(severe==0,$"forward wrist pose, {r.name}/{r.sharedMesh.name} P99 {ratios[(int)(ratios.Count*.99f)]:F2}x, max {ratios[^1]:F2}x, triangle edges stretched >2x and >10mm: {severe}/{ratios.Count}");
    }
}

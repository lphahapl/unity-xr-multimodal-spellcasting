using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Copy into the game's Editor assembly in an isolated checkout, then use -executeMethod PassageGlyphPlayChecks.Run.
[InitializeOnLoad]
public static class PassageGlyphPlayChecks
{
    const string Key = "NarutoVR.PassageGlyphTest";
    const string Scene = "Assets/Scene/PassageGlyphLab.unity";
    const string Report = "Logs/PassageGlyph.txt";
    static PassageGlyphPlayChecks() { EditorApplication.update += Tick; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("仅用于隔离批处理工程");
        var standalone=File.ReadAllText("Assets/XR/XRGeneralSettingsPerBuildTarget.asset").Split("m_Name: Standalone Settings")[1].Split("---")[0];
        Need(standalone.Contains("m_InitManagerOnStart: 0"),"请先在隔离副本关闭 Standalone XR 自动初始化");
        EditorSceneManager.OpenScene(Scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, "");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }

    static void Log(string s) { Debug.Log("PASSAGE " + s); File.AppendAllText(Report, s + "\n"); }
    static void Need(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    public static void CompileAndroidVariants()
    {
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Glyphs/PassageGold.shader");
        var pass=ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0);
        foreach (var stereo in new[]{"STEREO_INSTANCING_ON","STEREO_MULTIVIEW_ON"})
        foreach (var effect in new[]{"","_PASSAGE_CUE","_PASSAGE_SIGIL"})
        {
            var keywords=effect.Length>0?new[]{stereo,effect}:new[]{stereo};
            // Unity's Vulkan compiler returns both stages together under ShaderType.Vertex.
            var compiled=pass.CompileVariant(UnityEditor.Rendering.ShaderType.Vertex,keywords,UnityEditor.Rendering.ShaderCompilerPlatform.Vulkan,
                BuildTarget.Android,UnityEngine.Rendering.GraphicsTier.Tier2);
            Need(compiled.Success && compiled.ShaderData.Length>0,
                $"Vulkan {string.Join(" ",keywords)}: "+string.Join(" / ",compiled.Messages.Select(m=>m.message)));
            Debug.Log($"PASSAGE_SHADER {stereo} {(effect.Length>0?effect:"deck")} vertex+fragment {compiled.ShaderData.Length} bytes");
        }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
                throw new TimeoutException("等待隔离玩家就绪");
            if (!Application.isPlaying || Time.frameCount < 4) return;
            var displays=new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            Need(!displays.Any(x=>x.running),"灰盒 Play 要求零活动 XR display");
            var rpg = Object.FindFirstObjectByType<LinearRpgController>();
            if (!rpg || !rpg.CanAct) return;
            SessionState.EraseBool(Key);
            new Trial(rpg).Run();
            Log("PASSAGE_GLYPH_PLAY_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { SessionState.EraseBool(Key); Log("FAILED " + e); EditorApplication.Exit(1); }
    }

    sealed class Trial
    {
        readonly GlyphPenController pen;
        readonly RpgLocomotionController movement;
        readonly GlyphTemplate template;
        readonly LinearRpgController player;
        readonly PassageGlyphAnchor a, same, b, c;
        readonly Vector3 origin;
        double time = Time.realtimeSinceStartupAsDouble;
        Vector3 Eye => movement.PlayerFloorPosition + Vector3.up * 1.62f;
        Vector3 Hand => Eye + new Vector3(.17f, -.20f, .16f);
        static GameObject Preview => GameObject.Find("通路符搭路预览");
        static int Decks => Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Count(x => x.name == "通路符桥面" && x.enabled);

        public Trial(LinearRpgController rpg)
        {
            player = rpg;
            pen = rpg.GlyphPen;
            movement = rpg.locomotion;
            origin = rpg.CheckpointAt(0).transform.position;
            template = pen.activeTemplates.Single(x => x.effect is PassageGlyphEffect);
            var anchors = Object.FindObjectsByType<PassageGlyphAnchor>(FindObjectsSortMode.None);
            a = anchors.Single(x => x.name == "A"); same = anchors.Single(x => x.name == "A2");
            b = anchors.Single(x => x.name == "B"); c = anchors.Single(x => x.name == "C");
        }

        void Frame(Vector3 grip, Quaternion rotation, bool pinch = false, Vector3? aim = null, bool fist = false)
        {
            time += .018;
            pen.Observe(new PlayerHandObservation {
                headPosition = Eye, headForward = Vector3.forward, rightValid = true, rightObserved = time,
                rightWrist = grip, gripPosition = grip, gripRotation = rotation, gripPoseValid = true,
                rightTip = grip, pinchValid = true, systemPinch = true, systemPressed = pinch,
                rightPointing = aim.HasValue, rightOpen = !aim.HasValue && !fist && !pinch, rightClosed = fist, rightIndexRayValid = aim.HasValue,
                rightIndexDirection = aim.HasValue ? (aim.Value - grip).normalized : Vector3.zero
            }, time);
            pen.Advance(time);
        }

        void Ink(Vector3 xy)
        {
            var board = pen.activeBoard.transform;
            var offset = pen.penRoot.InverseTransformPoint(pen.nib.position);
            Frame(board.TransformPoint(xy) - board.rotation * offset, board.rotation);
        }

        void Draw()
        {
            var grip = pen.holster ? pen.holster.position : Eye + pen.beltOffset;
            Frame(grip, Quaternion.identity); Frame(grip, Quaternion.identity, true);
            Need(pen.IsHeld && pen.activeBoard.IsOpen, "未从上腹取笔展开主动板 " + pen.Status);
            var lift = Vector3.back * .055f;
            Ink(lift);
            foreach (var stroke in template.Preview())
            {
                Ink(stroke[0] * .115f);
                for (int i=1;i<stroke.Length;i++)
                    for (int j=1;j<=12;j++) Ink(Vector2.Lerp(stroke[i-1],stroke[i],j/12f)*.115f);
                Ink(lift);
            }
            time += pen.idleSeconds + .04;
            Ink(lift);
            Need(!pen.activeBoard.IsOpen && GameObject.Find("通路符 · 右手金光"), "停笔未辨出通路符 " + pen.Status);
        }

        void Aim(PassageGlyphAnchor anchor) => Frame(Hand, Quaternion.identity, aim: anchor.transform.position);
        void Fist() => Frame(Hand, Quaternion.identity, fist:true);
        void Cancel()
        {
            var config=(PassageGlyphEffect)template.effect;
            double end=time+config.cancelHoldSeconds+.05;
            while(time<end) Frame(Eye+Vector3.down*(config.cancelBelowHead+.10f),Quaternion.identity);
        }

        void Walk(Vector3 heading, float stopZ, int frames)
        {
            movement.SetExplorationMode(ExplorationMovement.PointWalk);
            for (int i=0;i<frames;i++)
            {
                time += .025;
                var nav = new NavigationHandInput {
                    leftValid = true, leftObserved = time, leftWrist = movement.head.position - Vector3.up * .12f,
                    leftIndex = movement.head.position + heading*.22f, leftRayOrigin = movement.head.position,
                    leftRayDirection = heading, leftRayValid = true,
                    leftPointing = i > 4, leftNavigationPointing = i > 4
                };
                movement.ProcessInput(nav, false, false, time);
                Physics.SyncTransforms();
                var z = movement.PlayerFloorPosition.z - origin.z;
                if (heading.z > 0 ? z >= stopZ : z <= stopZ) break;
            }
            for (int i = 0; i < 8; i++)
            {
                time += .025;
                movement.ProcessInput(new NavigationHandInput { leftValid = true, leftOpen = true, leftObserved = time },
                    false, false, time);
            }
            Need(!movement.NavigationActive, "松开移动手后尚未停止");
        }

        static PassageGlyphRoad CommitCheck(int prior)
        {
            var road = Object.FindObjectsByType<PassageGlyphRoad>(FindObjectsSortMode.None).Single(x => x.enabled);
            Need(road.Progress == 0 && Decks == prior && !Preview, "握拳后未从零开始架桥");
            float duration = Object.FindFirstObjectByType<GlyphPenController>().activeTemplates
                .Select(t => t.effect).OfType<PassageGlyphEffect>().Single().formationSeconds;
            road.Advance(duration * .5f);
            Need(Mathf.Abs(road.Progress - .5f) < .0001f && Decks == prior, "半程提前开放通行");
            road.Advance(duration * .5f);
            Need(Decks == prior+1 && road.Progress == 1, "成路后未开放唯一可走桥面");
            var sheet = road.GetComponentsInChildren<MeshRenderer>().Single();
            var mesh = sheet.GetComponent<MeshFilter>().sharedMesh;
            Need(sheet.enabled && mesh.GetIndexCount(0)==6 && sheet.sharedMaterial.renderQueue>=3000,
                "桥面应使用单层透明网格");
            var box = road.GetComponent<BoxCollider>();
            var contact = box.transform.TransformPoint(box.center + Vector3.up * box.size.y * .5f);
            float clearance = Vector3.Dot(sheet.bounds.center - contact, road.transform.up);
            Need(Mathf.Abs(clearance - .012f)<.0002f,"符面和可走面间距异常 "+clearance);
            return road;
        }

        // Independent background and shallow-angle camera probes on the actual completed A–B road.
        // A null graphics device can still verify mesh geometry, timing and locomotion above.
        void CheckTransparentSurface(PassageGlyphRoad road)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var sheet = road.GetComponentInChildren<MeshRenderer>();
            var camera = new GameObject("通路符材质读回").AddComponent<Camera>();
            var texture = new RenderTexture(96,96,24);
            var pixels = new Texture2D(96,96,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            var up = road.transform.up;
            var along = road.transform.forward;
            var side = road.transform.right;
            camera.enabled=false;
            camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.nearClipPlane=.009f;
            camera.farClipPlane=3;
            camera.fieldOfView=16;
            camera.aspect=1;
            camera.targetTexture=texture;
            Color Sample(Color background, bool visible)
            {
                sheet.enabled=visible;
                camera.backgroundColor=background;
                camera.Render();
                RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,96,96),0,0,false);
                Color sum=Color.black;
                foreach (var p in pixels.GetPixels(46,46,4,4)) sum+=p;
                return sum/16f;
            }
            try
            {
                var middle=sheet.bounds.center;
                camera.transform.SetPositionAndRotation(middle+up*.8f,Quaternion.LookRotation(-up,along));
                var gray=new Color(.70f,.70f,.70f,1);
                var bareDark=Sample(Color.black,false);
                var bareLight=Sample(gray,false);
                var goldDark=Sample(Color.black,true);
                var goldLight=Sample(gray,true);
                float transmitted=(goldLight.g-goldDark.g)/(bareLight.g-bareDark.g);
                Need(bareLight.g-bareDark.g>.25f && transmitted>.15f && transmitted<.99f &&
                    goldDark.r>goldDark.b+.055f && goldDark.b>bareDark.b+.04f,
                    $"半透明金色读回失败 dark={goldDark:F3}, light={goldLight:F3}, transmission={transmitted:F3}");
                float minimum=1f;
                foreach (var anchor in new[]{a,b})
                foreach (float height in new[]{.09f,.16f,.25f})
                {
                    var inward=anchor==a?along:-along;
                    // Sample exposed walking stone between the two raised corbels.
                    var seam=anchor.landing.position + inward*.065f + side*.18f + up*.012f;
                    var from=seam + inward*.8f + up*height;
                    camera.transform.SetPositionAndRotation(from,Quaternion.LookRotation(seam-from,up));
                    var bank=Sample(Color.black,false);
                    var withFilm=Sample(Color.black,true);
                    minimum=Mathf.Min(minimum,withFilm.r-bank.r);
                }
                Need(minimum>.045f, "低视角接岸处未连续读到淡金符面 "+minimum);
                Log($"单层桥面高于碰撞 12 mm；桥中亮暗背景对照 G 通道对比保留 {transmitted:P0}；两岸各三种低视角有无符面最小红通道差 {minimum:F3}。");
            }
            finally
            {
                sheet.enabled=true;
                camera.targetTexture=null;
                RenderTexture.active=previous;
                texture.Release();
                Object.Destroy(texture); Object.Destroy(pixels); Object.Destroy(camera.gameObject);
            }
        }

        public void Run()
        {
            CheckFingerRay();
            Need((movement.PlayerFloorPosition-origin).magnitude < .5f, "未在甲岸出生 " + movement.PlayerFloorPosition + " origin=" + origin);
            var story = Object.FindFirstObjectByType<GlyphStoryPanel>();
            Need(story && story.template.symbolId == "glyph.square-rule", "方直之矩剧情板引用异常");
            Physics.SyncTransforms();
            Walk(Vector3.forward, 4, 160);
            float edge = (movement.PlayerFloorPosition-origin).z;
            Need(edge > .8f && edge < 2.15f, "空隙未阻止正常行走 z="+edge+" "+movement.Status);
            Walk(Vector3.back,.25f,150);
            Log("空隙实走受阻，甲岸边缘 z="+edge.ToString("F2")+"；原剧情板仍引用方直之矩。");
            Draw();
            var beam=GameObject.Find("食指指向").GetComponent<LineRenderer>();
            var stamp=GameObject.Find("射线命中符纹").GetComponent<MeshRenderer>();
            var palm=GameObject.Find("右手符纹");
            var marks=palm.GetComponentsInChildren<MeshRenderer>();
            Need(marks.Length==2 && marks.All(m=>m.sharedMaterial.IsKeywordEnabled("_PASSAGE_SIGIL")) &&
                Vector3.Dot(marks[0].transform.forward,marks[1].transform.forward)<-.99f,"掌背双面符纹朝向异常");
            Frame(Hand, Quaternion.identity, aim: Hand+Vector3.up*5);
            Need(beam.enabled && !stamp.enabled && Vector3.Distance(Hand,beam.GetPosition(0))<.001f,"空射的指尖或落标错误");
            Fist();
            Need(pen.IsHeld && Decks==0 && !Preview,"无标记握拳误架桥");
            var art=a.GetComponentInChildren<MeshCollider>();
            Need(art && !a.GetComponent<MeshRenderer>() && art.sharedMesh.subMeshCount==3,"甲岸残留旧面片或缺少实体渡榫");
            foreach (var sockets in new[]{a,same,b,c}.GroupBy(x=>x.bank))
                Need(sockets.SelectMany(x=>x.GetComponentsInChildren<MeshCollider>())
                    .Count(x=>x.name=="通路符铜榫")==1,"单岸重复或遗漏高榫");
            var crown=art.transform.TransformPoint(new Vector3(-.35f,.575f,-.125f));
            Frame(Hand,Quaternion.identity,aim:crown);
            Need(stamp.enabled && beam.GetPosition(1).y-a.landing.position.y>.50f,"指向凸出的铜榫未命中真实模型");
            Log("三岸四座，每岸一根高铜榫；从真实铜榫横梁命中并锁定甲岸。");
            Aim(a); Aim(a);
            Need(beam.enabled && stamp.enabled &&
                Vector3.Distance(stamp.transform.position,beam.GetPosition(1))<.009f,"持续指向第一岸时光束或落标脱离接触点");
            Aim(same);
            Need(!stamp.enabled,"同岸无效标记仍亮锁定符纹");
            Fist();
            Need(Decks==0 && !Preview,"同岸误配");
            var cfg=(PassageGlyphEffect)template.effect;
            var max=cfg.maxSpan;
            try { cfg.maxSpan=2; Aim(b); Fist(); Need(Decks==0 && !Preview,"超距误配"); }
            finally { cfg.maxSpan=max; }
            var oldMarker = c.transform.position;
            try
            {
                c.transform.position = Vector3.Lerp(Hand, b.transform.position, .55f);
                Physics.SyncTransforms();
                Aim(b);
                Need(Preview && Mathf.Abs(Preview.transform.position.x -
                    (a.landing.position.x + c.landing.position.x) * .5f) < .03f,
                    "同一射线未选择最前的合法连接面");
            }
            finally { c.transform.position = oldMarker; Physics.SyncTransforms(); }
            Aim(b);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = Vector3.Lerp(Hand, b.transform.position, .55f);
                Physics.SyncTransforms(); Aim(b);
                Need(beam.enabled && !stamp.enabled &&
                    Vector3.Distance(wall.GetComponent<Collider>().ClosestPoint(beam.GetPosition(1)),beam.GetPosition(1))<.002f,
                    "遮挡墙未截断光束或错误显示命中纹");
                Fist();
                Need(Preview && Decks==0 && !Preview.GetComponent<Collider>().enabled, "新遮挡未阻止已选岸成路");
            }
            finally { wall.SetActive(false); Object.Destroy(wall); Physics.SyncTransforms(); }
            Aim(b); Need(Preview && !Preview.GetComponent<Collider>().enabled,"无合法无碰撞预览 "+pen.Status);
            Frame(Hand, Quaternion.identity, aim: Hand + Vector3.up*4);
            Need(Decks==0 && Preview && !Preview.GetComponent<Collider>().enabled,"指偏后丢失无碰撞预览");
            Aim(c); Need(Preview,"自由选丙岸失败 " + pen.Status + " 射线=" + string.Join(",", Physics.RaycastAll(Hand, (c.transform.position-Hand).normalized, 12).OrderBy(h=>h.distance).Select(h=>h.collider.name)));
            Cancel(); Need(!pen.IsHeld && !Preview && Decks==0 && !palm.activeInHierarchy,"放手未撤销手光或预览");
            Log("掌背双面符纹、指尖原点、首岸持续命中、无效面熄标、遮挡止线、收手回收通过。");
            Log("右手成符提示、空射、同岸、超距、同线首个合法标记、墙体遮挡、移开目标、放低右手取消通过。");
            Draw(); Aim(a); Aim(c); Fist(); CommitCheck(0);
            Draw(); Aim(a); Aim(b); Fist(); CheckTransparentSurface(CommitCheck(1));
            Physics.SyncTransforms();
            var middle=(a.landing.position+b.landing.position)*.5f;
            Need(Physics.Raycast(middle+Vector3.up,Vector3.down,out var floor,2) && floor.collider.name=="通路符桥面","桥中部缺少可走碰撞");
            Walk(Vector3.forward,6.35f,420);
            var finish=movement.PlayerFloorPosition;
            Need(finish.z-origin.z>=6.3f && Physics.Raycast(finish+Vector3.up,Vector3.down,out var bank,2)
                && bank.collider.transform.IsChildOf(b.bank), "实际行走未抵达乙岸 "+finish+" "+movement.Status);
            Log($"甲→丙、甲→乙各一次握拳落桥；左食指连续行走抵达乙岸 {finish:F3}，桥面采样 {floor.point:F3}，maxSpan={max} 米（试验值）。");
            Draw(); Aim(b); Aim(c);
            Need(Preview, "从新岸出发未获得乙→丙预览 " + pen.Status);
            Fist(); CommitCheck(2);
            Log("到岸后以乙为第一岸，另选丙岸，握拳建立第三条独立通路。");
        }

        void CheckFingerRay()
        {
            var lab = player.handTracking ? player.handTracking : player.skills.lab;
            var old = lab.Frame;
            var frameProperty = lab.GetType().GetProperty("Frame");
            var frame = RpgHandPoseChecks.Skeleton(Time.realtimeSinceStartupAsDouble);
            frame.source = old?.source;
            try
            {
                frameProperty.SetValue(lab, frame);
                player.Input.SampleHands(frame.time);
                var actual = player.Input.LatestHand;
                int tip = (int)Oculus.Interaction.Input.HandJointId.HandIndexTip;
                int joint = (int)Oculus.Interaction.Input.HandJointId.HandIndex2;
                Vector3 expected(Vector3 v) => lab.recordingOrigin ? lab.recordingOrigin.TransformPoint(v) : v;
                var from = expected(frame.right.joints[tip].position);
                var direction = (from - expected(frame.right.joints[joint].position)).normalized;
                Need(actual.rightIndexRayValid && Vector3.Distance(actual.rightTip, from) < .0001f &&
                    Vector3.Dot(direction, actual.rightIndexDirection) > .999f, "输入适配器未返回右食指关节射线");
                var straight = frame.right.joints[tip];
                frame.right.joints[tip].position = frame.right.joints[joint].position +
                    (frame.right.joints[joint].position - straight.position) * .6f;
                frame.right.observedAt = frame.time = Time.realtimeSinceStartupAsDouble;
                player.Input.SampleHands(frame.time);
                Need(!player.Input.LatestHand.rightIndexRayValid, "收指握拳时弯折的射线仍可覆盖已选岸");
                frame.right.joints[tip] = straight;
                frame.right.jointValid[joint] = false;
                frame.right.observedAt = frame.time = Time.realtimeSinceStartupAsDouble;
                player.Input.SampleHands(frame.time);
                Need(!player.Input.LatestHand.rightIndexRayValid, "食指缺节仍允许选面");
                Log("玩家输入适配器：指尖与射线吻合；收指握拳或丢失食指关节时，偏折的射线失效。");
            }
            finally { frameProperty.SetValue(lab, old); player.Input.SampleHands(Time.realtimeSinceStartupAsDouble); }
        }
    }
}


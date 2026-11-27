using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    // Run this from the game's Editor assembly in an isolated Unity project.
    public static class FourSkillAimPlay
    {
        const string Key="FourSkillAim.Play";
        const string Dir="Logs/FourSkillAim";
        static readonly MethodInfo tick=typeof(SkillController).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
        static readonly List<string> notes=new();
        static string Root => "Assets/ScriptableObjects/";
        [InitializeOnLoadMethod] static void Hook() => EditorApplication.update += CheckForPlay;
        public static void Run()
        {
            Directory.CreateDirectory(Dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Key,true);
            EditorApplication.EnterPlaymode();
        }
        static void CheckForPlay()
        {
            if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || !Application.isPlaying) return;
            SessionState.EraseBool(Key);
            Test();
        }
        static void Test()
        {
            int code=1;
            try{
                var skills=new[]{"技能定义配置/火球术","雷光剑配置/雷光剑","技能定义配置/龙火之术","技能定义配置/飞炎术"}
                    .Select(s=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(Root+s+".asset")).ToArray();
                Check(skills.All(s=>s && s.aimProfile && s.aimProfile.showReticle) && skills.Select(s=>s.aimProfile).Distinct().Count()==4,"四个正式 SkillDefinition 引用四个独立瞄准资产");
                foreach(var s in skills)notes.Add(s.skillId+" | "+AssetDatabase.GetAssetPath(s.aimProfile)+" | "+AssetDatabase.GetAssetPath(s.effectDefinition));
                var failures=new List<string>();
                void Probe(string name,Action check)
                {
                    try { check(); } catch(Exception ex){failures.Add(name);notes.Add("FAIL "+name+" "+ex);Debug.LogException(ex);}
                    File.WriteAllLines(Path.Combine(Dir,"checks.txt"),notes);
                }
                Probe("玩家目录与飞炎资源",()=>
                {
                    var catalog=ScriptableObject.CreateInstance<SkillCatalog>();
                    try
                    {
                        catalog.skills.AddRange(skills);
                        using var runtime=new RuntimeSkillCatalog(catalog);
                        foreach(var original in skills)
                            Check(runtime.TryGet(original.skillId,out var player) && player!=original &&
                                player.effectDefinition!=original.effectDefinition && player.aimProfile==original.aimProfile,
                                original.displayName+" 玩家运行时副本保留本术瞄准引用");
                        var core=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoFireCore.prefab");
                        var shared=AssetDatabase.LoadAssetAtPath<BreathEffectDefinition>(Root+"吐火效果配置/飞炎术效果.asset");
                        Check(core && shared && shared!=skills[3].effectDefinition && core.GetComponentsInChildren<MonoBehaviour>(true)
                            .Any(c=>c && new SerializedObject(c).FindProperty("flightEffect")?.objectReferenceValue==shared),
                            "玩家飞炎与掷火残机火核的共享飞行资源各自绑定");
                    }finally{Object.DestroyImmediate(catalog);}
                });
                Probe("候印与取消",()=>
                {
                    using var f=new BreathSkillChecks.Fixture("breath.stream");
                    f.Sample(6,near:false);f.Speak();f.Sample(4,near:false);
                    Check(f.Effects.ActivationCount==0 && Mark("落点准心"),"已认印、口令生效、尚未贴嘴时有预瞄");
                    f.HandsApart=true;f.Sample(16,near:false,seal:false);
                    Check(f.Effects.ActivationCount==0 && !Mark("落点准心"),"双腕松印取消等待，预瞄收起");
                });
                Probe("火球术",()=>TestHand(skills[0],false));
                Probe("左手火球实际发射",()=>TestHand(skills[0],false,true));
                Probe("雷光剑",()=>TestHand(skills[1],true));
                Probe("龙火之术",TestStream);
                Probe("飞炎术",BreathSkillChecks.VerifySealProjectile);
                Probe("既有回归",()=>{ThrowPresentationChecks.Run();BreathSkillChecks.Run();Check(true,"原火球瞄准与口部施法测试通过");});
                Check(failures.Count==0,"全部检查通过 "+string.Join(" / ",failures));
                notes.Add("FOUR_AIM_PLAY_PASS");code=0;
            }catch(Exception ex){notes.Add("FAIL "+ex);Debug.LogException(ex);}
            File.WriteAllLines(Path.Combine(Dir,"checks.txt"),notes);
            SessionState.EraseBool(Key);EditorApplication.Exit(code);
        }
        static void Check(bool v,string s){if(!v)throw new InvalidOperationException(s);notes.Add("PASS "+s);}
        static LineRenderer Mark(string name) => GameObject.Find(name)?.GetComponent<LineRenderer>();
        static Vector3 Center(LineRenderer r)
        {
            Vector3 sum=Vector3.zero;
            for(int i=0;i<r.positionCount;i++)sum+=r.GetPosition(i);
            return sum/r.positionCount;
        }
        static void TestHand(SkillDefinition asset,bool sword,bool leftHand=false)
        {
            var root=new GameObject("四术手部回放");root.transform.position=new Vector3(115,50,130);
            var skill=Object.Instantiate(asset);var style=Object.Instantiate(asset.aimProfile);skill.aimProfile=style;skill.leftHand=leftHand;
            var head=new GameObject("眼位").transform;head.SetParent(root.transform,false);head.localPosition=Vector3.up*1.6f;
            var target=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try{
                var effects=root.AddComponent<SkillController>();effects.head=head;effects.playerRoot=root.transform;
                double now=20;bool tracked=true, closed=false;var wrist=head.position+new Vector3(.25f,-.16f,.24f);
                var rotation=Quaternion.identity;
                effects.GameClock=effects.HandInputClock=()=>now;
                effects.WristPoseOverride=left=>tracked && left==leftHand ? new Pose(wrist,rotation):(Pose?)null;
                effects.ThrowGripProvider=(_,__)=>new ThrowGripSample{observed=now,fistValid=true,fistOpen=!closed,fistClosed=closed};
                void Tick(){now+=.025;tick.Invoke(effects,null);}
                string rootName=sword?"雷光剑 · 发波预览":"火球术 · 瞄准";
                string markName=sword?"剑波横竖预览":"落点准心";
                LineRenderer Reticle()=>GameObject.Find(rootName)?.transform.Find(markName).GetComponent<LineRenderer>();
                effects.aimDistance=2;style.gazeDistance=9;
                target.transform.position=head.position+Vector3.forward*5;target.transform.localScale=new Vector3(2,2,.12f);
                target.AddComponent<SkillTrainingTarget>();Physics.SyncTransforms();
                Check(effects.TryCast(skill),skill.displayName+" 从实际施法事务生成");
                for(int i=0;i<14;i++)Tick();
                var mark=Reticle();Check(mark&&mark.enabled,skill.displayName+" 准心已启用");
                Check(Vector3.Distance(Center(mark),head.position+head.forward*5)<.65f,skill.displayName+" 准心到靶；独立 SO 覆盖执行器 2 m 旧值");
                if(leftHand)
                {
                    closed=true;for(int i=0;i<14;i++)Tick();
                    for(int i=1;i<=18 && effects.HasHeldObject;i++){wrist+=Vector3.up*.045f;closed=i<6;Tick();}
                    Check(effects.LaunchCount==1 && !effects.HasHeldObject && !Reticle(),"左腕火球真实甩出、收起准心 "+effects.ThrowStatus);
                    Check(Vector3.Angle(effects.LastLaunchDirection,head.position+head.forward*4.94f-effects.LastLaunchPosition)<1,"实际火球从释放腕位飞向瞄准靶");
                    return;
                }
                Picture(skill.skillId,head,mark);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform.parent);
                try{
                    wall.transform.position=head.position+new Vector3(.13f,-.10f,1.4f);
                    wall.transform.localScale=new Vector3(1,1,.08f);Physics.SyncTransforms();
                    Tick();mark=Reticle();Check(mark && Center(mark).z<head.position.z+1.6f && mark.sharedMaterial.color.r>.9f,skill.displayName+" 中途墙截断并显示阻挡色");
                    wall.SetActive(false);Physics.SyncTransforms();
                    style.showReticle=false;Tick();Check(!Reticle(),skill.displayName+" 自身 SO 开关立即隐藏");
                    style.showReticle=true;tracked=false;Tick();Check(!Reticle(),skill.displayName+" 持术手失追隐藏");
                    tracked=true;for(int i=0;i<16;i++)Tick();Check(Reticle(),skill.displayName+" 恢复手追后重新瞄准");
                    if(sword){
                        for(int i=0;i<9;i++){wrist+=Vector3.right*.083f;Tick();}
                        var wave=Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).FirstOrDefault(w=>w.Strike!=null);
                        Check(wave,"实际横挥生成剑波");
                        var dir=(Vector3)typeof(SkillProjectile).GetField("direction",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wave);
                        Check(Vector3.Angle(dir,head.position+head.forward*4.94f-wave.transform.position)<1,"剑波实际飞向同一视线靶");
                    }
                    effects.CancelLatest();Check(!Reticle(),skill.displayName+" 收招当帧隐藏");
                }finally{Object.DestroyImmediate(wall);}
            }finally{Object.DestroyImmediate(root);Object.DestroyImmediate(target);Object.DestroyImmediate(style);Object.DestroyImmediate(skill);}
        }
        static void TestStream()
        {
            const string id="breath.stream";
            using var f=new BreathSkillChecks.Fixture(id);
            f.StartFromChest();f.Sample(1);
            var mark=Mark("落点准心");Check(mark && mark.transform.parent.name=="吐息 · 瞄准",id+" 口令与双手印后显示口部准心");
            Vector3 dir=f.Head.Find("龙火之术").forward;
            float range=f.Settings.range;
            Check(Vector3.Angle(Center(mark)-f.Mouth,dir)<1 && Mathf.Abs(Vector3.Distance(Center(mark),f.Mouth)-range)<.04f,id+" 实际口部方向与攻击范围一致");
            Picture(id+"-clear",f.Head,mark);
            // Put a physical wall on the attack ray; keep gaze on a different line.
            float dist=.85f;
            var wall=f.Cube(f.Mouth+dir*dist,Vector3.one*.26f);Physics.SyncTransforms();f.Sample(1);
            mark=Mark("落点准心");Check(mark&&Vector3.Distance(f.Mouth,Center(mark))<dist && mark.sharedMaterial.color.r>.9f,id+" 受墙遮挡时准心停在前方");
            Picture(id,f.Head,mark);
            wall.SetActive(false);Physics.SyncTransforms();
            {
                f.RightKnuckle=new Vector3(.10f,.045f,.12f);f.Sample(2);
                var jet=f.Head.Find("龙火之术");mark=Mark("落点准心");
                Check(mark && Vector3.Angle(Center(mark)-f.Mouth,jet.forward)<1 && Vector3.Angle(jet.forward,f.Head.forward)>12,"持续喷射准心随主导指节转向");
                f.LeftWristOnly=true;f.TipsTracked=false;f.Sample(1);Check(!Mark("落点准心"),"双手有一侧只剩腕位时立即熄灭");
                f.Effects.ClearAll();Check(!Mark("落点准心"),"龙火取消清理");
            }
        }

        static void Picture(string name,Transform eye,LineRenderer mark)
        {
            var cam=new GameObject("准心验收相机").AddComponent<Camera>();var rt=new RenderTexture(960,600,24);
            var tex=new Texture2D(960,600,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try{
                cam.enabled=false;cam.transform.SetPositionAndRotation(eye.position,eye.rotation);cam.stereoTargetEye=StereoTargetEyeMask.None;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.13f,.16f);cam.nearClipPlane=.03f;cam.fieldOfView=75;cam.aspect=1.6f;
                rt.Create();cam.targetTexture=rt;
                Color32[] Render(){cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,600),0,0);tex.Apply();return tex.GetPixels32();}
                var pixels=Render();File.WriteAllBytes(Path.Combine(Dir,name+".png"),tex.EncodeToPNG());
                mark.enabled=false;var without=Render();mark.enabled=true;
                var center=cam.WorldToScreenPoint(Center(mark));
                int changed=0;
                for(int y=Mathf.Max(0,(int)center.y-54);y<Mathf.Min(600,(int)center.y+55);y++)
                for(int x=Mathf.Max(0,(int)center.x-54);x<Mathf.Min(960,(int)center.x+55);x++){
                    var a=pixels[y*960+x];var b=without[y*960+x];if(Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>32)changed++;
                }
                Check(changed>5,name+" 单独切换准心渲染器，眼位图像产生 "+changed+" 个可见差异像素。落点 "+Center(mark).ToString("F3"));
            }finally{RenderTexture.active=old;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(cam.gameObject);}
        }
    }
}

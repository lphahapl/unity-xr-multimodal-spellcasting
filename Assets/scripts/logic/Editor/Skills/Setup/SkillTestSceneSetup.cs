using System;
using System.IO;
using System.Linq;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    /// <summary>搭建技能测试场景，提供菜单和请求文件入口执行配置、检查及状态导出。</summary>
    public static class SkillTestSceneSetup
    {
        public const string ScenePath="Assets/Scene/SkillTest.unity";
        private const string Request="Logs/skill-test.request";
        private static double nextPoll;
        [Serializable] private sealed class Report { public bool passed, hardwareTested; public string utc,scene,action,error; public int coreChecks,effectChecks,activationCount,sceneChecks,configurationChecks; }
        [Serializable] private sealed class State { public string utc,hud,voice,matching; public int voiceEvents,activations; public float shield; public Vector3 panelPosition; }
        static SkillTestSceneSetup() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            if(!File.Exists(Request)) return;
            string action=File.ReadAllText(Request).Trim(); File.Delete(Request);
            try
            {
                switch(action)
                {
                    case "stop": EditorApplication.isPlaying=false; Write(new Report{passed=true,action=action}); break;
                    case "build": BuildAndCheck(); break;
                    case "open": Open(); break;
                    case "play":
                        if(SceneManager.GetActiveScene().path!=ScenePath && !SkillShowcaseSetup.ScenePaths.Contains(SceneManager.GetActiveScene().path)) throw new Exception("必须先打开技能测试或展示场景。");
                        EditorApplication.isPlaying=true; Write(new Report{passed=true,action=action}); break;
                    case "probe": Probe(); break;
                    case "capture": Capture(); break;
                    case "layout": TuneLayout(); break;
                    case "showcase-build":
                        SkillShowcaseSetup.BuildAll();
                        int sceneCount=SkillShowcaseChecks.ValidateScenes();
                        Write(new Report{passed=true,action=action,sceneChecks=sceneCount,coreChecks=SkillCoreChecks.Run()});break;
                    case "showcase-preview": SkillShowcaseSetup.PreviewAll();Write(new Report{passed=true,action=action});break;
                    case "showcase-hud": SkillShowcaseSetup.TuneHudOrder();Write(new Report{passed=true,action=action});break;
                    case "vfx-preview": SkillVisualPreview.RenderAll();Write(new Report{passed=true,action=action});break;
                    case "vfx-velocity-repair":
                        int particleRepairs=SkillParticleVelocityRepair.RepairExistingVfxPrefabs();
                        if(SkillParticleVelocityRepair.CountInvalidEnabledVelocityModules()!=0)throw new Exception("仍有速度模式不一致的粒子模块");
                        Write(new Report{passed=true,action=action,configurationChecks=particleRepairs});break;
                    case "open-fire": SkillShowcaseSetup.OpenFireball();Write(new Report{passed=true,action=action});break;
                    case "open-shield": SkillShowcaseSetup.OpenShield();Write(new Report{passed=true,action=action});break;
                    case "open-combo": SkillShowcaseSetup.OpenCombo();Write(new Report{passed=true,action=action});break;
                    case "showcase-probe":
                        int prefabCount=SkillShowcaseChecks.ValidateRuntimePrefabs();int fallbackCount=SkillEffectChecks.Run();
                        SkillEffectChecks.Begin((passed,detail)=>Write(new Report{passed=passed,action=action,effectChecks=prefabCount+fallbackCount+2,error=passed?"":detail}));break;
                    case "shield-trial-probe":
                        SkillScenarioChecks.Begin((passed,detail)=>Write(new Report{passed=passed,action=action,sceneChecks=passed?1:0,error=passed?"":detail}));break;
                    case "architecture-migrate":
                        int migrated=SkillConfigurationSetup.Migrate();SkillConfigurationSetup.ExportCurrent();
                        Write(new Report{passed=true,action=action,sceneChecks=migrated,configurationChecks=SkillArchitectureChecks.Run(),coreChecks=SkillCoreChecks.Run()});break;
                    case "architecture-repair":
                        int repaired=SkillConfigurationSetup.RepairInitialMigration();SkillConfigurationSetup.ExportCurrent();
                        Write(new Report{passed=true,action=action,sceneChecks=repaired,configurationChecks=SkillMigrationChecks.Run()+SkillArchitectureChecks.Run(),coreChecks=SkillCoreChecks.Run()});break;
                    case "architecture-check":
                        Write(new Report{passed=true,action=action,configurationChecks=SkillMigrationChecks.Run()+SkillArchitectureChecks.Run(),coreChecks=SkillCoreChecks.Run(),sceneChecks=SkillShowcaseChecks.ValidateScenes()});break;
                    case "architecture-probe":
                        int configuration=SkillEffectConfigurationChecks.Run()+SkillArchitectureChecks.RunController();
                        int visual=SkillShowcaseChecks.ValidateRuntimePrefabs();int basic=SkillEffectChecks.Run();
                        SkillEffectChecks.Begin((passed,detail)=>Write(new Report{passed=passed,action=action,configurationChecks=configuration,effectChecks=visual+basic+2,error=passed?"":detail}));break;
                    case "voice-integration":
                        SkillVoiceIntegrationChecks.Begin((passed,detail)=>Write(new Report{passed=passed,action=action,configurationChecks=passed?1:0,error=passed?"":detail}));break;
                    default: throw new Exception("未知技能测试操作："+action);
                }
            }
            catch(Exception e){ Write(new Report{error=e.ToString(),action=action}); Debug.LogException(e); }
        }
        private static void Write(Report r)
        {
            r.utc=DateTime.UtcNow.ToString("O");r.scene=SceneManager.GetActiveScene().path;
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/skill-test-result.json",JsonUtility.ToJson(r,true));
        }
        [MenuItem("Tools/NarutoVR/技能/创建并检查技能测试场景")]
        public static void BuildAndCheck()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("请先停止播放模式再构建技能场景。");
            Directory.CreateDirectory("Assets/Scene");Directory.CreateDirectory("Assets/ScriptableObjects/技能定义配置/历史配置");Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.Refresh();
            var gesture=Profile("子印加持","buff.zi","", "子",SkillTriggerMode.GestureOnly,SkillEffectKind.SelfBuff);
            var voice=Profile("护体咒加持","buff.voice","护体咒","",SkillTriggerMode.VoiceOnly,SkillEffectKind.SelfBuff);
            var fire=Profile("火球术","fireball","火球术","寅",SkillTriggerMode.VoiceAndGesture,SkillEffectKind.HeldFireball);
            var fireMat=Material("火球橙色",new Color(1,.2f,.025f));
            var buffMat=Material("加持青色",new Color(.1f,.9f,1));
            var targetMat=Material("训练靶蓝色",new Color(.15f,.35f,.65f));
            if(!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(NarutoVR.Editor.SealSteamLinkSetup.ScenePath,ScenePath)) throw new Exception("复制输入场景失败。");
            var previous=SceneManager.GetActiveScene(); var scene=SceneManager.GetSceneByPath(ScenePath);
            bool opened=scene.IsValid()&&scene.isLoaded;
            if(!opened) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                var lab=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Lab>(true)).Single();
                var camera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).Single(c=>c.enabled);
                var controller=lab.GetComponent<SkillTestController>();
                if(!controller)
                {
                    // Only change the newly copied scene. The recording and recognition scenes remain intact.
                    lab.gameObject.name="技能训练台";lab.recordOnStart=false;lab.SetTestMode(true);
                    var oldTest=lab.GetComponent<SealRecognitionTest>(); if(oldTest) UnityEngine.Object.DestroyImmediate(oldTest);
                    var view=lab.GetComponent<SealLabView>();
                    if(view && view.statusText){UnityEngine.Object.DestroyImmediate(view.statusText.canvas.gameObject);view.statusText=null;}
                    var effects=lab.gameObject.AddComponent<SkillEffects>(); effects.lab=lab;effects.head=camera.transform;effects.playerRoot=camera.transform.root;effects.fireMaterial=fireMat;effects.buffMaterial=buffMat;
                    var receiver=lab.gameObject.AddComponent<SkillVoiceReceiver>();
                    controller=lab.gameObject.AddComponent<SkillTestController>();controller.lab=lab;controller.effects=effects;controller.voice=receiver;
                    controller.skills.AddRange(new[]{gesture,voice,fire});
                    controller.chineseFont=AssetDatabase.LoadAssetAtPath<Font>(NarutoVR.Editor.SealChineseUISetup.FontPath);
                    var panel=new GameObject("技能状态中文面板",typeof(RectTransform),typeof(Canvas));
                    panel.transform.SetParent(camera.transform,false);panel.transform.localPosition=new Vector3(-.66f,.1f,1.45f);panel.transform.localScale=Vector3.one*.0012f;
                    panel.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
                    panel.GetComponent<RectTransform>().sizeDelta=new Vector2(900,900);
                    var bg=new GameObject("面板底色",typeof(RectTransform),typeof(Image));bg.transform.SetParent(panel.transform,false);Stretch(bg.GetComponent<RectTransform>(),0);
                    bg.GetComponent<Image>().color=new Color(.025f,.04f,.07f,.85f);bg.GetComponent<Image>().raycastTarget=false;
                    var text=new GameObject("中文技能状态",typeof(RectTransform),typeof(Text));text.transform.SetParent(panel.transform,false);Stretch(text.GetComponent<RectTransform>(),20);
                    controller.statusText=text.GetComponent<Text>();controller.statusText.font=controller.chineseFont;controller.statusText.fontSize=30;controller.statusText.color=Color.white;controller.statusText.raycastTarget=false;
                    controller.statusText.text="技能训练场\n子印：加持\n护体咒：加持\n火球术＋寅印：右手火球\n真实语音需启动本地服务。";
                    var dot=new GameObject("头向瞄准点",typeof(RectTransform),typeof(Text));dot.transform.SetParent(panel.transform,false);
                    dot.GetComponent<RectTransform>().anchoredPosition=new Vector2(550,-83.3333f);dot.GetComponent<RectTransform>().sizeDelta=new Vector2(80,80);
                    var dt=dot.GetComponent<Text>();dt.font=controller.chineseFont;dt.fontSize=36;dt.text="＋";dt.color=Color.white;dt.raycastTarget=false;dt.alignment=TextAnchor.MiddleCenter;
                    for(int i=0;i<3;i++)
                    {
                        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="训练靶"+(i+1);SceneManager.MoveGameObjectToScene(go,scene);
                        go.transform.position=new Vector3((i-1)*1.3f,1.4f,4+i*.35f);go.transform.localScale=new Vector3(.65f,.9f,.35f);
                        go.GetComponent<Renderer>().sharedMaterial=targetMat;go.AddComponent<SkillTrainingTarget>();
                    }
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                if(SkillConfigurationSetup.Bind(controller)){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                if(!controller.lab || !controller.effects || !controller.voice || !controller.statusText || controller.ConfiguredSkills.Count==0) throw new Exception("技能场景配置不完整。");
                NarutoVR.Editor.SealLabChecks.Run();int count=SkillCoreChecks.Run(); CheckPush();
                Write(new Report{passed=true,action="build",coreChecks=count});
                Debug.Log($"[技能测试] 场景创建完成；核心检查{count}项及既有骨骼检查通过。");
            }
            finally {if(!opened) EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        private static void Stretch(RectTransform r,float pad){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.one*pad;r.offsetMax=Vector2.one*(-pad);}
        private static void CheckPush()
        {
            if(!SkillEffects.IsForwardPush(Vector3.zero,Vector3.forward*.15f,Vector3.zero,Vector3.zero,Vector3.forward,.3f))throw new Exception("有效前推被拒绝");
            if(SkillEffects.IsForwardPush(Vector3.zero,Vector3.zero,Vector3.zero,Vector3.back*.2f,Vector3.forward,.3f))throw new Exception("后仰误触发前推");
            if(SkillEffects.IsForwardPush(Vector3.zero,Vector3.forward*.2f,Vector3.zero,Vector3.forward*.2f,Vector3.forward,.3f))throw new Exception("整体走动误触发前推");
            if(SkillEffects.IsForwardPush(Vector3.zero,Vector3.forward*.2f,Vector3.zero,Vector3.zero,Vector3.forward,.02f))throw new Exception("瞬时跳变误触发前推");
        }
        private static SkillDefinition Profile(string name,string id,string phrase,string seal,SkillTriggerMode mode,SkillEffectKind effect)
        {
            string path="Assets/ScriptableObjects/技能定义配置/历史配置/"+name+".asset";var p=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);if(p)return p;
            p=ScriptableObject.CreateInstance<SkillDefinition>();p.skillId=id;p.displayName=name;p.phrase=phrase;p.sealName=seal;p.triggerMode=mode;p.effectKind=effect;
            AssetDatabase.CreateAsset(p,path);return p;
        }
        private static Material Material(string name,Color color)
        {
            string path="Assets/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");
            m=new Material(shader){name=name,color=color};AssetDatabase.CreateAsset(m,path);return m;
        }
        [MenuItem("Tools/NarutoVR/技能/打开技能测试场景")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("先停止播放模式。");
            if(SceneManager.GetActiveScene().isDirty) throw new Exception("当前场景有未保存修改，请先保存；技能场景已独立生成。");
            if(!File.Exists(ScenePath)) BuildAndCheck();EditorSceneManager.OpenScene(ScenePath);
        }
        private static void Probe()
        {
            if(!EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath) throw new Exception("需要在技能场景播放模式中执行运行检查。");
            var c=UnityEngine.Object.FindFirstObjectByType<SkillTestController>();
            if(c==null||c.Engine==null)throw new Exception("技能控制器未启动。");
            c.Cancel();int before=c.effects.ActivationCount;
            c.ReceiveVoice("护体咒",Time.realtimeSinceStartupAsDouble);
            if(c.effects.ActivationCount!=before+1 || c.effects.BuffRemaining<=0)throw new Exception("纯语音加持未实际生效。");
            if(c.effects.AbsorbDamage(30)!=0 || Math.Abs(c.effects.ShieldPoints-70)>0.01f)throw new Exception("护盾没有真实吸收伤害。");
            c.Cancel();if(c.effects.BuffRemaining>0)throw new Exception("取消没有结束加持。");
            int count=SkillEffectChecks.Run();
            SkillEffectChecks.Begin((passed,detail)=>Write(new Report{passed=passed,action="probe",effectChecks=count+2,activationCount=c.effects.ActivationCount,error=passed?"":detail}));
        }
        private static void Capture()
        {
            var c=UnityEngine.Object.FindFirstObjectByType<SkillTestController>();
            if(!c || c.Engine==null)throw new Exception("请先播放技能测试场景。");
            if(c.view&&c.view.statusText)c.view.statusText.text=c.view.BuildHUD(Time.realtimeSinceStartupAsDouble);
            File.WriteAllText("Logs/skill-runtime-state.json",JsonUtility.ToJson(new State{utc=DateTime.UtcNow.ToString("O"),hud=c.statusText.text,voice=c.voice.Status,matching=c.Engine.Status,voiceEvents=c.VoiceEventCount,activations=c.effects.ActivationCount,shield=c.effects.ShieldPoints,panelPosition=c.statusText.canvas.transform.localPosition},true));
            // Render our game scene to an artifact; this does not capture or operate desktop UI.
            var go=new GameObject("技能界面验收相机");var cam=go.AddComponent<Camera>();
            var rt=new RenderTexture(1600,900,24);var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                cam.CopyFrom(Camera.main);cam.enabled=false;cam.stereoTargetEye=StereoTargetEyeMask.None;
                cam.transform.SetPositionAndRotation(Camera.main.transform.position,Camera.main.transform.rotation);cam.ResetWorldToCameraMatrix();cam.ResetProjectionMatrix();cam.fieldOfView=60;cam.aspect=1600f/900;cam.targetTexture=rt;
                cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes("Logs/skill-scene-preview.png",pixels.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(pixels);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            Write(new Report{passed=true,action="capture",activationCount=c.effects.ActivationCount});
        }
        private static void TuneLayout()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("布局操作需要技能测试场景处于编辑模式。");
            var c=UnityEngine.Object.FindFirstObjectByType<SkillTestController>();var panel=c.statusText.canvas.GetComponent<RectTransform>();
            panel.localScale=Vector3.one*.0012f;panel.sizeDelta=new Vector2(900,900);panel.localPosition=new Vector3(-.66f,.1f,1.45f);c.statusText.fontSize=30;
            var dot=panel.Find("头向瞄准点").GetComponent<RectTransform>();dot.anchoredPosition=new Vector2(550,-83.3333f);dot.sizeDelta=new Vector2(80,80);dot.GetComponent<Text>().alignment=TextAnchor.MiddleCenter;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Write(new Report{passed=true,action="layout"});
        }
    }
}

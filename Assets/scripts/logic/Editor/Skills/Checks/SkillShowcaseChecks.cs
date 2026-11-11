using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lab=NarutoVR.SealLab.SealLab;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>检查展示场景、视觉预置体和组件接线是否符合当前技能配置。</summary>
    public static class SkillShowcaseChecks
    {
        public static int ValidateScenes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("场景检查需要编辑模式。");
            int count=0;var previous=SceneManager.GetActiveScene();
            foreach(string path in SkillShowcaseSetup.ScenePaths)
            {
                if(!File.Exists(path))throw new Exception("缺少展示场景："+path);
                var scene=SceneManager.GetSceneByPath(path);bool wasOpen=scene.IsValid()&&scene.isLoaded;
                if(!wasOpen)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var c=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkillInputController>(true)).Single();
                    Require(c.lab&&c.voice&&c.effects&&c.statusText&&c.chineseFont,"技能绑定或中文字体缺失");
                    Require(c.ConfiguredSkills.Count>0 && c.ConfiguredSkills.All(s=>s && s.ValidateDefinition()==null),"技能目录配置无效");
                    SkillConfigurationSetup.ValidateController(c);
                    Require(c.effects.fireballPrefab&&c.effects.shieldPrefab&&c.effects.impactPrefab,"特效预置体没有绑定");
                    Require(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkillScenario>(true)).Count()==1,"场景引导必须唯一");
                    Require(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkillTrainingTarget>(true)).Any(),"场景缺少可命中的训练靶");
                    foreach(var p in new[]{c.effects.fireballPrefab,c.effects.shieldPrefab,c.effects.impactPrefab})
                    {
                        Require(p.GetComponentsInChildren<Collider>(true).Length==0,"视觉预置体不应带伤害碰撞体："+p.name);
                        foreach(var renderer in p.GetComponentsInChildren<Renderer>(true))
                            foreach(var mat in renderer.sharedMaterials)
                                Require(mat && mat.shader && !ShaderUtil.ShaderHasError(mat.shader),"特效材质或Shader编译无效："+p.name);
                    }
                    count++;
                }
                finally {if(!wasOpen)EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
            }
            return count;
        }
        public static int ValidateRuntimePrefabs()
        {
            if(!EditorApplication.isPlaying)throw new Exception("预置体运行检查需要播放模式。");
            GameObject root=null,held=null,shield=null,targetObject=null,projectileObject=null,impact=null,inactiveTemplate=null,secondProjectile=null,secondImpact=null;SkillDefinition fire=null,buff=null;
            try
            {
                root=new GameObject("预置体验收_临时玩家");root.transform.position=new Vector3(150,100,100);
                var lab=root.AddComponent<Lab>();lab.recordingOrigin=root.transform;
                var frame=new SealFrame{time=Time.realtimeSinceStartupAsDouble};
                foreach(var hand in new[]{frame.left,frame.right})
                {
                    hand.connected=hand.tracked=hand.fresh=true;hand.observedAt=frame.time;
                    hand.jointValid[(int)HandJointId.HandWristRoot]=true;
                    hand.joints[(int)HandJointId.HandWristRoot]=new Pose(new Vector3(.3f,1.2f,.4f),Quaternion.identity);
                }
                typeof(Lab).GetProperty("Frame").SetValue(lab,frame);
                var effects=root.AddComponent<SkillController>();effects.lab=lab;effects.head=root.transform;effects.playerRoot=root.transform;
                effects.fireballPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.FireballPath);
                effects.shieldPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.ShieldPath);
                effects.impactPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.ImpactPath);
                fire=ScriptableObject.CreateInstance<SkillDefinition>(); fire.skillId = "fixture.skillshowcasechecks.fire"; fire.cooldownSeconds = 0;fire.effectKind=SkillEffectKind.HeldFireball;fire.displayName="预置体火球检查";
                buff=ScriptableObject.CreateInstance<SkillDefinition>(); buff.skillId = "fixture.skillshowcasechecks.buff"; buff.cooldownSeconds = 0;buff.effectKind=SkillEffectKind.SelfBuff;buff.displayName="预置体护盾检查";
                Require(effects.TryCast(fire),"真实火球预置体未生成");
                held=(GameObject)UnityEditor.EditorUtility.EntityIdToObject(effects.HeldProjectileInstanceId);
                Require(held&&held.GetComponentsInChildren<Renderer>(true).Length>1,"华丽火球缺少层次表现");
                var motion=held.GetComponent<SkillVisualMotion>();Require(motion && motion.flightTrails.Length>0,"火球缺少尾迹绑定");
                Require(motion.flightTrails.All(t=>!t.emitting),"持握时不应发出飞行尾迹");
                motion.SetFlying(true);Require(motion.flightTrails.All(t=>t.emitting),"飞行视觉没有开启尾迹");motion.SetFlying(false);
                Require(!effects.TryCast(fire),"真实预置体路径未限制手槽");
                Require(effects.TryCast(buff),"真实护盾预置体未生成");
                shield=(GameObject)typeof(SkillController).GetField("buffObject",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(effects);
                Require(shield&&shield.GetComponentsInChildren<Renderer>(true).Length>0,"护盾预置体缺少表现");
                Require(effects.AbsorbPracticeDamage(30)==0&&Mathf.Abs(effects.ShieldPoints-70)<.01f,"预置体改变了护盾数值行为");
                effects.CancelLatest();Require(!effects.HasHeldObject&&effects.ShieldPoints==70,"取消持握错误地清掉护盾");
                effects.CancelLatest();Require(effects.ShieldPoints==0,"预置体护盾取消失败");
                targetObject=GameObject.CreatePrimitive(PrimitiveType.Cube);targetObject.transform.position=root.transform.position+Vector3.forward*10;
                var target=targetObject.AddComponent<SkillTrainingTarget>();Physics.SyncTransforms();
                projectileObject=new GameObject("爆散验收_投射物");projectileObject.transform.position=targetObject.transform.position;
                var projectile=projectileObject.AddComponent<SkillProjectile>();projectile.impactPrefab=effects.impactPrefab;
                int impacts=0;projectile.ImpactCreated=v=>{impact=v;impacts++;};
                projectile.Initialize(Vector3.forward,root.transform,~0,0,null,null);
                Require(impacts==1&&impact&&target.CurrentHealth==75,"命中必须扣一次伤害并创建一次爆散");
                projectile.Finish();Require(impacts==1,"结束重复生成命中爆散");
                var lifetime=impact.GetComponent<SkillVisualLifetime>();Require(lifetime,"爆散没有清理生命周期");lifetime.Finish();Require(!impact.activeSelf,"爆散结束没有停止显示");
                inactiveTemplate=UnityEngine.Object.Instantiate(effects.impactPrefab);
                inactiveTemplate.SetActive(false);inactiveTemplate.GetComponent<SkillVisualLifetime>().enabled=false;
                secondProjectile=new GameObject("停用预置体的爆散验收");secondProjectile.transform.position=targetObject.transform.position;
                var retry=secondProjectile.AddComponent<SkillProjectile>();retry.impactPrefab=inactiveTemplate;
                retry.ImpactCreated=v=>secondImpact=v;retry.Initialize(Vector3.forward,root.transform,~0,0,null,null);
                Require(secondImpact&&secondImpact.activeSelf&&secondImpact.GetComponent<SkillVisualLifetime>().isActiveAndEnabled,"停用的爆散模板必须恢复显示及自动清理");
                Require(target.CurrentHealth==50,"停用模板不应改变一次命中的伤害");
                return 9;
            }
            finally
            {
                if(root)UnityEngine.Object.DestroyImmediate(root);if(held)UnityEngine.Object.DestroyImmediate(held);if(shield)UnityEngine.Object.DestroyImmediate(shield);
                if(targetObject)UnityEngine.Object.DestroyImmediate(targetObject);if(projectileObject)UnityEngine.Object.DestroyImmediate(projectileObject);if(impact)UnityEngine.Object.DestroyImmediate(impact);
                if(inactiveTemplate)UnityEngine.Object.DestroyImmediate(inactiveTemplate);if(secondProjectile)UnityEngine.Object.DestroyImmediate(secondProjectile);if(secondImpact)UnityEngine.Object.DestroyImmediate(secondImpact);
                if(fire)UnityEngine.Object.DestroyImmediate(fire);if(buff)UnityEngine.Object.DestroyImmediate(buff);
            }
        }
        private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    }
}

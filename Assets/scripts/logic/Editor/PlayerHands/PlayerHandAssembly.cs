using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealLab;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.Editor
{
    internal static class PlayerHandAssembly
    {
        internal static GameObject Build(GameObject master, GameObject model, Material body, Material end,
            WatchMountProfile profile, string output)
        {
            string side=PlayerHandRig.Side(master);
            var errors=PlayerHandRig.Compare(master,model);
            if (errors.Count!=0) throw new InvalidOperationException(string.Join("\n",errors));
            var source=model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int slots=source.sharedMesh.subMeshCount;
            if (slots<1 || slots>2 || !body || body.renderQueue>2500 || slots==2 && (!end || end.renderQueue>2500))
                throw new ArgumentException("网格须为主体或主体＋末段两槽，供给写深度的不透明材质。");
            if (side=="Left" && !profile) throw new ArgumentException("请选择左腕准望匣佩戴配置。");
            if (!output.StartsWith("Assets/",StringComparison.Ordinal) || !output.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase) ||
                output.Split('/').Contains("..")) throw new ArgumentException("输出须在 Assets 内。");
            var template=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHandRig.Visual(side));
            if (!template) throw new FileNotFoundException("安装的 SDK 缺少官方手部 Prefab",PlayerHandRig.Visual(side));
            var prior=AssetDatabase.LoadAssetAtPath<GameObject>(output);
            if (prior && PrefabUtility.GetCorrespondingObjectFromOriginalSource(prior)!=template)
                throw new ArgumentException("输出位置已存在其他 Prefab。");
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(template,preview);
                var visual=instance.GetComponent<HandVisual>();
                var settings=new SerializedObject(visual);
                var target=(SkinnedMeshRenderer)settings.FindProperty("_openXRSkinnedMeshRenderer").objectReferenceValue;
                var joints=PlayerHandRig.Joints(instance,errors);
                if (errors.Count!=0 || !target || target.rootBone!=joints[PlayerHandRig.Wrist])
                    throw new InvalidOperationException("官方手模显示层级或腕骨引用已改变。");
                // Each bindpose belongs to THIS mesh's bone index, independently of SDK joint enum order.
                var mapped=source.bones.Select(t=>joints[t.name]).ToArray();
                target.sharedMesh=source.sharedMesh;
                target.bones=mapped;
                target.rootBone=joints[PlayerHandRig.Wrist];
                target.localBounds=source.localBounds;
                target.sharedMaterials=slots==1 ? new[]{body} : new[]{body,end};
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                if (side=="Left")
                {
                    var wrist=target.rootBone;
                    var mount=new GameObject("WatchMount").AddComponent<WatchMount>();
                    mount.transform.SetParent(wrist,false);
                    var index=joints["XRHand_IndexProximal"].position-wrist.position;
                    var little=joints["XRHand_LittleProximal"].position-wrist.position;
                    mount.wristToPalm=Quaternion.Inverse(wrist.rotation)*Quaternion.LookRotation((index+little)*.5f,-Vector3.Cross(index,little));
                    mount.profile=profile;
                    mount.gaze=mount.gameObject.AddComponent<WatchGazeActivator>();
                    mount.ApplyProfile();
                }
                var asset=PrefabUtility.SaveAsPrefabAsset(instance,output,out bool saved);
                if (!saved || !asset) throw new IOException("生成玩家手 Prefab 失败 " + output);
                return asset;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static LinearRpgController Player(Scene scene)
        {
            if (Application.isPlaying || !scene.isLoaded || (scene.path!="Assets/Scene/Chapter01_Courtyard.unity" &&
                scene.path!="Assets/Scene/Chapter01_Awakening.unity"))
                throw new InvalidOperationException("请在编辑模式打开首章院落或苏醒场景。");
            return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LinearRpgController>(true)).Single();
        }

        internal static void BoneLines(Scene scene,bool enabled)
        {
            var view=Player(scene).skills.lab.GetComponent<SealLabView>();
            Undo.RecordObject(view,"玩家追踪骨骼线");
            view.showBoneLines=enabled;
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        internal static void Install(Scene scene,GameObject left,GameObject right)
        {
            var player=Player(scene);
            var input=player.skills.lab.xrHandsSource;
            var menu=player.wristMenu;
            if (!input || !input.IsConfigured || !menu || !menu.wornDevice || !player.locomotion.head)
                throw new InvalidOperationException("缺少首章左右手追踪源、头显或已佩戴准望匣。");
            var requested=new[]{left,right};
            var masterPrefabs=new[]{"Left","Right"}.Select(s=>AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHandRig.Visual(s))).ToArray();
            for(int i=0;i<2;i++)
                if (!requested[i] || !EditorUtility.IsPersistent(requested[i]) || !requested[i].GetComponent<HandVisual>() ||
                    (requested[i]!=masterPrefabs[i] && PrefabUtility.GetCorrespondingObjectFromOriginalSource(requested[i])!=masterPrefabs[i]))
                    throw new ArgumentException("请选择与对应侧一致的官方手或由其生成的玩家 Prefab。");

            var existing=input.GetComponentsInChildren<HandVisual>(true);
            foreach(var h in existing)
                if(h.transform.parent!=input.transform || !masterPrefabs.Contains(PrefabUtility.GetCorrespondingObjectFromOriginalSource(h.gameObject)))
                    throw new InvalidOperationException("玩家数据源下存在来源不明的 HandVisual，请先核对 " + h.name);
            Undo.IncrementCurrentGroup();
            int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("切换首章玩家手与准望匣");
            try
            {
                for(int i=0;i<2;i++)
                {
                    var matches=existing.Where(h=>h && PrefabUtility.GetCorrespondingObjectFromOriginalSource(h.gameObject)==masterPrefabs[i]).ToArray();
                    var visual=matches.FirstOrDefault(h=>PrefabUtility.GetCorrespondingObjectFromSource(h.gameObject)==requested[i]);
                    if (!visual)
                    {
                        var obj=(GameObject)PrefabUtility.InstantiatePrefab(requested[i],input.transform);
                        Undo.RegisterCreatedObjectUndo(obj,"安装追踪手");
                        visual=obj.GetComponent<HandVisual>();
                    }
                    Undo.RecordObject(visual,"连接真实手源");
                    visual.InjectHand(i==0 ? input.leftHand : input.rightHand);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                    if (i==0)
                    {
                        Undo.RecordObject(menu,"绑定已有准望匣");
                        menu.playerWatchMount=visual.GetComponentInChildren<WatchMount>(true);
                        if (menu.playerWatchMount && menu.playerWatchMount.gaze)
                        {
                            var gaze=menu.playerWatchMount.gaze;
                            Undo.RecordObject(gaze,"绑定头显");
                            gaze.viewer=player.locomotion.head;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(gaze);
                        }
                        PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
                    }
                    foreach(var old in matches) if (old!=visual) Undo.DestroyObjectImmediate(old.gameObject);
                }
                BoneLines(scene,false);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
    }
}

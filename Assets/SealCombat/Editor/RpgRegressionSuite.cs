#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class RpgRegressionSuite
    {
        [Serializable] sealed class Group { public string name, error; public int checks; public bool passed; }
        [Serializable] sealed class Report { public string utc; public bool passed, hardwareTested; public int total; public List<Group> groups = new(); }
        [MenuItem("Tools/NarutoVR/线性RPG/运行完整编辑模式回归")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new Exception("完整回归需要已保存的编辑模式场景");
            if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(LinearRpgSceneSetup.ScenePath);
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            void Group(string name, Func<int> run)
            {
                var group = new Group { name = name }; report.groups.Add(group);
                try { group.checks = run(); group.passed = true; report.total += group.checks; }
                catch (Exception e) { group.error = e.ToString(); }
            }
            Group("骨骼→官方手形→游戏操作", RpgHandPoseChecks.Run);
            Group("麦克风遥测", VoiceDiagnosticsChecks.Run);
            Group("施法链日志与故障隔离", SkillCastTraceChecks.Run);
            Group("激活策略接口行为等价", SkillActivationPolicyChecks.Run);
            Group("独立储存超时与单发限制", ProjectileStorageChecks.Run);
            Group("射线直接选点", RayNavigationChecks.Run);
            Group("玩家数值与存档", RpgDataChecks.Run);
            Group("导航、闪避、碰撞与时钟", RpgLocomotionChecks.Run);
            Group("四向手势与结印防误触", RpgDirectionalInteractionChecks.Run);
            Group("导航融合评分与实际碰撞", NavigationScoringChecks.Run);
            Group("握持松手投掷", HandThrowChecks.Run);
            Group("评分发射、伸手保持与日志回读", ScoredThrowChecks.Run);
            Group("护盾腕饰追踪丢失表现", ThrowPresentationChecks.Run);
            Group("口令队列与输入时钟", SkillClockRegressionChecks.Run);
            Group("导航平滑与驻留暂停", InteractionFeelChecks.Run);
            Group("详细调试与音画反馈", InteractionFeelPresentationChecks.Run);
            Group("腕饰菜单、射线点按与独立面板策略", WristMenuChecks.Run);
            Group("抬腕易用性与单手交接", WristMenuEaseChecks.Run);
            Group("点腕开关、UI仲裁与轻捏确认", WristTapChecks.Run);
            Group("弯指丢射线原问题", WristRayIssueProbe.Run);
            Group("当前火球配置的小幅投掷", ThrowEaseChecks.Run);
            Group("腕饰生命周期与重载清理", WristViewLifecycleChecks.Run);
            Group("技能核心匹配与取消", SkillCoreChecks.Run);
            Group("可扩展配置架构", SkillArchitectureChecks.Run);
            Group("旧配置迁移", SkillMigrationChecks.Run);
            Group("三个展示场景与特效接线", SkillShowcaseChecks.ValidateScenes);
            Group("结印评分、录制与回放", () =>
            {
                NarutoVR.Editor.SealLabChecks.Run();
                var json = File.ReadAllText("Logs/seal-lab-checks.json");
                return JsonUtility.FromJson<SealReport>(json).checks.Length;
            });
            Group("RPG场景官方配置与旧字段清除", () =>
            {
                var scene = SceneManager.GetSceneByPath(LinearRpgSceneSetup.ScenePath);
                if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(LinearRpgSceneSetup.ScenePath);
                var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Rpg.LinearRpgController>(true)).Single();
                if (!controller.handPoseSettings || controller.handPoseSettings.Validate() != null) throw new Exception("官方手形配置接线失败");
                if(!controller.locomotion.useRayNavigation) throw new Exception("实际RPG场景未启用射线直接选点");
                string raw = File.ReadAllText(LinearRpgSceneSetup.ScenePath);
                if (raw.Contains("straightCurl:") || raw.Contains("foldedCurl:")) throw new Exception("场景残留旧阈值");
                return 1;
            });
            Group("完整检查结束无腕部UI残留", WristViewLifecycleChecks.RequireNoOrphans);
            report.passed = report.groups.All(g => g.passed);
            Directory.CreateDirectory("Logs/official-hand-poses");
            File.WriteAllText("Logs/official-hand-poses/editor-suite.json", JsonUtility.ToJson(report, true));
            if (!report.passed) throw new Exception("编辑模式回归有失败项，见Logs/official-hand-poses/editor-suite.json");
            Debug.Log($"[官方手形回归] 编辑模式{report.total}项通过；不代表真机精度。");
        }
        [Serializable] sealed class SealReport { public string[] checks; }
    }
}
#endif

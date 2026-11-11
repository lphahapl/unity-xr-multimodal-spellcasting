#if UNITY_EDITOR
using System;
using System.Reflection;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Read-only debug surfaces and presentation lifecycle, using edit-mode temporary objects.</summary>
    public static class InteractionFeelPresentationChecks
    {
        public static int Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("交互表现检查需要编辑模式");
            var root = new GameObject("交互表现回归_临时实体") { hideFlags = HideFlags.HideAndDontSave };
            RpgInteractionFeedback feedback = null;
            int count = 0;
            try
            {
                var view = root.AddComponent<RpgSessionView>();
                Require(view.BuildHud().Contains("等待绑定") && view.BuildDesktopDebug(10).Contains("【投掷输入与拟合】"),
                    "未接线调试面板必须提供缺失信息，不能异常"); count++;
                var controller = root.AddComponent<LinearRpgController>();
                controller.chapter = root.AddComponent<TrainingChapterFlow>();
                ((TrainingChapterFlow)controller.chapter).rpg = controller; ((TrainingChapterFlow)controller.chapter).initialSkills = new[]{"fireball"};
                var movement = root.AddComponent<RpgLocomotionController>();
                var skills = root.AddComponent<SkillInputController>();
                var effects = root.AddComponent<SkillController>();
                controller.locomotion = movement; controller.skills = skills; skills.effects = effects;
                view.controller = controller;
                Vector3 before = root.transform.position;
                string hud = view.BuildHud(), debug = view.BuildDesktopDebug(10);
                Require(debug.Contains("【运行状态】") && debug.Contains("【移动输入与选点】") && debug.Contains("【投掷输入与拟合】") &&
                    !debug.Contains("NaN") && !debug.Contains("Infinity"), "无真实观测时三分区必须清晰且无非有限数字"); count++;
                Require(hud.Length < debug.Length && !hud.Contains("【投掷输入与拟合】") && root.transform.position == before &&
                    !effects.HasHeldObject && !movement.CandidateNode, "精简世界HUD与详细面板均不能生成技能或移动玩家"); count++;

                feedback = root.AddComponent<RpgInteractionFeedback>(); feedback.controller = controller;
                feedback.playSounds = false;
                Call(feedback, "OnEnable");
                int renderers = root.GetComponentsInChildren<LineRenderer>(true).Length;
                Require(renderers >= 8, "反馈创建必须包含地面路径、手边圈与投掷短线"); count++;
                feedback.Present(RpgActionCue.ThrowGripReady);
                feedback.Present(RpgActionCue.ThrowGripReady);
                feedback.Present(RpgActionCue.DwellNearComplete);
                Require(feedback.GripCueCount == 1 && feedback.DwellCueCount == 1 && feedback.SoundCount == 0,
                    "握持提示不能重复刷出；独立驻留提示可通过且静音必须有效"); count++;
                Call(feedback, "RefreshVisuals", true);
                Require(!feedback.GripVisualVisible && !feedback.ThrowDirectionVisible && !effects.HasHeldObject,
                    "没有真实投掷输入时不能在世界原点显示假握持圈或方向线"); count++;
                Call(feedback, "OnDisable"); Call(feedback, "OnEnable");
                Require(root.GetComponentsInChildren<LineRenderer>(true).Length == renderers,
                    "表现重新启用应复用资源，不能重复堆积线条"); count++;
                return count;
            }
            finally
            {
                // Presentation normally owns play-mode Destroy. Dispose its temporary resources now
                // so edit-mode cleanup does not queue runtime destruction or touch shared assets.
                if (feedback)
                {
                    Call(feedback, "OnDisable");
                    foreach (string field in new[] { "pathMaterial", "progressMaterial", "pulseMaterial", "throwMaterial" })
                        if (GetField(feedback, field) is Object resource && resource) Object.DestroyImmediate(resource);
                    if (GetField(feedback, "generated") is AudioClip[] clips)
                        foreach (var clip in clips) if (clip) Object.DestroyImmediate(clip);
                    if (GetField(feedback, "visuals") is Transform visuals && visuals) Object.DestroyImmediate(visuals.gameObject);
                    if (GetField(feedback, "speaker") is AudioSource speaker && speaker) Object.DestroyImmediate(speaker);
                }
                if (root) Object.DestroyImmediate(root);
            }
        }

        static object GetField(object target, string field) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static void Call(object target, string method, params object[] args)
        {
            try { target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        static void Require(bool success, string reason)
        { if (!success) throw new InvalidOperationException("交互表现回归：" + reason); }
    }
}
#endif

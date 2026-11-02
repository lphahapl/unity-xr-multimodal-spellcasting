#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Exercises the controller's actual deferred queue and LateUpdate with an advancing input clock.</summary>
    public static class SkillClockRegressionChecks
    {
        static void Require(bool value, string detail)
        { if (!value) throw new InvalidOperationException("口令时钟回归：" + detail); }

        public static int Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("口令时钟检查需要编辑模式");
            int passed = 0;
            using (var f = new Fixture())
            {
                long epoch = f.Engine.Epoch;
                f.Controller.ReceiveVoice("火球术", 100.05);
                f.Tick();
                Require(f.Controller.VoiceEventCount == 1, "应经过真实控制器队列消费一次口令");
                Require(f.Engine.Epoch == epoch && f.Engine.PendingSkillId == "clock.fire",
                    $"延迟口令处理不能被同帧旧时间清空；epoch={f.Engine.Epoch}，状态={f.Engine.Status}");
                passed++;
            }
            using (var f = new Fixture())
            {
                long epoch = f.Engine.Epoch;
                f.Controller.ReceiveVoice("未知口令", 100.02);
                f.Controller.ReceiveVoice("火球术", 100.05);
                f.Tick();
                Require(f.Controller.VoiceEventCount == 2 && f.Engine.Epoch == epoch && f.Engine.PendingSkillId == "clock.fire",
                    "一帧消费多个口令仍须保持时间单调和最后的合法候选");
                passed++;
            }
            using (var f = new Fixture())
            {
                f.Engine.Voice("火球术", 100.02, 100.03);
                long epoch = f.Engine.Epoch; int cancelled = 0;
                f.Controller.CancelOverride = () => cancelled++;
                f.Controller.ReceiveVoice("取消施法", 100.05);
                f.Tick();
                Require(cancelled == 1 && f.Engine.Epoch == epoch + 1 && string.IsNullOrEmpty(f.Engine.PendingSkillId) &&
                    !f.Engine.Status.Contains("时钟回退"), "合法延迟取消只能清理一次，不能再被旧Tick触发额外重置");
                passed++;
            }
            using (var f = new Fixture())
            {
                long epoch = f.Engine.Epoch;
                f.Controller.DeferVoiceUntilLateUpdate = false;
                f.Controller.ReceiveVoice("火球术", 100.05);
                f.Tick();
                Require(f.Engine.Epoch == epoch && f.Engine.PendingSkillId == "clock.fire", "旧场景的即时口令路径仍可等待结印");
                passed++;
            }
            using (var f = new Fixture())
            {
                f.Engine.Voice("火球术", 100.02, 100.03);
                long epoch = f.Engine.Epoch;
                f.Engine.Tick(99);
                Require(f.Engine.Epoch == epoch + 1 && string.IsNullOrEmpty(f.Engine.PendingSkillId) &&
                    f.Engine.Status.Contains("时钟回退"), "真实时钟倒退必须继续清空输入，不能放宽匹配器保护");
                passed++;
            }
            using (var f = new Fixture())
            {
                int cancelled = 0; f.Controller.CancelOverride = () => cancelled++;
                f.Controller.ReceiveVoice("火球术",100.05);
                f.Controller.ExclusiveInputBlocked = () => true;
                f.Tick(); f.Controller.ReceiveVoice("取消施法",100.06); f.Tick();
                Require(f.Controller.LastVoiceBlock.Contains("腕部菜单独占输入") && f.Controller.VoiceEventCount==0 && cancelled==0 && string.IsNullOrEmpty(f.Engine.PendingSkillId),
                    "UI获得输入后，队列口令与取消口令均不可进入战斗"); passed++;
            }
            using (var f = new Fixture())
            {
                f.Controller.InvalidateEarlierVoice();
                f.Controller.ReceiveVoice("火球术",100.05); f.Tick();
                Require(f.Controller.LastVoiceBlock.Contains("屏障") && f.Controller.VoiceEventCount==0 && string.IsNullOrEmpty(f.Engine.PendingSkillId),"UI退出后迟到旧口令必须丢弃"); passed++;
                f.Controller.ReceiveVoice("火球术",100.102); f.Tick();
                Require(f.Controller.VoiceEventCount==1 && f.Engine.PendingSkillId=="clock.fire","交接后新口令仍能进入匹配"); passed++;
            }
            return passed;
        }

        sealed class Fixture : IDisposable
        {
            readonly GameObject root;
            readonly SkillDefinition skill;
            double now = 100.1;
            public readonly SkillTestController Controller;
            public readonly SkillMatchEngine Engine;
            public Fixture()
            {
                root = new GameObject("口令时钟回归_临时实体") { hideFlags = HideFlags.HideAndDontSave };
                var lab = root.AddComponent<Lab>();
                Controller = root.AddComponent<SkillTestController>();
                Controller.lab = lab; Controller.logFeatureFailures = false;
                Controller.DeferVoiceUntilLateUpdate = true;
                Controller.InputClock = () => { double value = now; now += .001; return value; };
                skill = ScriptableObject.CreateInstance<SkillDefinition>();
                skill.skillId = "clock.fire"; skill.displayName = "火球术";
                skill.triggerMode = SkillTriggerMode.VoiceAndGesture; skill.phrase = "火球术"; skill.sealName = "寅";
                Engine = new SkillMatchEngine(new[] { skill }); Engine.Tick(100);
                Require(Engine.ActiveRecipes.Count == 1, "检查配方必须有效");
                typeof(SkillTestController).GetProperty(nameof(SkillTestController.Engine)).GetSetMethod(true).Invoke(Controller, new object[] { Engine });
            }
            public void Tick()
            {
                try { typeof(SkillTestController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Controller, null); }
                catch (TargetInvocationException e) { throw e.InnerException ?? e; }
            }
            public void Dispose()
            {
                if (root) Object.DestroyImmediate(root);
                if (skill) Object.DestroyImmediate(skill);
            }
        }
    }
}
#endif

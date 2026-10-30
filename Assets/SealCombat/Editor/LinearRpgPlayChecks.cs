#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Actual Play integration with simulated commands/poses; never starts Play or saves a scene.</summary>
    public static class LinearRpgPlayChecks
    {
        static Runner active;

        public static void Begin(Action<bool, string, int> completed)
        {
            if (!EditorApplication.isPlaying || !Application.isPlaying) throw new InvalidOperationException("检查必须由父桥接在真实播放模式中启动。");
            if (active != null) throw new InvalidOperationException("线性RPG集成检查已经在运行。");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "LinearRpgTest") throw new InvalidOperationException("请使用LinearRpgTest测试场景。");
            var controllers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LinearRpgController>(true)).ToArray();
            if (controllers.Length != 1) throw new InvalidOperationException("测试场景必须恰好包含一个LinearRpgController。");
            active = new Runner(controllers[0], completed);
        }

        sealed class Runner
        {
            readonly LinearRpgController controller;
            readonly Action<bool, string, int> completed;
            readonly double deadline;
            readonly StringBuilder results = new StringBuilder();
            readonly bool previousBackground;
            readonly IEnumerator steps;
            readonly string runId = Guid.NewGuid().ToString("N");
            readonly List<KeyValuePair<Behaviour, bool>> disabledInputs = new List<KeyValuePair<Behaviour, bool>>();
            Func<bool, Pose?> previousWrist;
            bool wristCaptured, ended;
            int lastFrame = -1, passed;

            public Runner(LinearRpgController controller, Action<bool, string, int> completed)
            {
                this.controller = controller; this.completed = completed;
                previousBackground = Application.runInBackground;
                Application.runInBackground = true;
                deadline = EditorApplication.timeSinceStartup + 45;
                steps = Execute();
                EditorApplication.update += Poll;
                AssemblyReloadEvents.beforeAssemblyReload += Abort;
                EditorApplication.playModeStateChanged += PlayStateChanged;
            }

            IEnumerator Execute()
            {
                yield return null; yield return null;
                Require(controller && controller.isActiveAndEnabled && controller.Player != null && controller.skills && controller.skills.Engine != null,
                    "主控、玩家或技能匹配器未初始化。");
                Require(Time.timeScale > 0, "检查需要未暂停的游戏时钟。");
                var field = typeof(LinearRpgController).GetField("EditorTestSaveFile", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                string probeName = field?.GetValue(null) as string;
                Require(IsProbeName(probeName) && Path.GetFileName(controller.SavePath) == probeName &&
                    !controller.autoLoad && !controller.autoAdvanceStages,
                    "父桥接必须在Awake前注入唯一EditorTestSaveFile，并禁用自动读档/推进；拒绝操作用户档案。");
                Require(Path.GetFullPath(controller.SavePath) == Path.GetFullPath(Path.Combine(Application.persistentDataPath, probeName)),
                    "测试路径不属于约定的唯一probe文件。");
                Record("真实Play初始化与唯一测试存档隔离：" + controller.SavePath);

                var effects = controller.skills.effects;
                var locomotion = controller.locomotion;
                Require(effects && locomotion && locomotion.head && locomotion.rigRoot, "缺少效果、导航或Rig接线。");
                previousWrist = effects.WristPoseOverride; wristCaptured = true;
                Disable(controller.skills.lab); Disable(controller.skills.voice);
                foreach (var hazard in controller.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RpgTrainingHazard>(true))) Disable(hazard);
                controller.skills.CancelInputOnly(); locomotion.ResetInput(); effects.ClearAll();
                double settle = Time.realtimeSinceStartupAsDouble + .3;
                while (Time.realtimeSinceStartupAsDouble < settle) yield return null;
                locomotion.ResetInput();
                Require(controller.StageIndex == 0 && controller.Progress.unlockedSkillIds.Count == 1 && controller.Progress.unlockedSkillIds.Contains("fireball"),
                    "新角色应只解锁fireball且位于首阶段。");
                Record("初始仅解锁火球；真实手追踪/语音与自动攻击已隔离");

                SkillDefinition fire = Skill("fireball"), shield = Skill("buff.zi"), voiceShield = Skill("buff.voice");
                Require(fire.manaCost > 0 && fire.cooldownSeconds > 0 && Group(shield) == Group(voiceShield), "需配置正数火球消耗/CD和护盾共享组。");
                SkillCatalog original = typeof(LinearRpgController).GetField("originalCatalog", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller) as SkillCatalog;
                Require(original, "无法取得原配置目录。");
                var diskEffect = original.skills.Single(s => s && s.skillId == "fireball").effectDefinition as ProjectileEffectDefinition;
                var runtimeEffect = fire.effectDefinition as ProjectileEffectDefinition;
                Require(diskEffect && runtimeEffect && diskEffect != runtimeEffect && AssetDatabase.Contains(diskEffect) && !AssetDatabase.Contains(runtimeEffect),
                    "运行态火球效果必须是磁盘SO的独立克隆。");
                string diskPath = Path.GetFullPath(AssetDatabase.GetAssetPath(diskEffect));
                byte[] originalBytes = File.ReadAllBytes(diskPath);
                string originalJson = JsonUtility.ToJson(diskEffect);

                effects.WristPoseOverride = _ => null;
                float mana = controller.Player.Mana;
                Require(!controller.DebugCast(fire.skillId), "没有有效手姿时不应生成火球。");
                Equal(mana, controller.Player.Mana, "无手姿失败退款");
                Equal(0, controller.Player.GetCooldown(Group(fire), controller.GameTime), "失败不建立CD");
                Record("显式无手姿：火球失败、不扣MP、不建立CD");

                effects.WristPoseOverride = _ => new Pose(locomotion.head.position + locomotion.head.forward * .45f - Vector3.up * .2f, locomotion.head.rotation);
                mana = controller.Player.Mana;
                Require(controller.DebugCast(fire.skillId) && effects.HasRightHeldObject, "注入模拟右手姿后应成功持球。");
                Equal(mana - fire.manaCost, controller.Player.Mana, "持球精确扣MP");
                mana = controller.Player.Mana;
                Require(!controller.DebugCast(fire.skillId), "重复持球应拒绝。");
                Equal(mana, controller.Player.Mana, "重复持球不重复扣费");
                Require(!controller.DebugMove(true, Vector2.up), "右手持球时前闪必须让位于发射。");
                float cooldown = controller.Player.GetCooldown(Group(fire), controller.GameTime);
                controller.CancelCasting();
                Require(!effects.HasHeldObject, "取消应移除未发射火球。");
                Equal(mana, controller.Player.Mana, "主动取消不退款");
                Equal(cooldown, controller.Player.GetCooldown(Group(fire), controller.GameTime), "主动取消保留CD");
                Record("模拟持球扣费、重复拒绝、前闪门控与取消保留MP/CD");

                while (controller.Player.GetCooldown(Group(fire), controller.GameTime) > 0) yield return null;
                controller.Player.RestoreMana(controller.Player.MaxMana);
                Require(controller.DebugCast(fire.skillId), "冷却结束后可再次持球。");
                while (controller.Player.GetCooldown(Group(fire), controller.GameTime) > 0) yield return null;
                mana = controller.Player.Mana;
                Require(effects.HasHeldObject && !controller.DebugCast(fire.skillId), "CD结束但手槽占用时仍应拒绝重复持球。");
                Equal(mana, controller.Player.Mana, "槽占用失败退款");
                Record("跨真实帧等待CD后，占用手槽仍拒绝且不扣MP");

                MethodInfo launch = typeof(SkillEffects).GetMethod("Launch", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(launch != null, "缺少发射接线测试的Launch入口。");
                launch.Invoke(effects, new object[] { Vector3.up });
                Require(!effects.HasHeldObject && effects.ActiveProjectileCount > 0, "反射发射后应出现飞行火球。");
                int flying = effects.ActiveProjectileCount;
                controller.CancelCasting();
                Require(effects.ActiveProjectileCount == flying, "取消不能销毁已发射火球。");
                yield return null;
                Require(effects.ActiveProjectileCount > 0, "飞行火球应跨真实帧持续存在。");
                Record("反射Launch模拟发射：取消保留飞行对象（未测试真实前推识别）");

                controller.DebugDefeatTargets();
                Require(controller.TryAdvanceStage(), "作弊击破后应允许推进：" + controller.Status);
                Require(controller.StageIndex == 1 && controller.Progress.unlockedSkillIds.Contains(shield.skillId) && controller.Progress.unlockedSkillIds.Contains(voiceShield.skillId),
                    "第一阶段完成后应解锁两个护盾。");
                Record("作弊击靶推进至第二阶段，首次固定解锁两个护盾");

                controller.Player.RestoreMana(controller.Player.MaxMana);
                Require(controller.DebugCast(shield.skillId), "解锁后的护盾应可施放。");
                float shieldPoints = effects.ShieldPoints;
                mana = controller.Player.Mana;
                Require(!controller.DebugCast(voiceShield.skillId), "交替护盾不能绕过共享CD。");
                Equal(mana, controller.Player.Mana, "共享CD拒绝不扣费");
                controller.CancelCasting(); Equal(shieldPoints, effects.ShieldPoints, "取消保留护盾");
                Record("共享CD阻止交替刷新；取消保留已生效护盾");

                string hit = "probe." + runId + ".shield-hit";
                Require(shieldPoints >= 25 && controller.ReceiveDamage(hit, 25), "首次模拟伤害应被护盾承接。");
                float afterHit = effects.ShieldPoints, healthAfterHit = controller.Player.Health;
                Equal(shieldPoints - 25, afterHit, "首次扣盾");
                Require(!controller.ReceiveDamage(hit, 25), "重复damageId必须在扣盾之前拒绝。");
                Equal(afterHit, effects.ShieldPoints, "重复伤害不再吃盾");
                Equal(healthAfterHit, controller.Player.Health, "重复伤害不扣生命");
                Record("同一damageId只结算一次护盾与生命");

                effects.ClearAll(); controller.Player.Respawn(controller.Player.MaxHealth, 0, controller.GameTime);
                Equal(0, controller.Player.GetCooldown(Group(fire), controller.GameTime), "MP门控测试必须先排除CD干扰");
                mana = controller.Player.Mana;
                Require(!controller.DebugCast(fire.skillId), "MP不足时应拒绝火球。");
                Equal(mana, controller.Player.Mana, "MP不足不扣费");
                controller.Player.RestoreMana(controller.Player.MaxMana);
                Record("显式注入测试状态0MP，资源门控拒绝施法");

                Vector3 localHead = locomotion.head.localPosition;
                Quaternion localRotation = locomotion.head.localRotation;
                Vector3 rootBefore = locomotion.rigRoot.position;
                bool moved = false;
                foreach (Vector2 direction in new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up })
                    if (controller.DebugMove(false, direction)) { moved = true; break; }
                Require(moved && Vector3.Distance(rootBefore, locomotion.rigRoot.position) > .1f, "至少应有一个可用导航落点：" + locomotion.Status);
                Equal(localHead, locomotion.head.localPosition, "导航保持头部局部位置");
                Require(Quaternion.Angle(localRotation, locomotion.head.localRotation) < .01f, "导航不得修改头部局部旋转。");
                Record("真实Physics落点检查下模拟导航成功，保持head.localPosition/Rotation");

                localHead = locomotion.head.localPosition; rootBefore = locomotion.rigRoot.position;
                bool dodged = false;
                foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.down, Vector2.up })
                    if (controller.DebugMove(true, direction)) { dodged = true; break; }
                Require(dodged && Vector3.Distance(rootBefore, locomotion.rigRoot.position) > .1f, "至少应有一个可用闪避方向：" + locomotion.Status);
                Equal(localHead, locomotion.head.localPosition, "闪避保持头部局部位置");
                Require(locomotion.DodgeRemaining > 0, "成功闪避应建立CD。");
                Record("真实Physics检查下模拟闪避成功，保持head.localPosition并建立CD");

                float baseRuntimeDamage = runtimeEffect.damage;
                Require(controller.Progress.currency >= controller.fireballUpgradePrice, "首关奖励应足以支付一次火球升级。");
                Require(controller.UpgradeFireball(), "升级应成功：" + controller.Status);
                Require(runtimeEffect.damage > baseRuntimeDamage, "升级应增加运行态火球伤害。");
                Require(JsonUtility.ToJson(diskEffect) == originalJson && File.ReadAllBytes(diskPath).SequenceEqual(originalBytes), "升级不得修改原SO内存或磁盘。");
                Record("升级增加运行态伤害；原SO内存与磁盘字节未改变");

                while (controller.Player.GetCooldown(Group(fire), controller.GameTime) > 0) yield return null;
                controller.Player.RestoreMana(controller.Player.MaxMana);
                Require(controller.DebugCast(fire.skillId), "存档前施法应成功。"); controller.CancelCasting();
                Require(controller.SaveCheckpoint(), "测试检查点应保存成功：" + controller.Status);
                int savedStage = controller.StageIndex, savedCompleted = controller.Progress.completedStageIds.Count;
                float savedCooldown = controller.Progress.checkpoint.cooldowns.Single(cd => cd.group == Group(fire)).remainingSeconds;
                controller.Player.Respawn(controller.Player.MaxHealth, 1, controller.GameTime);
                controller.Progress.completedStageIds.Clear();
                yield return null;
                Require(controller.RestoreCheckpoint(), "从probe恢复应成功：" + controller.Status);
                Require(controller.StageIndex == savedStage && controller.Progress.completedStageIds.Count == savedCompleted, "恢复应重建已保存阶段进度。");
                Equal(savedCooldown, controller.Player.GetCooldown(Group(fire), controller.GameTime), "恢复CD剩余时间");
                Require(controller.Player.Mana > 1, "恢复应还原已保存MP。");
                Record("磁盘probe检查点恢复阶段、MP和CD剩余秒数");

                Require(controller.ReceiveDamage("probe." + runId + ".fatal", 10000) && !controller.Player.Alive, "无盾致命伤害应使玩家死亡。");
                yield return null;
                Require(!controller.Player.Alive && !effects.HasHeldObject && effects.ActiveProjectileCount == 0, "死亡状态应跨帧保持且清理临时火球。");
                Require(!controller.DebugCast(fire.skillId) && !controller.DebugMove(false, Vector2.left) && !controller.DebugMove(true, Vector2.right), "死亡后施法、导航和闪避必须拒绝。");
                Require(controller.RestoreCheckpoint() && controller.Player.Alive, "死亡后应能恢复有效检查点。");
                Require(controller.StageIndex == savedStage, "死亡恢复不能丢失阶段。");
                Require(File.ReadAllBytes(diskPath).SequenceEqual(originalBytes), "测试结束后原SO磁盘字节必须不变。");
                Record("死亡拒绝全部动作；检查点恢复生命与阶段；原资产未改动");

                // Exercise the actual hazard Update across frames, independently of simulated direct damage.
                var hazards = controller.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RpgTrainingHazard>(true)).Where(h => h.controller == controller).ToArray();
                Require(hazards.Length == 1 && controller.StageIndex == 1, "攻击预警检查需要第二阶段的唯一训练攻击组件。");
                var hazardProbe = hazards[0];
                float priorInterval = hazardProbe.interval, priorWarning = hazardProbe.warningSeconds, priorDamage = hazardProbe.damage;
                bool priorHazardToggle = controller.HazardsEnabled;
                try
                {
                    hazardProbe.enabled = false;
                    hazardProbe.interval = 2; hazardProbe.warningSeconds = .5f; hazardProbe.damage = 25;
                    if (!controller.HazardsEnabled) controller.ToggleHazards();
                    effects.ClearAll(); controller.Player.Respawn(controller.GameTime);
                    float initialHealth = controller.Player.Health;
                    bool sawWarning = false;
                    double hazardDeadline = EditorApplication.timeSinceStartup + 6;
                    hazardProbe.enabled = true;
                    while (controller.Player.Health == initialHealth)
                    {
                        Require(EditorApplication.timeSinceStartup <= hazardDeadline, "六秒内应完成一次真实预警攻击。");
                        sawWarning |= hazardProbe.WarningActive;
                        yield return null;
                    }
                    hazardProbe.enabled = false;
                    Require(sawWarning, "应先出现真实帧红圈预警，再产生伤害。");
                    Equal(initialHealth - 25, controller.Player.Health, "首次训练攻击精确扣25HP");
                    yield return null; yield return null;
                    Equal(initialHealth - 25, controller.Player.Health, "停用训练攻击后不重复扣血");
                }
                finally
                {
                    if (hazardProbe)
                    {
                        hazardProbe.enabled = false;
                        hazardProbe.interval = priorInterval; hazardProbe.warningSeconds = priorWarning; hazardProbe.damage = priorDamage;
                    }
                    if (controller && controller.HazardsEnabled != priorHazardToggle) controller.ToggleHazards();
                }
                Record("真实Update预警攻击：2秒间隔＋0.5秒预警后仅扣25HP，停用跨帧不再扣血，参数已还原");

                int currencyBeforeSecondStage = controller.Progress.currency;
                int secondReward = controller.stages[controller.StageIndex].rewardCurrency;
                int rewardCountBefore = controller.Progress.claimedRewardIds.Count;
                controller.DebugDefeatTargets();
                Require(controller.TryAdvanceStage() && controller.StageIndex == 2, "第二阶段作弊击靶后应推进至最终阶段。");
                Equal(currencyBeforeSecondStage + secondReward, controller.Progress.currency, "第二阶段首次奖励");
                Require(controller.Progress.claimedRewardIds.Count == rewardCountBefore + 1, "第二阶段只能新增一个奖励ID。");
                int currencyAfterSecondStage = controller.Progress.currency;
                Require(!controller.TryAdvanceStage(), "最终阶段新训练靶尚未击破，不应再次推进领奖。");
                Equal(currencyAfterSecondStage, controller.Progress.currency, "重复推进不追加奖励");
                Equal(50, currencyAfterSecondStage, "正常奖励与先前升级后的货币余额");
                Require(!controller.PurchaseHiddenSkill() && !controller.Progress.unlockedSkillIds.Contains("buff.hidden"), "50货币不足以购买60货币隐藏术。");
                Equal(currencyAfterSecondStage, controller.Progress.currency, "购买资金不足不扣货币");
                Record("第二阶段奖励仅一次；正常奖励减升级支出后余额50，购买60货币隐藏术失败且不扣钱");

                int finalCurrency = controller.Progress.currency;
                int finalReward = controller.stages[controller.StageIndex].rewardCurrency;
                controller.DebugDefeatTargets();
                Require(controller.TryAdvanceStage() && controller.CampaignComplete, "最终阶段作弊击靶后应完成全部关卡。");
                Equal(finalCurrency + finalReward, controller.Progress.currency, "最终阶段首次奖励");
                Require(controller.Progress.completedStageIds.Count == controller.stages.Length, "全部阶段完成标识应完整。");
                int completedCurrency = controller.Progress.currency, completedRewards = controller.Progress.claimedRewardIds.Count;
                Require(!controller.TryAdvanceStage(), "CampaignComplete后不能再次推进领奖。");
                Equal(completedCurrency, controller.Progress.currency, "最终关不能重复领奖");
                Require(controller.Progress.claimedRewardIds.Count == completedRewards && !controller.HazardsEnabled, "最终奖励ID不重复且完成后停止自动攻击。");
                Require(controller.PurchaseHiddenSkill() && controller.Progress.unlockedSkillIds.Contains("buff.hidden"), "最终正常奖励到账后应能购买隐藏术。");
                Equal(completedCurrency - controller.hiddenSkillPrice, controller.Progress.currency, "隐藏术只扣一次价格");
                int currencyAfterPurchase = controller.Progress.currency;
                Require(!controller.PurchaseHiddenSkill(), "重复购买隐藏术必须被拒绝。");
                Equal(currencyAfterPurchase, controller.Progress.currency, "重复购买不扣货币");
                var hidden = Skill("buff.hidden");
                Equal(25, hidden.manaCost, "隐藏术配置消耗");
                mana = controller.Player.Mana;
                Require(controller.DebugCast(hidden.skillId), "已购买隐藏术应可模拟施放。");
                Equal(mana - 25, controller.Player.Mana, "隐藏术精确扣25MP");
                Equal(150, effects.ShieldPoints, "隐藏术提供150护盾");
                Record("最终关CampaignComplete且不重复领奖；正常通关货币购买隐藏术，重复购买不扣钱，150护盾/25MP");

                while (controller.Player.GetCooldown(Group(fire), controller.GameTime) > 0) yield return null;
                controller.Player.RestoreMana(controller.Player.MaxMana);
                controller.CancelCasting();
                double oldCancelEnd = Time.realtimeSinceStartupAsDouble - .1;
                Require(controller.DebugCast(fire.skillId), "取消屏障测试需要一个新持握火球。");
                controller.skills.ReceiveVoice(controller.skills.CancelPhrase, oldCancelEnd);
                yield return null;
                Require(effects.HasHeldObject, "结束时间早于最近取消屏障的旧口令不能收起新球。");
                controller.skills.ReceiveVoice(controller.skills.CancelPhrase, Time.realtimeSinceStartupAsDouble);
                yield return null;
                Require(!effects.HasHeldObject, "有效的新取消口令应经延迟语音队列收起持球。");
                Record("显式注入口令时间：旧取消被barrier拒绝，新取消经LateUpdate队列生效（未测麦克风）");

                while (controller.Player.GetCooldown(Group(fire), controller.GameTime) > 0) yield return null;
                controller.Player.RestoreMana(controller.Player.MaxMana);
                Require(controller.DebugCast(fire.skillId), "生命周期检查需要一个新持握火球。");
                launch.Invoke(effects, new object[] { Vector3.up });
                Require(effects.ActiveProjectileCount > 0, "生命周期检查需要活跃飞行火球。");
                float manaBeforeDisable = controller.Player.Mana;
                float cooldownBeforeDisable = controller.Player.GetCooldown(Group(fire), controller.GameTime);
                double timeBeforeDisable = controller.GameTime;
                try
                {
                    controller.enabled = false;
                    yield return null; yield return null;
                    Require(!controller.skills.enabled && !effects.enabled, "禁用主控后技能与效果子系统必须同时禁用。");
                    Require(!effects.HasHeldObject && effects.ActiveProjectileCount == 0, "禁用主控应清理持握与飞行火球。");
                    Require(controller.GameTime == timeBeforeDisable, "禁用主控期间会话游戏时钟不能推进。");
                    Equal(manaBeforeDisable, controller.Player.Mana, "禁用不重置能量");
                    Equal(cooldownBeforeDisable, controller.Player.GetCooldown(Group(fire), controller.GameTime), "禁用不重置CD");
                }
                finally
                {
                    if (controller) controller.enabled = true;
                }
                Require(controller.skills.enabled && effects.enabled && controller.CanAct, "重新启用主控应恢复技能与效果子系统。");
                Equal(manaBeforeDisable, controller.Player.Mana, "重新启用不回满能量");
                Equal(cooldownBeforeDisable, controller.Player.GetCooldown(Group(fire), controller.GameTime), "重新启用不清空CD");
                yield return null;
                bool movedAfterEnable = false;
                foreach (Vector2 direction in new[] { Vector2.left, Vector2.down, Vector2.right, Vector2.up })
                    if (controller.DebugMove(false, direction)) { movedAfterEnable = true; break; }
                Require(movedAfterEnable, "重新启用后应能继续执行有效模拟导航。");
                Require(File.ReadAllBytes(diskPath).SequenceEqual(originalBytes), "最终扩展检查仍不能修改原SO磁盘字节。");
                Record("主控禁用跨两帧关闭子系统/清飞行，重启保留MP/CD且可继续导航");
            }

            SkillDefinition Skill(string id) => controller.skills.ConfiguredSkills.Single(s => s && s.skillId == id);
            static string Group(SkillDefinition skill) => string.IsNullOrEmpty(skill.cooldownGroup) ? skill.skillId : skill.cooldownGroup;
            static bool IsProbeName(string name)
            {
                const string prefix = "linear-rpg-probe-";
                return !string.IsNullOrEmpty(name) && name == Path.GetFileName(name) && name.StartsWith(prefix, StringComparison.Ordinal) &&
                    name.EndsWith(".json", StringComparison.Ordinal) && Guid.TryParse(name.Substring(prefix.Length, name.Length - prefix.Length - 5), out _);
            }
            void Disable(Behaviour input)
            {
                if (!input) return;
                disabledInputs.Add(new KeyValuePair<Behaviour, bool>(input, input.enabled)); input.enabled = false;
            }
            void Record(string message) { passed++; results.AppendLine(passed + ". 通过：" + message); }
            void Poll()
            {
                try
                {
                    if (!Application.isPlaying || !controller) throw new InvalidOperationException("播放模式或主控已结束。");
                    if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("真实Play检查超过45秒；见最后通过步骤。");
                    if (lastFrame == Time.frameCount) return;
                    lastFrame = Time.frameCount;
                    if (!steps.MoveNext()) Finish(true, "全部真实Play集成检查通过。");
                }
                catch (Exception error) { Finish(false, error.ToString()); }
            }
            void Abort() => Finish(false, "程序集重载，检查已中止。");
            void PlayStateChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Finish(false, "退出播放模式，检查已中止。"); }
            void Finish(bool success, string detail)
            {
                if (ended) return; ended = true;
                EditorApplication.update -= Poll;
                AssemblyReloadEvents.beforeAssemblyReload -= Abort;
                EditorApplication.playModeStateChanged -= PlayStateChanged;
                try { (steps as IDisposable)?.Dispose(); }
                catch (Exception error) { success = false; detail += "\n临时测试参数恢复失败：" + error.Message; }
                if (controller && controller.skills && controller.skills.effects && wristCaptured)
                { controller.skills.effects.WristPoseOverride = previousWrist; controller.skills.effects.ClearAll(); }
                foreach (var pair in disabledInputs) if (pair.Key) pair.Key.enabled = pair.Value;
                Application.runInBackground = previousBackground;
                string text = (success ? "通过" : "失败") + "：" + detail + "\n" + results +
                    "\n范围：Unity真实Play生命周期、游戏时钟、Physics落点及显式调试命令。手姿为WristPoseOverride模拟，发射通过反射Launch；击靶使用显式作弊。\n" +
                    "未测：真机手追踪、结印/前推/导航手势识别、麦克风/真实语音、Quest/PICO体验。\n" +
                    "保留测试存档：" + (controller ? controller.SavePath : "主控已销毁") + "；未删除档案、未保存场景或资产。";
                try
                {
                    string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "linear-rpg-probe-" + runId + "-report.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, text, new UTF8Encoding(false));
                    text += "\n报告：" + reportPath;
                }
                catch (Exception error) { text += "\n报告写入失败：" + error.Message; }
                active = null;
                completed?.Invoke(success, text, passed);
            }
            static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            static void Equal(float expected, float actual, string message)
            { Require(float.IsFinite(actual) && Mathf.Abs(expected - actual) <= .02f, message + $"：期望{expected}，实际{actual}"); }
            static void Equal(Vector3 expected, Vector3 actual, string message)
            { Require(Vector3.Distance(expected, actual) < .0001f, message + $"：期望{expected}，实际{actual}"); }
        }
    }
}
#endif

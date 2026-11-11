using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Events;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class EnemyHitFeedbackChecks
    {
        const string Key = "NarutoVR.EnemyHitFeedbackChecks";
        const string Report = "Logs/enemy-hit-feedback.txt";
        static int phase, failures, notices, deaths, impacts;
        static double deadline, hitTime;
        static bool sawHit, sawSeated, movedIntoAttackRange, attackSurvivedBodyHit;
        static string originalAttack;
        static IDisposable hpListener, deathListener;
        static EnemyController enemy;
        static EnemyVitalsNotice last;
        static EnemyCombatState firstLife;
        static LinearRpgController rpg;
        static CombatRoomEncounter room;
        static SkillController effects;
        static VfxPoolManager particles;
        static ProjectileEffectDefinition fire;
        static int priorAudio, priorImpacts, priorParticles;
        static Vector3 impactPoint;
        static double Now => EditorApplication.timeSinceStartup;

        static EnemyHitFeedbackChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) { deadline = Now + 95; EditorApplication.update += Tick; }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
                }
            };
        }

        public static void RunBatch()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            if (!Application.isBatchMode || !File.Exists(Path.Combine(root, ".skill-assembly-test-project")) ||
                !File.ReadAllText("Assets/scripts/logic/Rpg/LinearRpgController.cs").Contains("SkillAssemblyPlayerSave"))
                throw new InvalidOperationException("Requires disposable clone with isolated save path");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, DateTimeOffset.Now.ToString("o") + Environment.NewLine);
            var scene = EditorSceneManager.OpenScene("Assets/Scene/Chapter01_Awakening.unity");
            var owner = Object.FindFirstObjectByType<LinearRpgController>();
            var chapter = (OpeningTrialChapter)owner.chapter;
            int combat = Array.FindIndex(chapter.sections, s => s.lines.Any(line => line.id == "FT01"));
            var model = new PlayerModel(new RpgPlayerState());
            model.Discover(chapter.InitialDiscoveries);
            foreach (var stage in owner.stages.Take(combat))
                if (!model.ClaimStage(stage.stageId, stage.rewardCurrency, stage.unlockSkillIds)) throw new Exception(stage.stageId);
            var dto = model.Export(model.Vitals.CaptureCheckpoint(0), owner.checkpointNodes[combat].nodeId);
            owner.autoLoad = true;
            owner.saveFileName = "qa-enemy-hit-" + Guid.NewGuid().ToString("N") + ".json";
            owner.skills.voice.autoStartInEditor = false;
            var savedir = Path.Combine(root, "Temp", "SkillAssemblyPlayerSave");
            Directory.CreateDirectory(savedir);
            File.WriteAllText(Path.Combine(savedir, owner.saveFileName), JsonUtility.ToJson(new SaveEnvelope { profileId = "linear-rpg-player", progress = dto }, true));
            if (!EditorSceneManager.SaveScene(scene, "Assets/Scene/QA_EnemyHitFeedback.unity")) throw new IOException("Could not create QA scene");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".exit", 1); EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            try
            {
                if (Now > deadline) throw new TimeoutException("phase " + phase + " room=" + room?.Status + " last=" + last.HitId);
                if (!rpg) rpg = Object.FindFirstObjectByType<LinearRpgController>();
                if (!rpg || !rpg.CanAct) return;
                if (phase == 0)
                {
                    room = ((OpeningTrialChapter)rpg.chapter).encounter; effects = rpg.skills.effects;
                    var target = room.area.transform.TransformPoint(room.area.center); var floor = rpg.locomotion.PlayerFloorPosition;
                    target.y = floor.y; rpg.locomotion.rigRoot.position += target - floor; Physics.SyncTransforms();
                    fire = (ProjectileEffectDefinition)rpg.skills.ConfiguredSkills.Single(s => s.skillId == "fireball").effectDefinition;
                    effects.WristPoseOverride = left => left ? (Pose?)null : new Pose(rpg.locomotion.head.position + Vector3.right * .3f, Quaternion.identity);
                    particles = Object.FindFirstObjectByType<VfxPoolManager>();
                    hpListener = EventCenter.Subscribe(EnemyEvents.VitalsChanged, n => {
                        if (!enemy || n.Enemy != enemy.Entity || string.IsNullOrEmpty(n.HitId)) return;
                        last = n;
                        notices++;
                        if (notices == 1) attackSurvivedBodyHit = !string.IsNullOrEmpty(originalAttack) &&
                            enemy.CurrentAttack >= 0 && enemy.Entity.ActiveAttackId == originalAttack &&
                            enemy.Entity.Animator.GetCurrentAnimatorStateInfo(0).IsTag("EnemyAttack");
                    });
                    deathListener = EventCenter.Subscribe(EnemyEvents.Died, n => { if (enemy && n.Enemy == enemy.Entity) deaths++; });
                    effects.ProjectileStageChanged += ProjectileEvent; phase++; return;
                }
                if (phase == 1 || phase == 5)
                {
                    if (!room.HasStarted || room.enemies.Length == 0) return;
                    enemy = room.enemies.FirstOrDefault(e => e && e.CanReceiveHit);
                    if (!enemy || (phase == 5 && ReferenceEquals(firstLife, enemy.Entity.Combat))) return;
                    if (phase == 5 && rpg.Player.GetCooldown("fireball", rpg.GameTime) > 0) return;
                    bool weak = phase == 5;
                    if (!weak)
                    {
                        firstLife = enemy.Entity.Combat;
                        if (!movedIntoAttackRange)
                        {
                            var floor = rpg.locomotion.PlayerFloorPosition;
                            var near = enemy.transform.position + enemy.transform.forward * (enemy.attackDistance * .7f);
                            near.y = floor.y;
                            rpg.locomotion.rigRoot.position += near - floor;
                            Physics.SyncTransforms();
                            movedIntoAttackRange = true;
                            return;
                        }
                        var state = enemy.Entity.Animator.GetCurrentAnimatorStateInfo(0);
                        if (enemy.CurrentAttack < 0 || !state.IsTag("EnemyAttack") || state.normalizedTime < .08f || state.normalizedTime > .45f) return;
                        originalAttack = enemy.Entity.ActiveAttackId;
                        Check(!string.IsNullOrEmpty(originalAttack), $"body test fires during authored attack {originalAttack} at normalized time {state.normalizedTime:F3}");
                    }
                    Info($"spawn role={enemy.Entity.Definition.enemyId} HP={enemy.Entity.Combat.Health} poise={enemy.Entity.Combat.Poise} stage={rpg.StageIndex} weakShot={weak}");
                    Shoot(weak); phase++; return;
                }
                if (phase == 2)
                {
                    if (notices == 0 || impacts == priorImpacts) return;
                    Check(!last.WeakPoint && !last.Dead && !last.Broken && Close(last.HealthDamage, fire.damage) && Close(last.PoiseDamage, fire.poiseDamage),
                        $"body hit {last.HitId} HP={last.Health} (-{last.HealthDamage}) poise={last.Poise} (-{last.PoiseDamage})");
                    CheckFeedback("body");
                    int old = notices;
                    Check(!enemy.Entity.ReceiveHit(last.HitId, fire.damage, fire.poiseDamage) && notices == old, "same physical projectile ID cannot reapply HP, poise or publish a second notice");
                    sawHit = IsAnimation("Hit"); hitTime = Now; phase++; return;
                }
                if (phase == 3)
                {
                    sawHit |= IsAnimation("Hit");
                    if (Now - hitTime < 1.25) return;
                    Check(attackSurvivedBodyHit && !sawHit, $"ordinary body fireball preserves current attack {originalAttack} without full-body stagger");
                    var bar = enemy.GetComponent<EnemyPoiseBar>();
                    var healthImage = bar.GetComponentsInChildren<Image>().Single(i => i.name == "剩余生命");
                    var poiseImage = bar.GetComponentsInChildren<Image>().Single(i => i.name == "剩余韧性");
                    Check(Close(healthImage.rectTransform.anchorMax.x, .5f) && poiseImage.rectTransform.anchorMax.x < .99f,
                        $"visible HP/poise fill ratios {healthImage.rectTransform.anchorMax.x:F3}/{poiseImage.rectTransform.anchorMax.x:F3} with colors {healthImage.color}/{poiseImage.color}");
                    var closeHit = enemy.GetComponentsInChildren<EnemyHitZone>().First(z => !z.isWeakPoint);
                    var testBall = new GameObject("QA poise-only projectile").AddComponent<SkillProjectile>();
                    testBall.damage = 0; testBall.poiseDamage = enemy.Entity.Combat.MaxPoise; testBall.radius = .015f;
                    testBall.transform.position = closeHit.GetComponent<Collider>().bounds.center;
                    testBall.Initialize(Vector3.forward, rpg.locomotion.rigRoot, ~0, 0, null, null);
                    Check(!enemy.Entity.Combat.Dead && enemy.Entity.Combat.Broken, "physical poise-only control hit breaks poise without killing enemy");
                    sawSeated = IsAnimation("Seated"); hitTime = Now; phase++; return;
                }
                if (phase == 4)
                {
                    sawSeated |= IsAnimation("Seated"); if (!sawSeated && Now - hitTime < 1.25) return;
                    Check(sawSeated, "poise break enters authored Seated animator state");
                    room.ResetEncounter(); phase++; return;
                }
                if (phase == 6)
                {
                    if (!last.Dead || impacts == priorImpacts) return;
                    Check(last.WeakPoint && last.HitZone.isWeakPoint && Close(last.HealthDamage, Mathf.Min(last.Enemy.Combat.MaxHealth, fire.damage * last.HitZone.DamageMultiplier + last.HitZone.BonusDamage)) &&
                        Close(last.PoiseDamage, Mathf.Min(last.Enemy.Combat.MaxPoise, fire.poiseDamage * last.HitZone.PoiseMultiplier)),
                        $"rear weak point on fresh life {last.HitId} HP={last.Health} (-{last.HealthDamage}) poise={last.Poise} (-{last.PoiseDamage})");
                    CheckFeedback("lethal weak point"); Check(deaths == 1 && notices == 3, $"final hit publishes exactly one death and three distinct hit notices, got {deaths}/{notices}");
                    hitTime = Now; phase++; return;
                }
                if (phase == 7)
                {
                    bool death = enemy && enemy.isActiveAndEnabled && IsAnimation("Dead");
                    if (!death && enemy && enemy.gameObject.activeInHierarchy && Now - hitTime < 1.25) return;
                    Check(death, "lethal hit enters authored Dead animation while spawned enemy is still active"); Finish();
                }
            }
            catch (Exception error) { Check(false, error.ToString()); Finish(); }
        }

        static void Shoot(bool weak)
        {
            var zone = enemy.GetComponentsInChildren<EnemyHitZone>().Where(z => z.isWeakPoint == weak)
                .OrderBy(z => z.GetComponent<Collider>().bounds.center.y).First();
            var col = zone.GetComponent<Collider>(); Physics.SyncTransforms();
            Vector3 outward = (weak ? -1 : 1) * enemy.transform.forward;
            var point = col.ClosestPoint(col.bounds.center + outward * 3);
            if (!rpg.DebugCast("fireball")) throw new Exception("authored fireball cast refused: " + rpg.Status);
            var held = (GameObject)typeof(SkillController).GetProperty("heldObject", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effects);
            held.transform.position = point + outward * (fire.radius + .05f);
            priorAudio = effects.AudioPlayer ? effects.AudioPlayer.PlayCount : 0; priorImpacts = impacts;
            priorParticles = particles ? particles.ActiveCount : 0;
            typeof(SkillController).GetMethod("Launch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effects, new object[] { -outward });
        }
        static void ProjectileEvent(ProjectileStage stage, Vector3 point)
        { if (stage == ProjectileStage.Impact) { impacts++; impactPoint = point; } }
        static bool IsAnimation(string name)
        {
            var animator = enemy.Entity.Animator;
            return animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + name) ||
                (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Base Layer." + name));
        }
        static void CheckFeedback(string shot)
        {
            var audio = effects.AudioPlayer;
            var source = audio && audio.LastClip ? Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .FirstOrDefault(s => s.clip == audio.LastClip && s.isActiveAndEnabled && Vector3.Distance(s.transform.position, impactPoint) < .05f) : null;
            Check(impacts == priorImpacts + 1 && audio && effects.LastImpactAudioHandled && audio.LastCue == SkillAudioCue.Impact &&
                source && source.spatialBlend > .5f && audio.PlayCount > priorAudio && Vector3.Distance(audio.LastPosition, impactPoint) < .01f,
                $"{shot} impact audio cue={audio?.LastCue} clip={audio?.LastClip?.name} at={impactPoint}");
            var poolRoot = rpg.gameObject.scene.GetRootGameObjects().Single(g => g.name == "命中特效池（运行时）");
            var systems = poolRoot.GetComponentsInChildren<ParticleSystem>().Where(system => system.gameObject.activeInHierarchy &&
                Vector3.Distance(system.transform.position, impactPoint) < .7f && system.GetComponent<ParticleSystemRenderer>().enabled).ToArray();
            Check(particles && particles.Handles(fire.impactPrefab) && particles.ActiveCount == priorParticles + 1 && systems.Length > 0,
                $"{shot} live authored impact pool {priorParticles}->{particles?.ActiveCount} particles={systems.Length} " +
                string.Join(",", systems.Take(4).Select(s => s.name + ":" + s.main.startColor.colorMax + " material=" + s.GetComponent<ParticleSystemRenderer>().sharedMaterial?.name)));
        }
        static bool Close(float a, float b) => Mathf.Abs(a - b) < .02f;
        static void Info(string message) => File.AppendAllText(Report, message + Environment.NewLine);
        static void Check(bool ok, string description) { if (!ok) failures++; Info((ok ? "PASS " : "FAIL ") + description); }
        static void Finish()
        {
            EditorApplication.update -= Tick;
            hpListener?.Dispose(); deathListener?.Dispose();
            if (effects) effects.ProjectileStageChanged -= ProjectileEvent;
            Info(failures == 0 ? "PASS END" : "FAIL END " + failures);
            SessionState.SetInt(Key + ".exit", failures == 0 ? 0 : 1); EditorApplication.ExitPlaymode();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    public sealed partial class EnemyController
    {
        EliteSummonSession summons;
        EliteSummonSession memberOf;
        float paralysisRemaining;
        bool seatedByCommand;

        public bool IsCommanding => summons?.Commanding == true;
        public bool IsCommandParalyzed => paralysisRemaining > 0;
        internal bool IsCommandedBy(EnemyLease leader) => memberOf != null && memberOf.BelongsTo(leader);

        void ParalyzeByCommand(float duration)
        {
            paralysisRemaining = Mathf.Max(paralysisRemaining, duration);
            if (reaction < EnemyReaction.Seated)
            {
                var life = spawnLease;
                bool seated = TryEnterSeated();
                if (!this || !IsCommandParalyzed || spawnLease != life) return;
                seatedByCommand = seated;
            }
            if (IsCommandParalyzed)
            {
                StopNavigation();
                CancelAttack();
            }
        }

        // 独立行动锁。房间暂停仍冻结计时，瘫痪本身不改变 CanReceiveHit。
        bool TickCommandParalysis(float elapsed)
        {
            if (!IsCommandParalyzed) return false;
            paralysisRemaining = Mathf.Max(0, paralysisRemaining - elapsed);
            if (IsCommandParalyzed)
            {
                SetBehaviour(Behaviour.Seated, "精英指挥被打断，本机停转");
                return true;
            }
            if (seatedByCommand && reaction == EnemyReaction.Seated && !enemyObj.Combat.Broken)
            {
                if (enemyObj.AnimationActive && enemyObj.SupportsReactions) enemyObj.RecoverReactionAnimation();
                reaction = EnemyReaction.None;
            }
            seatedByCommand = false;
            return false;
        }

        internal void ConfigureSummons(EnemyLease lease, EliteSummonConfig config)
        {
            EndSummons();
            if (config) summons = new EliteSummonSession(lease, config);
        }

        void EndSummons()
        {
            memberOf = null;
            paralysisRemaining = 0;
            seatedByCommand = false;
            var previous = summons;
            summons = null;
            previous?.Close();
        }

        /// <summary>绑定到精英本次出生。晚返回的异步结果只能操作自己的租约。</summary>
        sealed class EliteSummonSession
        {
            readonly EnemyLease owner;
            readonly EliteSummonConfig config;
            readonly List<EnemyLease> children = new();
            readonly SummonApproachSlots approach = new();
            readonly EliteCommandFormation formation;
            CancellationTokenSource pending;
            int waves;
            float retryAt, windupEnds;
            bool closed;
            public bool Commanding { get; private set; }
            public bool BelongsTo(EnemyLease lease) => !closed && owner == lease;

            public EliteSummonSession(EnemyLease owner, EliteSummonConfig config)
            {
                this.owner = owner;
                this.config = config;
                formation = owner.Controller.GetComponent<EliteCommandFormation>();
                if (!formation && config.formationMaterial)
                    formation = owner.Controller.gameObject.AddComponent<EliteCommandFormation>();
            }

            public bool TryApproach(EnemyLease child, Transform target, Vector3 floor, out Vector3 destination)
            {
                destination = default;
                return !closed && owner.Controller.target == target &&
                    approach.TryGetDestination(child, children, target, floor, Time.time, out destination);
            }

            public void Tick(Transform target, float now)
            {
                if (closed || pending != null || now < retryAt || (Commanding && now < windupEnds)) return;
                int alive = 0;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    var child = children[i];
                    if (!owner.Owner.IsCurrent(child)) children.RemoveAt(i);
                    else if (!child.Defeated && !child.Controller.IsDead) alive++;
                }
                int count = config.CountForWave(waves + 1, alive);
                if (!Commanding)
                {
                    if (count == 0 && alive == 0) return;
                    windupEnds = now + config.commandDuration;
                    Commanding = true;
                    if (formation) formation.Present(owner, children, config.formationMaterial);
                    owner.Controller.StopNavigation();
                    owner.Controller.CancelAttack();
                    return;
                }
                if (count == 0) { FinishCommand(now); return; }
                pending = new CancellationTokenSource();
                _ = SpawnWave(target, count, pending);
            }

            async Task SpawnWave(Transform target, int count, CancellationTokenSource request)
            {
                var manager = owner.Owner;
                var staged = new List<EnemyLease>(count);
                bool Current() => !closed && !request.IsCancellationRequested && manager && manager.IsCurrent(owner) &&
                    owner.Controller.CanReceiveHit && owner.Controller.HasTarget && owner.Controller.target == target &&
                    CombatDamageReceiver.IsDetectable(target);
                try
                {
                    var origin = owner.Controller.transform.position;
                    var rotation = owner.Controller.transform.rotation;
                    for (int i = 0; i < count; i++)
                    {
                        if (!Current()) return;
                        var offset = Quaternion.AngleAxis(360f * i / count, Vector3.up) * Vector3.forward * config.radius;
                        var child = await manager.SpawnEnemyAsync(config.minion.enemyId, origin + offset, rotation, target, true, request.Token);
                        staged.Add(child);
                        if (!Current() || !manager.IsCurrent(child)) return;
                        child.Controller.SetSuspended(true);
                    }
                    if (!Current() || staged.Exists(child => !manager.IsCurrent(child))) return;
                    var born = staged.ToArray();
                    children.AddRange(born);
                    staged.Clear();
                    waves++;
                    foreach (var child in born) child.Controller.memberOf = this;
                    FinishCommand(owner.Controller.clock);
                    foreach (var child in born)
                        if (Current() && manager.IsCurrent(child))
                            child.Controller.SetSuspended(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception error)
                {
                    if (Current()) Debug.LogWarning("精英召唤失败，将在冷却后重试：" + error.Message, owner.Controller);
                }
                finally
                {
                    foreach (var child in staged) if (manager) manager.Recycle(child);
                    if (ReferenceEquals(pending, request))
                    {
                        if (Commanding) FinishCommand(owner.Controller.clock);
                        pending = null;
                    }
                    request.Dispose();
                }
            }

            void FinishCommand(float now)
            {
                Commanding = false;
                retryAt = now + config.cooldown;
                if (formation) formation.Hide();
                if (owner.Controller && owner.Controller.CurrentBehaviour == Behaviour.Commanding)
                    owner.Controller.SetBehaviour(Behaviour.Idle, "指挥结束");
            }

            public void LoseTarget(float now)
            {
                if (!Commanding) return;
                FinishCommand(now);
                pending?.Cancel();
            }

            public void Interrupt(float now)
            {
                if (closed || !Commanding) return;
                LoseTarget(now);
                // 结束攻击可能回调回池；每次操作都要重新确认租约与所属。
                foreach (var child in children.ToArray())
                    if (!closed && owner.Owner && owner.Owner.IsCurrent(child) && !child.Defeated &&
                        !child.Controller.IsDead && ReferenceEquals(child.Controller.memberOf, this))
                        child.Controller.ParalyzeByCommand(config.paralysisDuration);
            }

            public void SetSuspended(bool value)
            {
                if (value) LoseTarget(owner.Controller.clock);
                foreach (var child in children.ToArray())
                    if (!closed && owner.Owner && owner.Owner.IsCurrent(child)) child.Controller.SetSuspended(value);
            }

            public void ChangeTarget(Transform target)
            {
                approach.Clear();
                LoseTarget(owner.Controller.clock);
                foreach (var child in children.ToArray())
                    if (!closed && owner.Owner && owner.Owner.IsCurrent(child)) child.Controller.SetTarget(target);
            }

            public void Close()
            {
                closed = true;
                Commanding = false;
                if (formation) formation.Hide();
                approach.Clear();
                pending?.Cancel();
                foreach (var child in children) if (owner.Owner) owner.Owner.Recycle(child);
                children.Clear();
            }
        }
    }
}

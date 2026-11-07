using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Events;
using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>单次攻击的快照、时间窗和去重。Controller 只提交动画进度与取消。</summary>
    public sealed class EnemyAttackExecution
    {
        readonly EnemyObj owner;
        readonly HashSet<CombatDamageReceiver> hitReceivers = new();
        readonly List<CombatDamageReceiver> candidates = new();
        readonly string sourceId = Guid.NewGuid().ToString("N");
        long sequence;
        int version, index;
        float previousProgress;
        bool evaluating, windowOpened;
        public AttackData Data { get; private set; }
        public string Id { get; private set; }
        public bool Active { get; private set; }
        public EnemyAttackExecution(EnemyObj owner) => this.owner = owner;

        public void Begin(int attackIndex, AttackData data)
        {
            End(); Data = data.Snapshot(); index = attackIndex;
            Id = "enemy:" + sourceId + ":" + (++sequence);
            previousProgress = -1; windowOpened = false; hitReceivers.Clear(); Active = true;
        }
        public void End() { ++version; Active = false; }

        /// <returns>本次更新第一次经过生效起点，用于接触通知（不等同命中）。</returns>
        public bool Evaluate(float progress, bool checkOcclusion, float eyeHeight, LayerMask obstructionMask)
        {
            if (!Active || evaluating || !float.IsFinite(progress) || progress < 0 || progress < previousProgress) return false;
            // 低帧率跨过整个窗口也检测一次；超过窗口后不继续造成伤害。
            bool overlaps = progress >= Data.activeStart && (progress <= Data.activeEnd || previousProgress < Data.activeEnd);
            previousProgress = Mathf.Max(previousProgress, progress);
            if (!overlaps) return false;
            bool opened = !windowOpened; windowOpened = true;
            int token = version;
            var data = Data; string id = Id; int attack = index;
            evaluating = true;
            try
            {
                CombatDamageReceiver.CopyActive(candidates);
                Vector3 center = data.Center(owner.transform), forward = AttackData.HorizontalRotation(owner.transform) * Vector3.forward;
                foreach (var receiver in candidates)
                {
                    if (!Active || version != token || !owner || !owner.isActiveAndEnabled) break;
                    if (!receiver || !receiver.isActiveAndEnabled || receiver.transform == owner.transform || receiver.transform.IsChildOf(owner.transform) ||
                        hitReceivers.Contains(receiver) || !receiver.CanReceiveDamage ||
                        !data.Contains(center, forward, receiver.HitPosition) ||
                        !owner.HasLineOfSightTo(receiver.HitTransform, receiver.HitPosition, checkOcclusion, eyeHeight, obstructionMask)) continue;
                    // 在用户代码前记账，阻止回调重入造成同一攻击重复命中。
                    hitReceivers.Add(receiver);
                    var hit = new EnemyAttackHit(owner, receiver, id, attack, data.damage, receiver.HitPosition);
                    try { if (receiver.TryReceiveDamage(hit)) EventCenter.Publish(EnemyEvents.DamageAccepted, hit); }
                    catch (Exception error) { Debug.LogWarning("敌人受击接收器异常，已跳过本次目标：" + error, receiver); }
                }
            }
            finally { evaluating = false; }
            return opened;
        }
    }
}

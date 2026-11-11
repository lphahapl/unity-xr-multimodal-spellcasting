using UnityEngine;

namespace NarutoVR.SealCombat.Enemies
{
    public sealed partial class EnemyController
    {
        static readonly int DeathState = Animator.StringToHash("Base Layer.Dead");
        EnemyLease spawnLease;
        bool keepDeadInstanceForReset, retiredDeath;
        DeathPlayback deathPlayback;

        sealed class DeathPlayback
        {
            public EnemyLease Lease;
            public EnemyCombatState Life;
            public Animator Animator;
            public AnimatorCullingMode PreviousCulling;
            public AnimatorUpdateMode PreviousUpdateMode;
            public bool Paused, AnimatorConfigured, Entered, ForcedEntry;
            public float ResumeSpeed, Waiting;
        }

        internal void BindDeathLease(EnemyLease lease) => spawnLease = lease;

        /// <summary>预放置的房间敌人由该房间复位；没有房间和池所有者的敌人播完后销毁。</summary>
        internal void SetSceneRespawnOwner(bool keepForReset) => keepDeadInstanceForReset = keepForReset;

        void BeginDeathPlayback()
        {
            if (deathPlayback != null) return;
            var playback = new DeathPlayback { Lease = spawnLease, Life = enemyObj.Combat, Animator = enemyObj.Animator };
            deathPlayback = playback;
            if (spawnLease != null && spawnLease.Owner && spawnLease.Owner.IsCurrent(spawnLease)) spawnLease.Defeated = true;
            var player = playback.Animator;
            if (player && player.runtimeAnimatorController)
            {
                playback.PreviousCulling = player.cullingMode;
                playback.PreviousUpdateMode = player.updateMode;
                playback.AnimatorConfigured = true;
                player.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (player.updateMode == AnimatorUpdateMode.UnscaledTime) player.updateMode = AnimatorUpdateMode.Normal;
            }
            SetDeathPlaybackSuspended(suspended);
        }

        void SetDeathPlaybackSuspended(bool pause)
        {
            var state = deathPlayback;
            if (state == null || state.Paused == pause) return;
            state.Paused = pause;
            if (!state.Animator) return;
            if (pause)
            {
                state.ResumeSpeed = state.Animator.speed;
                state.Animator.speed = 0;
            }
            else state.Animator.speed = state.ResumeSpeed;
        }

        void CancelDeathPlayback()
        {
            var state = deathPlayback;
            if (state == null) return;
            deathPlayback = null;
            if (!state.Animator) return;
            if (state.Paused) state.Animator.speed = state.ResumeSpeed;
            if (state.AnimatorConfigured)
            {
                state.Animator.cullingMode = state.PreviousCulling;
                state.Animator.updateMode = state.PreviousUpdateMode;
            }
        }

        void OnDeathPlaybackDisabled()
        {
            var state = deathPlayback;
            if (state == null) return;
            CancelDeathPlayback();
            if (state.Lease != null)
            {
                // 常规退池会先从活跃表摘除租约，只有外部中断的当前死者才走销毁。
                if (state.Lease.Owner && state.Lease.Owner.DiscardInterruptedDeath(state.Lease)) retiredDeath = true;
            }
            else if (keepDeadInstanceForReset)
            {
                // 仅禁用控制器时也要隐藏剩余渲染和碰撞；保留房间的下次复位入口。
                if (gameObject.activeInHierarchy && !enabled) { gameObject.SetActive(false); enabled = true; }
            }
            else { retiredDeath = true; Destroy(gameObject); }
        }

        void LateUpdate()
        {
            var state = deathPlayback;
            if (state == null || state.Paused || Time.timeScale <= 0) return;
            if (!IsDead || !ReferenceEquals(state.Life, enemyObj.Combat)) { CancelDeathPlayback(); return; }
            var player = state.Animator;
            if (!player || !player.isActiveAndEnabled || !player.runtimeAnimatorController || !player.HasState(0, DeathState))
            {
                FinishDeathPlayback(state);
                return;
            }
            if (player.speed == 0) return;
            var current = player.GetCurrentAnimatorStateInfo(0);
            if (current.fullPathHash == DeathState)
            {
                state.Entered = true;
                if (current.normalizedTime >= 1 || current.length <= 0) FinishDeathPlayback(state);
                return;
            }
            // 正常过渡后依据实际 Dead 状态的 normalizedTime 回收，慢放和较长片段均可完整播完。
            // 动画图异常跳出 Dead 或迟迟不进入时，最多显式重入一次。
            state.Waiting += Time.deltaTime * Mathf.Abs(player.speed);
            if (!state.Entered && state.Waiting < Mathf.Max(1, reactionTimeout)) return;
            if (state.ForcedEntry) { FinishDeathPlayback(state); return; }
            state.Waiting = 0; state.Entered = false; state.ForcedEntry = true;
            player.Play(DeathState, 0, 0);
        }

        void FinishDeathPlayback(DeathPlayback state)
        {
            if (!ReferenceEquals(state, deathPlayback) || !ReferenceEquals(state.Life, enemyObj.Combat) ||
                !ReferenceEquals(state.Lease, spawnLease)) return;
            CancelDeathPlayback();
            if (state.Lease != null)
            {
                if (state.Lease.Owner) state.Lease.Owner.Recycle(state.Lease);
            }
            else if (keepDeadInstanceForReset) gameObject.SetActive(false);
            else { retiredDeath = true; Destroy(gameObject); }
        }
    }
}

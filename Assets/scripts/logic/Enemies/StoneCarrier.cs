using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>工奴的非战斗行动。导航、暂停、受击和死亡均由同一个 EnemyController 调度。</summary>
    [DisallowMultipleComponent]
    public sealed class StoneCarrier : MonoBehaviour
    {
        public StoneCarrierProfile profile;
        public BallastStone Stone { get; private set; }
        public bool Retreating { get; private set; }
        public string Status { get; private set; }
        NavMeshPath path;
        Transform[] route = Array.Empty<Transform>();
        Vector3 origin, lateral, escape;
        int patrolIndex;
        float pause, remaining;
        static readonly int CarryState = Animator.StringToHash("Base Layer.StoneCarry");
        static readonly int CarrySpeed = Animator.StringToHash("StoneCarrySpeed");
        readonly List<AnimatorClipInfo> clips = new(1);
        Animator figure;
        Transform[] carryJoints;
        float heldPhase;
        bool interrupted, suspended;
        float playbackRate;
        bool OwnsCarriedStone => Stone && Stone.State == BallastStone.Phase.Carried && Stone.transform.parent == transform;

        EnemyLease owner;
        internal void Prepare(EnemyLease lease)
        {
            if (!ReferenceEquals(owner, lease))
            {
                if (OwnsCarriedStone)
                {
                    Stone.gameObject.SetActive(false);
                    Destroy(Stone.gameObject);
                }
                Stone = null;
            }
            owner = lease;
            heldPhase = 0;
            interrupted = suspended = false;
            playbackRate = 0;
            origin = transform.position;
            lateral = transform.right;
            route = Array.Empty<Transform>();
            patrolIndex = 0;
            pause = remaining = 0;
            Retreating = false;
            if (profile && profile.stone && !Stone)
            {
                Stone = Instantiate(profile.stone, transform, false);
                Stone.Carry(profile.carryOffset);
            }
        }

        /// <summary>重生并重置原 Animator 之后显式进入搬运态。普通敌人没有此入口。</summary>
        internal void BeginCarryPose(Animator animator)
        {
            figure = null;
            if (!OwnsCarriedStone || !profile || !profile.carryPose || !profile.carryPose.TryEvaluate(0, out var start) ||
                !animator || !animator.isActiveAndEnabled || !animator.HasState(0, CarryState)) return;
            figure = animator;
            carryJoints = profile.carryPose.BindJoints(animator);
            Stone.transform.SetLocalPositionAndRotation(start.position, start.rotation);
            figure.SetFloat(CarrySpeed, 0);
            figure.Play(CarryState, 0, 0);
        }

        /// <summary>只接受独占基础层的原剪辑。反应过渡或操作者已脱手时，保留上次持姿。</summary>
        public bool TryGetCarryPhase(out float normalizedPhase)
        {
            normalizedPhase = 0;
            if (!isActiveAndEnabled || !OwnsCarriedStone || interrupted || !figure || !figure.isActiveAndEnabled ||
                !profile || !profile.carryPose || figure.IsInTransition(0)) return false;
            var state = figure.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != CarryState) return false;
            figure.GetCurrentAnimatorClipInfo(0, clips);
            if (clips.Count != 1 || clips[0].clip != profile.carryPose.Clip) return false;
            normalizedPhase = Mathf.Repeat(state.normalizedTime, 1f);
            return true;
        }

        void LateUpdate()
        {
            // Animator 已在本帧求完骨姿。镇石与手使用同一次求值的相位。
            if (!TryGetCarryPhase(out var phase) || !profile.carryPose.TryEvaluate(phase, out var pose)) return;
            heldPhase = phase;
            profile.carryPose.ApplyJoints(transform, carryJoints, phase);
            Stone.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
        }

        internal void SetCarryMoving(float groundSpeed)
        {
            playbackRate = profile ? groundSpeed / Mathf.Max(.05f, profile.clipWalkSpeed) : 0;
            if (!isActiveAndEnabled || !OwnsCarriedStone || !figure || !figure.isActiveAndEnabled) return;
            if (interrupted || figure.GetCurrentAnimatorStateInfo(0).fullPathHash != CarryState)
            {
                if (suspended || figure.IsInTransition(0)) return;
                figure.Play(CarryState, 0, heldPhase);
                interrupted = false;
            }
            figure.SetFloat(CarrySpeed, suspended ? 0 : playbackRate);
        }

        internal void SetCarrySuspended(bool value)
        {
            suspended = value;
            if (isActiveAndEnabled && !interrupted && OwnsCarriedStone && figure && figure.isActiveAndEnabled)
                figure.SetFloat(CarrySpeed, value ? 0 : playbackRate);
        }

        internal void InterruptCarryPose()
        {
            if (TryGetCarryPhase(out var phase)) heldPhase = phase;
            interrupted = true;
        }

        internal void EndCarryPose()
        {
            if (figure && figure.isActiveAndEnabled) figure.SetFloat(CarrySpeed, 0);
            figure = null;
            carryJoints = null;
        }

        void OnDisable()
        {
            if (figure && figure.isActiveAndEnabled) figure.SetFloat(CarrySpeed, 0);
        }

        void OnDestroy()
        {
            EndCarryPose();
            if (OwnsCarriedStone) Destroy(Stone.gameObject);
        }

        internal void AssignRoute(Transform[] points) => route = points ?? Array.Empty<Transform>();
        internal void Drop()
        {
            EndCarryPose();
            if (OwnsCarriedStone) Stone.Drop();
        }

        int PointCount => route.Length == 0 ? 2 : route.Length;
        bool TryPoint(int index, out Vector3 point)
        {
            if (route.Length == 0)
            {
                point = origin + lateral * ((index == 0 ? 1 : -1) * profile.defaultRadius);
                return true;
            }
            point = route[index] ? route[index].position : default;
            return route[index];
        }

        internal void RetreatFrom(Transform attacker, EnemyController controller)
        {
            var agent = controller.Agent;
            if (!profile || !agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            path ??= new NavMeshPath();
            // 先过滤断网面的点，再从真正更远的候选中取最远点；无逃路时保留正常巡逻。
            float farthest = (transform.position - attacker.position).sqrMagnitude;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            bool found = false;
            for (int i = 0; i < PointCount; i++)
            {
                if (!TryPoint(i, out var p) || !NavMesh.SamplePosition(p, out var hit, controller.navSampleRadius, filter) ||
                    !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float distance = (hit.position - attacker.position).sqrMagnitude;
                if (distance <= farthest + .001f) continue;
                farthest = distance;
                escape = hit.position;
                found = true;
            }
            if (!found) return;
            Retreating = true;
            var pace = owner?.Pace;
            remaining = pace ? pace.retreatSeconds : profile.retreatDuration;
            pause = 0;
            controller.Entity.RequestRepath();
        }

        internal bool Tick(EnemyController controller, float clock, float dt)
        {
            var entity = controller.Entity;
            if (!profile) { entity.StopNavigation(); Status = "缺少背石工奴配置"; return false; }
            if (pause > 0)
            {
                pause -= dt;
                Status = "工奴巡逻到点停留";
                return false;
            }
            Vector3 point = escape;
            if (!Retreating && !TryPoint(patrolIndex, out point))
            {
                Advance(entity);
                Status = "跳过空巡逻点";
                return false;
            }
            var pace = owner?.Pace;
            float patrol = pace ? pace.patrolSpeed : profile.patrolSpeed;
            float retreat = pace ? pace.engagedSpeed : profile.retreatSpeed;
            float speed = Retreating ? retreat : patrol;
            if (!entity.TryMoveTo(point, speed, profile.arrivalDistance, clock, controller.repathSeconds,
                    controller.navSampleRadius, out var failure))
            {
                Advance(entity);
                Status = failure ?? "等待可达巡逻点";
                return false;
            }
            if (Retreating) remaining -= dt;
            if (entity.HasArrived(profile.arrivalDistance + .05f) || (Retreating && remaining <= 0))
            {
                Advance(entity);
                Status = "工奴到点或撤离时间结束";
                return false;
            }
            entity.RotateTowards(entity.DesiredVelocity, controller.turnSpeed, dt);
            Status = Retreating ? "背石工奴受击撤离" : "背石工奴缓慢巡逻";
            return true;
        }

        void Advance(EnemyObj entity)
        {
            Retreating = false;
            patrolIndex = (patrolIndex + 1) % PointCount;
            var pace = owner?.Pace;
            pause = pace ? pace.patrolWait : profile.patrolPause;
            entity.StopNavigation();
            entity.RequestRepath();
        }
    }
}

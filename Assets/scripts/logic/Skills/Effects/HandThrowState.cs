using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>选择旧版前推释放或握持投掷输入路径，供配置资产序列化。</summary>
    public enum ProjectileReleaseMode { [InspectorName("旧版前推＋头向瞄准")] ForwardPush, [InspectorName("握持后甩手松开")] GripThrow }

    /// <summary>握持确认、松手确认、轨迹拟合与辅助发射参数，创建状态机时复制使用。</summary>
    [Serializable]
    public sealed class HandThrowSettings
    {
        [Header("辅助发射（各技能独立配置）")]
        [InspectorName("动作评分与伸手保持")] public AssistedThrowSettings assisted=new();
        [Header("握持确认与轨迹")]
        [InspectorName("允许三指张开松拳"), Tooltip("至少三指为官方Open且无Closed；仅用于已握住物体的投掷，不改变闪避张掌要求。")]
        public bool allowPartialFistRelease;
        [InspectorName("握拳或捏合确认秒数"), Min(.02f)] public float gripSeconds = .06f;
        [InspectorName("松手确认秒数"), Min(.01f)] public float releaseSeconds = .025f;
        [InspectorName("取样轨迹窗口（秒）"), Range(.06f,.3f)] public float velocityWindow = .12f;
        [InspectorName("松手前排除尾段（秒）"), Range(0,.04f)] public float releaseLookback = .01f;
        [InspectorName("近期样本权重衰减（秒）"), Range(.02f,.15f)] public float velocityWeightTime = .05f;
        [InspectorName("最大轨迹拟合误差（米）"), Range(.005f,.06f)] public float maximumFitError = .025f;
        [InspectorName("半张掌等待上限（秒）"), Range(.06f,.3f)] public float releaseTimeout = .18f;
        [InspectorName("最小轨迹时间（秒）"), Min(.02f)] public float minimumMotionSeconds = .04f;
        [InspectorName("最低甩手速度（米/秒）"), Min(.1f)] public float minimumSpeed = .65f;
        [InspectorName("最低相对身体位移（米）"), Min(.01f)] public float minimumDistance = .055f;
        [InspectorName("最大合理速度（米/秒）"), Min(1)] public float maximumSpeed = 12;
        [Header("追踪保护与捏合")]
        [InspectorName("最大样本年龄（秒）"), Min(.02f)] public float sampleMaxAge = .15f;
        [InspectorName("最大观测间隔（秒）"), Min(.02f)] public float sampleMaxGap = .12f;
        [InspectorName("单次跳变拒绝距离（米）"), Min(.1f)] public float maximumJump = .3f;
        [InspectorName("拇食指捏合距离（米）"), Min(.005f)] public float pinchCloseDistance = .025f;
        [InspectorName("拇食指松开距离（米）"), Min(.01f)] public float pinchOpenDistance = .045f;
        public HandThrowSettings Copy() { var copy=(HandThrowSettings)MemberwiseClone(); copy.assisted=(assisted??new AssistedThrowSettings()).Copy(); return copy; }
        public bool Valid => float.IsFinite(gripSeconds) && gripSeconds >= .02f && float.IsFinite(releaseSeconds) && releaseSeconds >= .01f &&
            float.IsFinite(velocityWindow) && velocityWindow >= .06f && velocityWindow <= .3f &&
            float.IsFinite(releaseLookback) && releaseLookback >= 0 && releaseLookback <= .04f &&
            float.IsFinite(velocityWeightTime) && velocityWeightTime >= .02f && velocityWeightTime <= .15f &&
            float.IsFinite(maximumFitError) && maximumFitError >= .005f && maximumFitError <= .06f &&
            float.IsFinite(releaseTimeout) && releaseTimeout >= releaseSeconds && releaseTimeout <= .3f &&
            float.IsFinite(minimumMotionSeconds) && minimumMotionSeconds >= .02f && minimumMotionSeconds <= velocityWindow - releaseLookback &&
            float.IsFinite(minimumSpeed) && minimumSpeed > 0 && float.IsFinite(maximumSpeed) && maximumSpeed > minimumSpeed &&
            float.IsFinite(minimumDistance) && minimumDistance > 0 && float.IsFinite(sampleMaxAge) && sampleMaxAge > 0 &&
            float.IsFinite(sampleMaxGap) && sampleMaxGap > 0 && float.IsFinite(maximumJump) && maximumJump > 0 &&
            float.IsFinite(pinchCloseDistance) && pinchCloseDistance > 0 && float.IsFinite(pinchOpenDistance) && pinchOpenDistance > pinchCloseDistance &&
            (assisted==null || assisted.Valid);
    }

    /// <summary>同一次手部观测的握持特征及时间戳，用于检测数据过期、不同步和松手边沿。</summary>
    [Serializable]
    public struct ThrowGripSample
    {
        public double observed;
        public bool fistValid, fistClosed, fistOpen, pinchValid, pinchClosed, pinchOpen;
        public Vector3 wristToKnucklesWorld; // Zero means missing or invalid.
    }

    /// <summary>轨迹拟合结果，包含世界速度、相对头部速度及有效性等诊断数据。</summary>
    [Serializable]
    public struct HandThrowEstimate
    {
        public double onset, start, end;
        public int samples;
        public Vector3 velocity;
        public float rmsError, relativeDistance;
        public bool accepted;
        public string reason;
    }

    /// <summary>跟踪握持到松手的边沿及最近手部轨迹，输出速度估计，由效果执行器完成瞄准与生成。</summary>
    public sealed class HandThrowState
    {
        readonly HandThrowSettings settings;
        enum Grip { None, Fist, Pinch }
        struct Sample { public double time; public Vector3 hand, head; }
        readonly List<Sample> samples = new(24);
        Grip grip;
        bool armed;
        double heldSince = double.NaN, releaseOnset = double.NaN, releasedSince = double.NaN, last = double.NegativeInfinity;
        Vector3 pendingVelocity, previousHand, previousHead;
        public string Status { get; private set; } = "先握拳或捏住火球";
        public bool Armed => armed;
        public bool InputValid { get; private set; }
        public bool ReleasePending => armed && !double.IsNaN(releaseOnset);
        public float GripProgress { get; private set; }
        public float ReleaseProgress { get; private set; }
        public HandThrowEstimate PreviewEstimate { get; private set; }
        public long CandidateId { get; private set; }
        public HandThrowEstimate Estimate { get; private set; }
        public HandThrowState(HandThrowSettings settings)
        {
            if (settings == null || !settings.Valid) throw new ArgumentException("投掷参数无效");
            this.settings = settings.Copy();
        }
        public void Reset()
        {
            grip = Grip.None; armed = false; heldSince = releaseOnset = releasedSince = double.NaN;
            last = double.NegativeInfinity; samples.Clear(); pendingVelocity = Vector3.zero;
            Estimate = default;
            PreviewEstimate = default; GripProgress = ReleaseProgress = 0;
            InputValid = false;
            Status = "先握拳或捏住火球";
        }
        /// <summary>以采样时间去重和校验轨迹，跟踪握持到松手的转换，确认后输出估计速度。</summary>
        public Vector3 Process(Vector3 hand, Vector3 head, ThrowGripSample input, double now)
        {
            if (!double.IsFinite(input.observed) || !double.IsFinite(now) || input.observed > now + .01 || now - input.observed > settings.sampleMaxAge ||
                !Finite(hand) || !Finite(head)) { Reset(); Status = "追踪无效：恢复后重新握住"; return Vector3.zero; }
            if (input.observed < last) { Reset(); return Vector3.zero; }
            if (input.observed == last) return Vector3.zero;
            if (double.IsFinite(last) && (input.observed - last > settings.sampleMaxGap ||
                Vector3.Distance(hand, previousHand) > settings.maximumJump || Vector3.Distance(head, previousHead) > settings.maximumJump))
            { Reset(); Status = "观测中断：请重新握持"; return Vector3.zero; }
            last = input.observed; previousHand = hand; previousHead = head;
            InputValid = input.fistValid || input.pinchValid;
            if (grip == Grip.None)
            {
                if (input.fistValid && input.fistClosed) grip = Grip.Fist;
                else if (input.pinchValid && input.pinchClosed) grip = Grip.Pinch;
                else return Vector3.zero;
                heldSince = input.observed;
            }
            bool valid = grip == Grip.Fist ? input.fistValid : input.pinchValid;
            bool closed = grip == Grip.Fist ? input.fistClosed : input.pinchClosed;
            bool open = grip == Grip.Fist ? input.fistOpen : input.pinchOpen;
            if (!valid) { Reset(); Status = "握持特征丢失：不会自动发射"; return Vector3.zero; }
            if (closed)
            {
                if (!double.IsNaN(releaseOnset))
                {
                    // 松手被撤回后必须重新确认握持，并重新采集轨迹。
                    samples.Clear(); armed = false; heldSince = input.observed; Estimate = default;
                }
                Append(hand, head, input.observed);
                if (double.IsNaN(heldSince)) heldSince = input.observed;
                GripProgress = Mathf.Clamp01((float)((input.observed-heldSince)/settings.gripSeconds));
                if (input.observed - heldSince >= settings.gripSeconds) armed = true;
                releaseOnset = releasedSince = double.NaN; pendingVelocity = Vector3.zero;
                ReleaseProgress = 0;
                PreviewEstimate = armed ? Fit(input.observed) : default;
                Status = armed ? "已握住：甩手并松开，沿头向预测线发射" : "确认握持中";
                return Vector3.zero;
            }
            if (!armed) { Reset(); return Vector3.zero; }
            if (double.IsNaN(releaseOnset))
            {
                releaseOnset = input.observed;
                CandidateId++;
                // 在握紧转为中间态或张开时冻结松手起点，避免等待完全张开造成延迟。
                // 剔除最新一小段轨迹，降低张指和追踪预测噪声对方向的干扰。
                Estimate = Fit(releaseOnset);
                pendingVelocity = Estimate.accepted ? Estimate.velocity : Vector3.zero;
            }
            if (input.observed - releaseOnset > settings.releaseTimeout)
            { Reset(); Status = "松手确认超时：请重新握住"; return Vector3.zero; }
            if (!open) { releasedSince = double.NaN; ReleaseProgress = 0; Status = "已锁定投掷轨迹：等待有效松手"; return Vector3.zero; }
            if (double.IsNaN(releasedSince)) releasedSince = input.observed;
            ReleaseProgress = Mathf.Clamp01((float)((input.observed-releasedSince)/settings.releaseSeconds));
            if (input.observed - releasedSince < settings.releaseSeconds) return Vector3.zero;
            Vector3 result = pendingVelocity;
            HandThrowEstimate estimate = Estimate;
            Reset();
            Estimate = estimate;
            Status = result == Vector3.zero ? estimate.reason + "：重新握住后再投掷" : "已投掷（松手前轨迹拟合）";
            return result;
        }

        void Append(Vector3 hand, Vector3 head, double observed)
        {
            while (samples.Count > 0 && observed - samples[0].time > settings.velocityWindow + settings.sampleMaxGap) samples.RemoveAt(0);
            samples.Add(new Sample { time = observed, hand = hand, head = head });
        }

        /// <summary>对松手前的轨迹窗口进行稳健拟合，生成释放速度及质量检查结果。</summary>
        HandThrowEstimate Fit(double onset)
        {
            var estimate = new HandThrowEstimate { onset = onset, reason = "有效轨迹不足" };
            int first = 0, end = samples.Count;
            while (first < end && samples[first].time < onset - settings.velocityWindow - 1e-6) first++;
            while (end > first && samples[end - 1].time > onset - settings.releaseLookback + 1e-6) end--;
            estimate.samples = end - first;
            if (estimate.samples < 4) return estimate;
            Sample a = samples[first], b = samples[end - 1];
            estimate.start = a.time; estimate.end = b.time;
            float span = (float)(b.time - a.time);
            if (span + 1e-6f < settings.minimumMotionSeconds) return estimate;

            // 用加权最小二乘拟合 p(t)=截距+速度*t，再用两轮 Huber 重加权减弱异常点影响。
            // 时间相对松手边沿、位置相对首个采样点计算，以改善数值稳定性。
            Vector3 velocity = Vector3.zero, intercept = Vector3.zero;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                double sum = 0, st = 0, stt = 0;
                Vector3 sp = Vector3.zero, stp = Vector3.zero;
                for (int i = first; i < end; i++)
                {
                    Sample sample = samples[i]; float t = (float)(sample.time - onset);
                    Vector3 p = sample.hand - a.hand;
                    float w = Mathf.Exp(t / settings.velocityWeightTime);
                    if (iteration > 0)
                    {
                        float error = (p - intercept - velocity * t).magnitude;
                        w *= Mathf.Min(1, settings.maximumFitError * .5f / Mathf.Max(.00001f, error));
                    }
                    sum += w; st += w * t; stt += w * t * t; sp += w * p; stp += w * t * p;
                }
                double denominator = stt - st * st / sum;
                if (denominator <= 1e-10) return estimate;
                velocity = (stp - sp * (float)(st / sum)) / (float)denominator;
                intercept = (sp - velocity * (float)st) / (float)sum;
            }
            double squaredError = 0, weights = 0;
            for (int i = first; i < end; i++)
            {
                float t = (float)(samples[i].time - onset), w = Mathf.Exp(t / settings.velocityWeightTime);
                squaredError += w * (samples[i].hand - a.hand - intercept - velocity * t).sqrMagnitude;
                weights += w;
            }
            estimate.velocity = velocity;
            estimate.rmsError = (float)Math.Sqrt(squaredError / weights);
            Vector3 relative = (b.hand - a.hand) - (b.head - a.head);
            estimate.relativeDistance = relative.magnitude;
            float speed = velocity.magnitude;
            if (!Finite(velocity) || !float.IsFinite(estimate.rmsError)) estimate.reason = "轨迹数值无效";
            else if (estimate.rmsError > settings.maximumFitError) estimate.reason = "轨迹跳变或弯曲过大";
            else if (relative.magnitude < settings.minimumDistance || Vector3.Dot(velocity, relative) <= 0) estimate.reason = "相对身体甩手位移不足";
            else if (speed < settings.minimumSpeed) estimate.reason = "松手前速度不足";
            else if (speed > settings.maximumSpeed) estimate.reason = "松手前速度异常";
            else { estimate.accepted = true; estimate.reason = "轨迹有效"; }
            return estimate;
        }
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}

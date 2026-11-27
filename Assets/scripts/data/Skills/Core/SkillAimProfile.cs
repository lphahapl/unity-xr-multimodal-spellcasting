using UnityEngine;

namespace NarutoVR.SealCombat
{
    [CreateAssetMenu(menuName = "NarutoVR/技能配置/瞄准配置")]
    public sealed class SkillAimProfile : ScriptableObject
    {
        [InspectorName("显示术法准心")] public bool showReticle = true;
        [InspectorName("显示方向线")] public bool showDirectionLine;
        [InspectorName("准心颜色")] public Color color = new(.95f, .78f, .33f);
        [InspectorName("受遮挡颜色")] public Color blockedColor = new(1f, .32f, .08f);
        [InspectorName("准心视角半径（度）"), Range(.15f, 3f)] public float angularRadius = .65f;
        [InspectorName("视线取点距离（米，火球和剑波）"), Min(1)] public float gazeDistance = 30f;
        [InspectorName("抬腕显示阈值（低于头部，米，仅火球）"), Range(.15f,.65f)] public float wristDrop = .4f;
        [InspectorName("发射后显示秒数（仅单发吐息）"), Range(0,1)] public float launchedSeconds = .42f;

        [Header("轻辅助瞄准")]
        [InspectorName("捕捉半角（度）"), Range(0, 6)] public float assistCone = 2.5f;
        [InspectorName("最大修正角（度）"), Range(0, 3)] public float assistMaxCorrection = 1.5f;
        [InspectorName("修正比例"), Range(0, 1)] public float assistStrength = .7f;

        const string AssistKey = "NarutoVR.SkillAim.AssistEnabled";
        static bool? assistEnabled;
        public static bool AssistEnabled
        {
            get => assistEnabled ??= PlayerPrefs.GetInt(AssistKey, 1) != 0;
            set
            {
                assistEnabled = value;
                PlayerPrefs.SetInt(AssistKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        internal float RadiusAt(float distance) =>
            Mathf.Clamp(distance * Mathf.Tan(Mathf.Clamp(angularRadius,.15f,3f) * Mathf.Deg2Rad), .014f, .6f);
    }
}

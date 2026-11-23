using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SceneArt
{
    [AddComponentMenu("NarutoVR/剧情/加载进度条"), DisallowMultipleComponent]
    public sealed class SceneLoadingProgressBar : MonoBehaviour
    {
        [SerializeField, InspectorName("进度条填充"), Tooltip("指定带精灵的水平填充 Image。")]
        private Image progressFill;
        [SerializeField, InspectorName("进度百分比"), Tooltip("可选。与进度条同步更新的百分比文字。")]
        private Text percentText;

        internal Image Fill => progressFill;
        internal Text Percentage => percentText;
    }
}

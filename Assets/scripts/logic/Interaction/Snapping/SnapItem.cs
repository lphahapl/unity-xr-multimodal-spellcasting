using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace NeuGameMRTools.Runtime.Components
{
    /// <summary>标记真正移动的道具根节点；子碰撞体统一归到这里，避免吸附玩家或只移动模型的一部分。</summary>
    [DisallowMultipleComponent, AddComponentMenu("NeuGameMRTools/Interaction/可吸附道具 Snap Item")]
    public sealed class SnapItem : MonoBehaviour
    {
        [SerializeField, Tooltip("吸附期间还需暂停的位置驱动，如 Animator 或自定义移动脚本。Meta 抓取组件自动处理。")]
        Behaviour[] _additionalDrivers = System.Array.Empty<Behaviour>();

        /// <summary>在 LateUpdate 接管前再次查询，已选中的 Meta 道具等待释放，不强行抢占手部。</summary>
        public bool IsHeld
        {
            get
            {
                foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!component || !component.isActiveAndEnabled) continue;
                    if (component is Grabbable grab && grab.SelectingPointsCount > 0) return true;
                    if (component is IInteractableView view && view.State == InteractableState.Select) return true;
                }
                return false;
            }
        }

        internal List<Behaviour> GetPositionDrivers()
        {
            var result = new List<Behaviour>();
            foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
                if (component is Grabbable || component is IInteractableView) result.Add(component);
            foreach (var driver in _additionalDrivers)
                if (driver && driver != this && driver is not SnapZone && !result.Contains(driver)) result.Add(driver);
            return result;
        }
    }
}

using UnityEngine;
using Oculus.Interaction.HandGrab;

namespace NeuGameMRTools.Runtime.Components
{
    /// <summary>
    /// 远距离抓取交互控制器
    /// 自动管理物体上的远距离抓取交互组件
    /// </summary>
    [AddComponentMenu("NeuGameMRTools/Interaction/Distance Grab Interaction Controller")]
    public class DistanceGrabInteractionController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [Tooltip("Distance grab interactable component. Auto-find in children if not assigned.")]
        [SerializeField]
        private DistanceHandGrabInteractable _distanceGrabInteractable;

        [Header("Settings")]
        [Tooltip("Enable grab interaction on start")]
        [SerializeField]
        private bool _enableOnStart = true;

        #endregion

        #region Properties

        /// <summary>
        /// Whether distance grab interaction is enabled
        /// </summary>
        public bool IsGrabEnabled { get; private set; }

        /// <summary>
        /// Distance grab interactable component reference
        /// </summary>
        public DistanceHandGrabInteractable DistanceGrab => _distanceGrabInteractable;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
            SetGrabEnabled(_enableOnStart);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Enable distance grab interaction
        /// </summary>
        public void EnableGrab()
        {
            SetGrabEnabled(true);
        }

        /// <summary>
        /// Disable distance grab interaction
        /// </summary>
        public void DisableGrab()
        {
            SetGrabEnabled(false);
        }

        /// <summary>
        /// Toggle distance grab interaction state
        /// </summary>
        public void ToggleGrab()
        {
            SetGrabEnabled(!IsGrabEnabled);
        }

        /// <summary>
        /// Find distance grab interactable components in children
        /// </summary>
        public void FindComponents()
        {
            if (_distanceGrabInteractable == null)
            {
                _distanceGrabInteractable = GetComponentInChildren<DistanceHandGrabInteractable>(true);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initialize component
        /// </summary>
        private void Initialize()
        {
            FindComponents();
        }

        /// <summary>
        /// Set distance grab interaction enabled state
        /// </summary>
        /// <param name="enabled">Enable state</param>
        private void SetGrabEnabled(bool enabled)
        {
            IsGrabEnabled = enabled;

            if (_distanceGrabInteractable != null)
            {
                _distanceGrabInteractable.enabled = enabled;
            }
        }

        #endregion
    }
}

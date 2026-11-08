using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>只观察 EnemyObj 的韧性，显示层不驱动状态或扣血。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPoiseBar : MonoBehaviour
    {
        [Header("削韧条显示")]
        [InspectorName("敌人实体")] public EnemyObj enemy;
        [InspectorName("根物体上方偏移（米）")] public Vector3 offset = new(0, 2.15f, 0);
        [InspectorName("条宽（米）"), Min(.05f)] public float width = .55f;
        [InspectorName("条高（米）"), Min(.01f)] public float height = .045f;
        [InspectorName("正常颜色")] public Color normalColor = new(1, .65f, .1f);
        [InspectorName("瘫痪颜色")] public Color brokenColor = new(.65f, .25f, 1);
        RectTransform root, fill;
        Image fillImage, background;
        Camera viewer;
        void Awake()
        {
            if (!enemy) enemy = GetComponent<EnemyObj>();
            var canvasObject = new GameObject("削韧条（运行时）", typeof(RectTransform), typeof(Canvas));
            root = (RectTransform)canvasObject.transform; root.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.localScale = Vector3.one * .01f;
            background = MakeImage("背景", root, new Color(.08f, .08f, .08f, .9f));
            fillImage = MakeImage("剩余韧性", root, normalColor); fill = fillImage.rectTransform;
            fill.pivot = new Vector2(0, .5f); fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1);
        }
        static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        void LateUpdate()
        {
            if (!root || !enemy) return;
            bool visible = enemy.isActiveAndEnabled && enemy.Combat != null && !enemy.Combat.Dead;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;
            if (!viewer || !viewer.isActiveAndEnabled) viewer = Camera.main;
            root.position = enemy.transform.position + offset;
            if (viewer) root.rotation = viewer.transform.rotation;
            float w = Mathf.Max(.05f, width) * 100;
            root.sizeDelta = new Vector2(w, Mathf.Max(.01f, height) * 100);
            float ratio = enemy.Combat.Poise / enemy.Combat.MaxPoise;
            fill.sizeDelta = new Vector2(w * ratio, 0); fill.anchoredPosition = Vector2.zero;
            fillImage.color = enemy.Combat.Broken ? brokenColor : normalColor;
            background.color = enemy.Combat.Broken ? new Color(brokenColor.r * .4f, brokenColor.g * .4f, brokenColor.b * .4f, .9f) : new Color(.08f, .08f, .08f, .9f);
        }
        void OnDisable() { if (root) root.gameObject.SetActive(false); }
        void OnDestroy() { if (root) Destroy(root.gameObject); }
    }
}

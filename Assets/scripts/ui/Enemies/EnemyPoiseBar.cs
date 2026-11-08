using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>头顶名牌与双条共用一个世界画布，只读取当前敌人的战斗状态。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPoiseBar : MonoBehaviour
    {
        [Header("头顶名牌")]
        [InspectorName("敌人实体")] public EnemyObj enemy;
        [InspectorName("挂点（留空使用敌人根物体）")] public Transform anchor;
        [InspectorName("血条中心偏移（米）")] public Vector3 offset = new(0, 2.15f, 0);
        [InspectorName("条宽（米）"), Min(.05f)] public float width = .55f;
        [InspectorName("血条高度（米）"), Min(.01f)] public float height = .045f;
        [InspectorName("生命颜色")] public Color healthColor = new(.84f, .25f, .18f);
        [InspectorName("韧性颜色")] public Color normalColor = new(1, .65f, .1f);
        [InspectorName("瘫痪颜色")] public Color brokenColor = new(.65f, .25f, 1);

        const float UnitsPerMeter = 1000;
        static readonly Color TrackColor = new(.10f, .08f, .07f, .95f);
        RectTransform root;
        Image healthTrack, healthFill, poiseTrack, poiseFill;
        Text nameLabel;
        Camera viewer;
        Vector2 lastSize;
        string fallbackName;

        void Awake()
        {
            if (!enemy) enemy = GetComponent<EnemyObj>();
            fallbackName = enemy ? enemy.name.Replace("(Clone)", "").Trim() : "敌人";
            var canvasObject = new GameObject("敌人名牌", typeof(RectTransform), typeof(Canvas));
            root = (RectTransform)canvasObject.transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one / UnitsPerMeter;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            healthTrack = MakeImage("血条", root, TrackColor);
            healthFill = MakeImage("剩余生命", healthTrack.rectTransform, healthColor);
            poiseTrack = MakeImage("韧性条", root, TrackColor);
            poiseFill = MakeImage("剩余韧性", poiseTrack.rectTransform, normalColor);
            var labelObject = new GameObject("敌人名称", typeof(RectTransform), typeof(Text), typeof(Outline));
            nameLabel = labelObject.GetComponent<Text>();
            nameLabel.rectTransform.SetParent(root, false);
            nameLabel.font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            nameLabel.fontSize = 80;
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 40;
            nameLabel.resizeTextMaxSize = 80;
            nameLabel.supportRichText = false;
            nameLabel.alignment = TextAnchor.MiddleCenter;
            nameLabel.color = new Color(.99f, .95f, .85f);
            nameLabel.raycastTarget = false;
            var outline = labelObject.GetComponent<Outline>();
            outline.effectColor = new Color(.09f, .07f, .05f, .95f);
            outline.effectDistance = new Vector2(2f, -2f);
            root.gameObject.SetActive(false);
        }

        static Image MakeImage(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var image = go.GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        void Layout()
        {
            var size = new Vector2(Mathf.Max(.05f, width), Mathf.Max(.01f, height)) * UnitsPerMeter;
            if (lastSize == size) return;
            lastSize = size;
            const float gap = 7, nameHeight = 112;
            float poiseHeight = size.y * .34f;
            float fullHeight = poiseHeight + gap + size.y + gap + nameHeight;
            root.sizeDelta = new Vector2(size.x, fullHeight);
            root.pivot = new Vector2(.5f, (poiseHeight + gap + size.y * .5f) / fullHeight);
            PlaceRow(poiseTrack.rectTransform, size.x, poiseHeight, 0);
            PlaceRow(healthTrack.rectTransform, size.x, size.y, poiseHeight + gap);
            PlaceRow(nameLabel.rectTransform, size.x * 1.25f, nameHeight, poiseHeight + gap + size.y + gap);
        }

        static void PlaceRow(RectTransform row, float rowWidth, float rowHeight, float bottom)
        {
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(.5f, 0);
            row.sizeDelta = new Vector2(rowWidth, rowHeight);
            row.anchoredPosition = new Vector2(0, bottom);
        }

        static void SetFill(Image fill, float ratio, Color color)
        {
            RectTransform rect = fill.rectTransform;
            var end = new Vector2(Mathf.Lerp(.005f, .995f, Mathf.Clamp01(ratio)), .9f);
            if (rect.anchorMax != end)
            {
                rect.anchorMin = new Vector2(.005f, .1f);
                rect.anchorMax = end;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            fill.color = color;
        }

        void LateUpdate()
        {
            var combat = enemy && enemy.isActiveAndEnabled ? enemy.Combat : null;
            bool visible = combat != null && !combat.Dead;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;

            if (!viewer || !viewer.isActiveAndEnabled) viewer = Camera.main;
            root.position = (anchor ? anchor.position : enemy.transform.position) + offset;
            if (viewer)
            {
                // 世界画布的可读面朝向 -Z。用世界竖直方向保持文字基线水平。
                Vector3 fromEye = root.position - viewer.transform.position;
                if (fromEye.sqrMagnitude > .000001f)
                {
                    Vector3 up = Vector3.Cross(Vector3.up, fromEye.normalized).sqrMagnitude > .0001f ? Vector3.up : Vector3.forward;
                    root.rotation = Quaternion.LookRotation(fromEye, up);
                }
            }
            Layout();
            string displayName = enemy.Definition ? enemy.Definition.displayName : null;
            nameLabel.text = string.IsNullOrWhiteSpace(displayName) ? fallbackName : displayName;
            SetFill(healthFill, combat.Health / combat.MaxHealth, healthColor);
            SetFill(poiseFill, combat.Poise / combat.MaxPoise, combat.Broken ? brokenColor : normalColor);
            poiseTrack.color = combat.Broken ? Color.Lerp(TrackColor, brokenColor, .55f) : TrackColor;
        }

        void OnDisable() { if (root) root.gameObject.SetActive(false); }
        void OnDestroy() { if (root) Destroy(root.gameObject); }
    }
}

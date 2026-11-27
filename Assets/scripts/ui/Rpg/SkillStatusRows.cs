using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Both in-world displays read the live character state. The cells have no input targets.</summary>
    public sealed class SkillStatusRows
    {
        readonly RectTransform parent;
        readonly float width, height;
        Font font;
        readonly Dictionary<string, Cell> cells = new();
        readonly List<Cell> visible = new();
        int layoutCount = -1;

        public SkillStatusRows(RectTransform parent, float width, float height, Font font = null)
        {
            this.parent = parent;
            this.width = width;
            this.height = height;
            this.font = font;
        }

        public bool Refresh(LinearRpgController controller)
        {
            visible.Clear();
            var definitions = controller && controller.IsReady && controller.skills ? controller.skills.ConfiguredSkills : null;
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    SkillDefinition skill = definitions[i];
                    if (!skill || !controller.IsSkillAvailable(skill.skillId)) continue;
                    string key = IconKey(skill);
                    if (!cells.TryGetValue(key, out var cell))
                    {
                        if (!font) font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        cells.Add(key, cell = new Cell(parent, key, font));
                    }
                    // The two ordinary shield recipes purchase the same shield with the same timer.
                    if (!visible.Contains(cell))
                    {
                        cell.Skill = skill;
                        visible.Add(cell);
                    }
                    else if (skill.manaCost < cell.Skill.manaCost)
                        cell.Skill = skill;
                }
            }

            foreach (Cell cell in cells.Values)
                if (cell.Root.gameObject.activeSelf && !visible.Contains(cell))
                    cell.Root.gameObject.SetActive(false);

            for (int i = 0; i < visible.Count; i++)
            {
                Cell cell = visible[i];
                if (!cell.Root.gameObject.activeSelf) cell.Root.gameObject.SetActive(true);
                if (layoutCount != visible.Count || cell.Slot != i)
                    cell.Layout(i, visible.Count, width, height);
                cell.Present(controller);
            }
            layoutCount = visible.Count;
            return layoutCount > 0;
        }

        static string IconKey(SkillDefinition skill) => skill.skillId switch
        {
            "buff.zi" or "buff.voice" when skill.cooldownGroup == "shield" => "shield",
            _ => skill.skillId
        };

        sealed class Cell
        {
            static readonly Color Brass = KaogongTheme.BronzeAged;
            static readonly Color Lacquer = KaogongTheme.LacquerDark;
            static readonly Color Paper = KaogongTheme.JadeWarm;
            static readonly Color Jade = KaogongTheme.JadeAccent;
            static readonly Color Amber = KaogongTheme.GoldInlay;
            static readonly Color Cinnabar = KaogongTheme.Cinnabar;
            static readonly Color Dimmed = new(.48f, .47f, .44f, 1);

            public readonly RectTransform Root;
            public SkillDefinition Skill;
            public int Slot { get; private set; } = -1;
            readonly string key;
            readonly Image backing, icon, recovered;
            readonly Text title, state;
            int lastSeconds = -1;
            string secondsLabel;

            internal Cell(RectTransform parent, string key, Font font)
            {
                this.key = key;
                Root = Rect(parent, "术法 " + key);
                Sprite cellFrame = KaogongUISprites.Get("Button_Normal");
                if (cellFrame)
                {
                    backing = Paint(Root, Color.white);
                    backing.sprite = cellFrame;
                    backing.type = Image.Type.Sliced;
                    backing.pixelsPerUnitMultiplier = 2.0f;
                }
                else
                {
                    backing = Paint(Root, Brass);
                    RectTransform plate = Rect(Root, "漆底");
                    plate.anchorMin = Vector2.zero;
                    plate.anchorMax = Vector2.one;
                    plate.offsetMin = Vector2.one * 2;
                    plate.offsetMax = Vector2.one * -2;
                    Paint(plate, Lacquer);
                }

                icon = Paint(Rect(Root, "图标"), Color.white);
                icon.sprite = Resources.Load<Sprite>("SkillStatus/" + key);
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                recovered = Paint(Rect(Root, "恢复进度"), Color.white);
                recovered.sprite = icon.sprite;
                recovered.type = Image.Type.Filled;
                recovered.fillMethod = Image.FillMethod.Radial360;
                recovered.fillOrigin = (int)Image.Origin360.Top;
                recovered.preserveAspect = true;
                recovered.enabled = false;
                title = Label(Root, "术名", font, Paper);
                state = Label(Root, "状态", font, Jade);
            }

            internal void Layout(int index, int count, float width, float height)
            {
                Slot = index;
                const int columns = 3;
                int rows = Mathf.Max(2, Mathf.CeilToInt((float)count / columns));
                float slotWidth = width / columns, slotHeight = height / rows;
                float gap = Mathf.Min(slotWidth, slotHeight) * .05f;
                float w = slotWidth - gap * 2, h = slotHeight - gap * 2;
                Root.sizeDelta = new Vector2(w, h);
                Root.anchoredPosition = new Vector2((index % columns + .5f) * slotWidth - width * .5f,
                    height * .5f - (index / columns + .5f) * slotHeight);
                float iconSize = Mathf.Min(h * .44f, w * .60f);
                Place(icon.rectTransform, iconSize, iconSize, h * .21f);
                Place(recovered.rectTransform, iconSize, iconSize, h * .21f);
                Place(title.rectTransform, w - gap * 1.4f, h * .23f, -h * .125f);
                Place(state.rectTransform, w - gap * 1.4f, h * .22f, -h * .35f);
                SetSize(title, Mathf.Min(h * .20f, w * .21f));
                SetSize(state, Mathf.Min(h * .19f, w * .20f));
            }

            internal void Present(LinearRpgController owner)
            {
                title.text = key == "shield" ? "护盾" : Skill.displayName;
                string group = string.IsNullOrEmpty(Skill.cooldownGroup) ? Skill.skillId : Skill.cooldownGroup;
                var effects = owner.skills.effects;
                bool channel = Skill.effectDefinition is BreathEffectDefinition { mode: BreathCastMode.Sustained };
                float remaining = owner.Player.GetCooldown(group, owner.GameTime);
                bool held = effects && effects.OccupiedHandSkill == Skill;
                bool cooling = remaining > 0 && !held;
                bool showProgress = cooling && !channel && Skill.cooldownSeconds > 0 && remaining <= Skill.cooldownSeconds && icon.sprite;
                recovered.enabled = showProgress;
                if (showProgress) recovered.fillAmount = 1f - remaining / Skill.cooldownSeconds;
                icon.color = cooling ? Dimmed : Color.white;
                if (backing && backing.sprite)
                    backing.color = cooling ? new Color(0.75f, 0.72f, 0.68f, 1f) : Color.white;

                if (held)
                {
                    string action = channel ? "喷射中" : effects.HasHeldObject ? "持球" :
                        Skill.effectDefinition is ThunderSwordEffectDefinition ? "持剑" : "使用中";
                    SetState(action, Jade);
                }
                else if (cooling)
                {
                    int seconds = Mathf.CeilToInt(remaining);
                    if (seconds != lastSeconds)
                    {
                        lastSeconds = seconds;
                        secondsLabel = seconds + "秒";
                    }
                    SetState(secondsLabel, Amber);
                }
                else if (owner.Player.Mana < Skill.manaCost || (channel && owner.Player.Mana <= Skill.manaCost))
                    SetState("度力不足", Cinnabar);
                else if (effects && effects.HasOccupiedHand && UsesHandSlot(Skill))
                    SetState("手部占用", Amber);
                else if (!owner.Input.CanCast(out _, includeWristMenu: false))
                    SetState("操作中", Amber);
                else
                    SetState("就绪", Jade);
            }

            static bool UsesHandSlot(SkillDefinition skill) =>
                skill.effectDefinition is ProjectileEffectDefinition or ThunderSwordEffectDefinition or BreathEffectDefinition;

            void SetState(string message, Color color)
            {
                state.text = message;
                state.color = color;
            }

            static void Place(RectTransform rect, float w, float h, float y)
            {
                rect.sizeDelta = new Vector2(w, h);
                rect.anchoredPosition = new Vector2(0, y);
            }

            static void SetSize(Text text, float size)
            {
                text.fontSize = Mathf.Max(8, Mathf.FloorToInt(size));
                text.resizeTextMaxSize = text.fontSize;
            }

            static RectTransform Rect(RectTransform parent, string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.hideFlags = parent.gameObject.hideFlags;
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                return rect;
            }

            static Image Paint(RectTransform rect, Color color)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                image.color = color;
                return image;
            }

            static Text Label(RectTransform root, string name, Font font, Color color)
            {
                var text = Rect(root, name).gameObject.AddComponent<Text>();
                text.font = font;
                text.alignment = TextAnchor.MiddleCenter;
                text.supportRichText = false;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 8;
                text.raycastTarget = false;
                text.color = color;
                return text;
            }
        }
    }
}

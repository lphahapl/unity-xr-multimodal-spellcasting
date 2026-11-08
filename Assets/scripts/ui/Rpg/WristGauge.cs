using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>一把尺管理自己从左到右的常驻刻格。</summary>
    public sealed class WristGauge : MonoBehaviour
    {
        [SerializeField, Tooltip("从空端至满端排列")] List<WristGaugeCell> cells = new();
        [SerializeField] Text reading;
        public IReadOnlyList<WristGaugeCell> Cells => cells;
        public float Value { get; private set; }
        public float Maximum { get; private set; } = 100;
        bool initialized, animating;

        public void Bind(List<WristGaugeCell> orderedCells, Text number)
        {
            SetCellsVisible(false);
            cells = new List<WristGaugeCell>(orderedCells);
            reading = number;
            initialized = false;
            SetCellsVisible(isActiveAndEnabled);
        }

        public void SetValue(float current, float maximum) => Apply(current, maximum, initialized && isActiveAndEnabled);
        public void SetCurrentValue(float current) => SetValue(current, Maximum);
        public void SetImmediate(float current, float maximum) => Apply(current, maximum, false);

        void Apply(float current, float maximum, bool animate)
        {
            if (!float.IsFinite(current) || !float.IsFinite(maximum) || maximum <= 0) return;
            current = Mathf.Clamp(current, 0, maximum);
            if (animate && current == Value && maximum == Maximum) return;
            int count = cells.Count;
            float previous = Value / Maximum * count;
            float next = current / maximum * count;
            int newReading = Mathf.CeilToInt(current);
            if (reading && (!initialized || Mathf.CeilToInt(Value) != newReading)) reading.text = newReading.ToString();
            animating = false;
            for (int i = 0; i < count; i++)
            {
                var cell = cells[i];
                if (!cell) continue;
                float order = next >= previous ? i - Mathf.Floor(previous) : Mathf.Ceil(previous) - 1 - i;
                cell.SetFill(next - i, animate, Mathf.Clamp(order * .025f, 0, .14f));
                animating |= cell.IsAnimating;
            }
            Value = current;
            Maximum = maximum;
            initialized = true;
        }

        void LateUpdate()
        {
            if (!animating) return;
            animating = false;
            float now = Time.unscaledTime;
            foreach (var cell in cells)
            {
                if (!cell || !cell.IsAnimating) continue;
                cell.Tick(now);
                animating |= cell.IsAnimating;
            }
        }

        void SetCellsVisible(bool visible)
        {
            animating = false;
            foreach (var cell in cells)
                if (cell) cell.SetVisible(visible);
        }

        void OnEnable() => SetCellsVisible(true);
        void OnDisable() => SetCellsVisible(false);
    }
}

using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    [DisallowMultipleComponent]
    public sealed class ChapterToolBelt : MonoBehaviour
    {
        [SerializeField] internal ToolBeltInteraction interaction;
        [SerializeField] internal GameItemBase pen;
        GlyphPenController drawing;
        bool storageUnlocked, writingUnlocked;

        void Awake()
        {
            if (!interaction || !pen) return;
            interaction.enabled = false;
            if (interaction.belt) interaction.belt.gameObject.SetActive(false);
            pen.gameObject.SetActive(false);
        }

        public void Bind(LinearRpgController owner, GlyphPenController glyph)
        {
            if (!owner || !glyph || glyph.GetComponent<LinearRpgController>() != owner || !interaction ||
                !pen || pen.Kind != GameItemKind.GlyphPen)
                throw new System.ArgumentException("章节工具带、实笔与画符控制器须绑定同一玩家");
            drawing = glyph;
            interaction.player = owner;
            glyph.enabled = false;
            glyph.BindBeltPen(interaction, pen);
            ApplyAvailability();
        }

        // The chapter calls these with its actual unlocked or restored state.
        public void SetStorageAvailable(bool value)
        {
            storageUnlocked = value;
            ApplyAvailability();
        }

        public void SetWritingAvailable(bool value)
        {
            writingUnlocked = value;
            ApplyAvailability();
        }

        internal bool RestorePenToHolster()
        {
            if (!storageUnlocked || !writingUnlocked || !pen.isActiveAndEnabled) return false;
            drawing.Finish();
            return interaction.RestoreToHome(pen);
        }

        void ApplyAvailability()
        {
            if (!drawing) return;
            bool canWrite = storageUnlocked && writingUnlocked;
            if (interaction.belt) interaction.belt.gameObject.SetActive(storageUnlocked);
            bool penWasHidden = !pen.gameObject.activeSelf;
            if (!canWrite) drawing.enabled = false;
            pen.gameObject.SetActive(canWrite);
            if (canWrite && penWasHidden && interaction.Initialised && !pen.Dock && !pen.IsHeld)
                pen.TryDock(pen.home, Quaternion.Euler(pen.stowEuler));
            interaction.enabled = storageUnlocked;
            if (canWrite) drawing.enabled = true;
        }
    }
}


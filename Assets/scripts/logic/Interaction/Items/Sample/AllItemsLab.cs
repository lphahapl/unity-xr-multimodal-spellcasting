using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Items
{
    // The lab grants only fixture equipment. Story rewards belong to their own chapters.
    public sealed class AllItemsLab : MonoBehaviour
    {
        public ChapterToolBelt equipment;
        public LinearRpgController player;
        public GlyphPenController drawing;
        public TravelRationPouch pouch;
        public TextMesh vitalsLabel;
        bool seeded;
        int previousHealth = -1, previousMana = -1, previousCount = -1;

        void Start()
        {
            equipment.Bind(player, drawing);
            equipment.SetStorageAvailable(true);
            equipment.SetWritingAvailable(true);
        }

        void Update()
        {
            if (!player.CanAct) return;
            if (seeded)
            {
                int hp = Mathf.CeilToInt(player.Player.Health), mana = Mathf.CeilToInt(player.Player.Mana);
                int count = pouch.InPouch;
                if (hp != previousHealth || mana != previousMana || count != previousCount)
                {
                    vitalsLabel.text = $"气血 {hp}    度力 {mana}\n袋内剩余 {count} / 4 块";
                    previousHealth = hp; previousMana = mana; previousCount = count;
                }
                return;
            }
            player.Player.Respawn(20f, 10f, player.GameTime);
            seeded = true;
        }
    }
}

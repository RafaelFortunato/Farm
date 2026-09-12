using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Farm.Farming;

namespace Farm.UI
{
    /// <summary>
    /// One item in the inventory strip: an icon, a count, and the item's own colour behind it.
    ///
    /// Authored as a prefab under Prefabs/UI and cloned once per tracked item by InventoryHud,
    /// the same way StoreMenu clones StoreRow. This script only fills the prefab in.
    /// </summary>
    public class InventoryChip : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image iconImage;
        [SerializeField] TextMeshProUGUI countLabel;

        [Tooltip("How much of the item's colour reaches the chip background. The icons have to stay "
               + "readable, so this is a wash rather than a fill.")]
        [Range(0f, 1f)] public float tintStrength = 0.35f;

        int _shown = int.MinValue;   // impossible value, so the first count always writes

        // The authored background colour, captured before anything tints it. Without this a second
        // Bind would tint the already-tinted colour and the chip would drift toward the item hue.
        Color _baseColor;
        bool _captured;

        /// <summary>Sets everything that depends on which item this is. Called once.</summary>
        public void Bind(ItemDef item)
        {
            if (item == null) return;

            name = "Chip_" + item.displayName;

            if (iconImage != null)
            {
                iconImage.sprite = item.icon;
                // A missing icon would otherwise draw as a white box over the chip.
                iconImage.enabled = item.icon != null;
            }

            if (background != null)
            {
                if (!_captured) { _baseColor = background.color; _captured = true; }
                background.color = Color.Lerp(_baseColor, item.tintColor, tintStrength);
            }
        }

        /// <summary>Sets the number. Cheap to call on every inventory change.</summary>
        public void SetCount(int count)
        {
            if (count == _shown) return;

            _shown = count;
            if (countLabel != null) countLabel.text = count.ToString();
        }
    }
}

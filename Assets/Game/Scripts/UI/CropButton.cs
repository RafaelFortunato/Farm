using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Farm.Farming;

namespace Farm.UI
{
    /// <summary>
    /// One row in the crop picker. Authored as a prefab (Prefabs/UI/CropButton) with its
    /// sprites, font and layout already set - this script only fills in the data.
    /// </summary>
    public class CropButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TextMeshProUGUI label;

        [Tooltip("The crop's own icon, so the picker reads at a glance the way the shop rows do.")]
        [SerializeField] Image iconImage;

        [Tooltip("How long the crop takes, or the level it is still waiting on.")]
        [SerializeField] TextMeshProUGUI timeLabel;

        [Tooltip("Stopwatch beside the duration, the way the sell rows put a coin beside the price.")]
        [SerializeField] Image timeIcon;

        [Tooltip("Label colour for a crop the farm has not reached yet. A greyed button alone is " +
                 "too quiet against this art - the row needs to read as locked, not as boring.")]
        public Color lockedColor = new Color(0.69f, 0.23f, 0.18f);

        CropDef _crop;
        Color _labelColor, _timeColor;
        bool _labelColorCached;

        void Reset()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>Points this row at a crop and wires the click. Called once per menu build.</summary>
        public void Bind(CropDef crop, Action<CropDef> onClick)
        {
            _crop = crop;
            if (crop == null) return;

            if (iconImage != null)
            {
                iconImage.sprite = crop.icon;
                // A missing icon would otherwise draw as a white box, the way the shop rows guard it.
                iconImage.enabled = crop.icon != null;
            }

            // The name never changes for a given crop; only the right-hand column does.
            if (label != null) label.text = crop.displayName;

            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(crop));
        }

        /// <summary>
        /// Shows what the crop is and how long it takes, or the level it is waiting on. Rebuilt
        /// here rather than in Bind because the farm can grow between two visits to the same plot.
        /// </summary>
        public void SetAvailable(bool unlocked)
        {
            if (_crop == null) return;

            // remember the authored colours once, so unlocking can put them back
            if (!_labelColorCached)
            {
                if (label != null) _labelColor = label.color;
                if (timeLabel != null) _timeColor = timeLabel.color;
                _labelColorCached = true;
            }

            if (label != null) label.color = unlocked ? _labelColor : lockedColor;

            if (timeLabel != null)
            {
                timeLabel.text = unlocked
                    ? Mathf.RoundToInt(_crop.growSeconds) + "s"
                    : "Lv " + _crop.requiredLevel;
                timeLabel.color = unlocked ? _timeColor : lockedColor;
            }

            // The stopwatch only means anything beside a duration - a locked row is showing the level
            // it is waiting on instead, and a clock next to that would read as a countdown.
            if (timeIcon != null) timeIcon.enabled = unlocked;

            if (button != null) button.interactable = unlocked;
        }
    }
}

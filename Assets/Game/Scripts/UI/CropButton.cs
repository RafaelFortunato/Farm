using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Tooltip("Label colour for a crop the farm has not reached yet. A greyed button alone is " +
             "too quiet against this art - the row needs to read as locked, not as boring.")]
    public Color lockedColor = new Color(0.69f, 0.23f, 0.18f);

    CropDef _crop;
    Color _labelColor;
    bool _labelColorCached;

    void Reset()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TextMeshProUGUI>();
    }

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
        if (label != null && _crop != null)
        {
            // remember the authored colour once, so unlocking can put it back
            if (!_labelColorCached) { _labelColor = label.color; _labelColorCached = true; }

            label.text = unlocked
                ? string.Format("{0} / {1}s", _crop.displayName, Mathf.RoundToInt(_crop.growSeconds))
                : string.Format("{0} / Lv {1}", _crop.displayName, _crop.requiredLevel);
            label.color = unlocked ? _labelColor : lockedColor;
        }

        if (button != null) button.interactable = unlocked;
    }
}

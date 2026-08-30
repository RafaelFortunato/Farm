using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the seed menu. Authored as a prefab (Prefabs/UI/CropButton) with its
/// sprites, font and layout already set - this script only fills in the data.
/// </summary>
public class CropButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI label;

    CropDef _crop;

    void Reset()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Bind(CropDef crop, Action<CropDef> onClick)
    {
        _crop = crop;
        if (crop == null) return;

        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(crop));
    }

    /// <summary>
    /// Shows how many seeds are left and disables the row at zero. The count is part of
    /// the label, so it is rebuilt here rather than in Bind - it changes as you plant.
    /// </summary>
    public void SetStock(int seeds)
    {
        if (label != null && _crop != null)
            label.text = string.Format("{0} / x{1} / {2}s",
                _crop.displayName, seeds, Mathf.RoundToInt(_crop.growSeconds));

        if (button != null) button.interactable = seeds > 0;
    }
}

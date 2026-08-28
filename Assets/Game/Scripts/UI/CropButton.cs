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

    void Reset()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Bind(CropDef crop, Action<CropDef> onClick)
    {
        if (crop == null) return;

        if (label != null)
            label.text = string.Format("{0}   {1}c   {2}s",
                crop.displayName.ToUpperInvariant(), crop.seedCost, Mathf.RoundToInt(crop.growSeconds));

        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(crop));
    }

    /// <summary>Greys the row out when the player cannot afford the seed.</summary>
    public void SetAffordable(bool affordable)
    {
        if (button != null) button.interactable = affordable;
    }
}

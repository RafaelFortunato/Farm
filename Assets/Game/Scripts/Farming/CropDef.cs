using UnityEngine;

/// <summary>
/// An item you can also plant. All balance lives here so tuning the 20-minute target never
/// requires a code change.
/// </summary>
[CreateAssetMenu(fileName = "Crop_", menuName = "Farm/Crop Definition")]
public class CropDef : ItemDef
{
    [Header("Growth")]
    [Tooltip("Visual for each growth stage, in order. The last one is the harvestable look.")]
    public GameObject[] stagePrefabs;

    [Tooltip("Optional per-stage uniform scale. Leave empty for 1. Lets a crop with only a " +
             "finished model (e.g. wheat) fake its stages by scaling.")]
    public float[] stageScales;

    [Tooltip("Seconds from planting to fully grown.")]
    public float growSeconds = 20f;

    [Header("Economy")]
    public int seedCost = 4;

    public int StageCount => stagePrefabs != null ? stagePrefabs.Length : 0;

    // A crop rarely needs its own pickup model - the ripe stage already is one.
    public override GameObject DisplayPrefab =>
        worldPrefab != null ? worldPrefab : StagePrefab(StageCount - 1);

    public GameObject StagePrefab(int index)
    {
        if (stagePrefabs == null || stagePrefabs.Length == 0) return null;
        return stagePrefabs[Mathf.Clamp(index, 0, stagePrefabs.Length - 1)];
    }

    public float StageScale(int index)
    {
        if (stageScales == null || stageScales.Length == 0) return 1f;
        return stageScales[Mathf.Clamp(index, 0, stageScales.Length - 1)];
    }
}

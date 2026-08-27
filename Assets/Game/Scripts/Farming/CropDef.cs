using UnityEngine;

/// <summary>
/// Data for one plantable crop. All balance lives here so tuning the 20-minute target
/// never requires a code change.
/// </summary>
[CreateAssetMenu(fileName = "Crop_", menuName = "Farm/Crop Definition")]
public class CropDef : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Crop";
    public Color menuColor = Color.white;

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
    public int sellValue = 14;

    [Header("Harvest")]
    [Tooltip("Spawned when the player harvests. Falls back to the last growth stage.")]
    public GameObject collectablePrefab;

    public int StageCount => stagePrefabs != null ? stagePrefabs.Length : 0;

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

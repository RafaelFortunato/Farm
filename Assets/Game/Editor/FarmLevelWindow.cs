using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Farm > Farm Levels: put the island into any level's state without entering play mode.
///
/// The expansion is authored as five overlapping states of one scene, which makes it nearly
/// impossible to judge from the editor - level 3 is whatever is left visible after four groups
/// have been switched on and two switched off, and checking it meant playing the game and
/// upgrading four times. This drives FarmExpansion directly instead, so the island, the walkable
/// area and the camera framing can be flipped through in a second and actually looked at.
///
/// It sets the farmhouse's own level as well, so pressing play keeps whatever is on screen
/// rather than snapping back to level 1.
/// </summary>
public class FarmLevelWindow : EditorWindow
{
    static readonly string[] Unlocks =
    {
        "Crops and the store",
        "Cooking - the stove",
        "Trucks - the road and sell counter",
        "Animals - the ranch and its land",
        "Nothing new; the farmhouse gets its final look",
    };

    [MenuItem("Farm/Farm Levels")]
    public static void Open()
    {
        var window = GetWindow<FarmLevelWindow>("Farm Levels");
        window.minSize = new Vector2(340f, 300f);
    }

    void OnGUI()
    {
        var expansion = FindFirstObjectByType<FarmExpansion>(FindObjectsInactive.Include);
        var areas = FindFirstObjectByType<FarmAreas>(FindObjectsInactive.Include);
        var farmhouse = FindFirstObjectByType<MainBuilding>(FindObjectsInactive.Include);

        if (expansion == null)
        {
            EditorGUILayout.HelpBox("No FarmExpansion in the open scene.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Show the farm at level", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Switches the island, the walkable area and the camera bounds, " +
                                   "and sets the farmhouse's level to match.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);

        int levels = Mathf.Min(expansion.stages != null ? expansion.stages.Length : 0, Unlocks.Length);
        for (int lv = 1; lv <= levels; lv++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = FarmAreas.TintFor(lv);

                bool current = farmhouse != null && farmhouse.Level == lv;
                if (GUILayout.Button((current ? "●  " : "     ") + "Level " + lv,
                                     GUILayout.Width(96f), GUILayout.Height(24f)))
                    Apply(expansion, farmhouse, lv);

                GUI.backgroundColor = previous;
                EditorGUILayout.LabelField(Unlocks[lv - 1], EditorStyles.wordWrappedLabel);
            }
        }

        EditorGUILayout.Space(10f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Frame the island", GUILayout.Height(20f))) FrameIsland();
            if (GUILayout.Button("Save scene", GUILayout.Height(20f)))
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Which land belongs to which level", EditorStyles.boldLabel);

        if (areas == null)
        {
            EditorGUILayout.HelpBox("No FarmAreas component in the scene, so the island cannot be " +
                                    "re-sorted. Add one to --- EXPANSION ---.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Move the circles on FarmAreas (they draw in the scene view), " +
                                   "then rebuild to re-sort every tile, tree and fence into the " +
                                   "level it now falls in.", EditorStyles.wordWrappedMiniLabel);

        if (GUILayout.Button("Select FarmAreas", GUILayout.Height(20f)))
        {
            Selection.activeGameObject = areas.gameObject;
            EditorGUIUtility.PingObject(areas.gameObject);
        }

        if (GUILayout.Button("Rebuild areas from the shapes", GUILayout.Height(26f)))
        {
            string report = FarmAreaBuilder.Rebuild();
            Debug.Log("Farm areas rebuilt:\n" + report);
            Apply(expansion, farmhouse, farmhouse != null ? farmhouse.Level : 1);
        }

        if (!string.IsNullOrEmpty(_lastMessage))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_lastMessage, MessageType.None);
        }
    }

    string _lastMessage;

    void Apply(FarmExpansion expansion, MainBuilding farmhouse, int level)
    {
        expansion.ApplyUpTo(level);

        // The farmhouse is the real source of the level at runtime, so it is moved too - without
        // this, pressing play would put the island straight back to whatever it had stored.
        if (farmhouse != null)
        {
            var so = new SerializedObject(farmhouse);
            so.FindProperty("_level").intValue = level;
            so.ApplyModifiedProperties();
        }

        _lastMessage = "Showing level " + level + ": " + Unlocks[Mathf.Clamp(level - 1, 0, Unlocks.Length - 1)];
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        SceneView.RepaintAll();
        Repaint();
    }

    static void FrameIsland()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) return;

        view.pivot = new Vector3(0f, 0f, -1f);
        view.rotation = Quaternion.Euler(50f, 0f, 0f);
        view.size = 30f;
        view.Repaint();
    }
}

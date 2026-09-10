using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sorts the starting island into the groups FarmExpansion switches on, one per farm level.
///
/// The island ships as one lump - two hundred tiles, forty trees, a road - and the levels cut
/// it into four pieces. Doing that by hand means dragging every one of those objects into the
/// right group and doing it again the moment a boundary moves, so it is done from the shapes on
/// FarmAreas instead: this reads them, drops each object into a "Lv1".."Lv4" child of the
/// container it already lives in, and writes the stage lists that turn those groups on.
///
/// Re-runnable on purpose. It flattens whatever a previous run left behind before sorting
/// again, so moving a circle and pressing Rebuild is the whole edit loop, and nothing has to be
/// unpicked first.
/// </summary>
public static class FarmAreaBuilder
{
    /// <summary>Containers whose children are sorted individually by where they stand.</summary>
    static readonly string[] SplitPaths =
    {
        "--- ENVIRONMENT ---/Ground",
        "--- ENVIRONMENT ---/Trees",
        "--- ENVIRONMENT ---/Decor",
        "--- ENVIRONMENT ---/Road",
        "--- ENVIRONMENT ---/Fence",
        "--- FARM ---/Plots",
    };

    /// <summary>
    /// Objects that belong to a level as a whole rather than by where they stand - the features
    /// each level is actually about. This is the table to edit when a feature moves level.
    /// </summary>
    static readonly (string path, int level)[] Features =
    {
        ("--- BUILDINGS ---/Farmhouse",         1),
        ("--- BUILDINGS ---/SeedStore",         1),   // store
        ("--- BUILDINGS ---/Stove",             2),   // cooking
        ("--- ENVIRONMENT ---/bushesportalgreen", 3),
        ("--- BUILDINGS ---/SellCounter",       3),   // trucks
        ("--- FARM ---/TruckQueue",             3),
        ("--- BUILDINGS ---/Ranch/RanchBarn",   4),
        ("--- BUILDINGS ---/Ranch/RanchSilo",   4),
        ("--- BUILDINGS ---/Ranch/AnimalPen",   4),
        ("--- BUILDINGS ---/Ranch/Animals",     4),   // animals
    };

    /// <summary>The farmhouse's own look, swapped as it is upgraded. Level 5 changes only this.</summary>
    const string ArtLv1 = "--- BUILDINGS ---/Farmhouse/Art_Lv1";
    const string ArtLv2 = "--- BUILDINGS ---/Farmhouse/Art_Lv2";
    const string ArtLv3 = "--- BUILDINGS ---/Farmhouse/Art_Lv3";

    const int Levels = 5;

    public static string Rebuild()
    {
        var areas = Object.FindFirstObjectByType<FarmAreas>(FindObjectsInactive.Include);
        if (areas == null) return "No FarmAreas component in the scene - add one to --- EXPANSION ---.";

        var expansion = Object.FindFirstObjectByType<FarmExpansion>(FindObjectsInactive.Include);
        if (expansion == null) return "No FarmExpansion component in the scene.";

        var log = new System.Text.StringBuilder();

        AdoptStrayTiles(log);

        // group[level] = the objects stage (level-1) must switch on
        var perLevel = new List<GameObject>[Levels + 1];
        for (int i = 0; i <= Levels; i++) perLevel[i] = new List<GameObject>();

        foreach (var path in SplitPaths)
        {
            var container = Find(path);
            if (container == null) { log.AppendLine("missing container: " + path); continue; }

            Flatten(container);

            var byLevel = new Dictionary<int, List<Transform>>();
            foreach (Transform child in container.transform)
            {
                int lv = Mathf.Clamp(areas.LevelAt(CentreOf(child.gameObject)), 1, 4);
                if (!byLevel.TryGetValue(lv, out var list)) byLevel[lv] = list = new List<Transform>();
                list.Add(child);
            }

            foreach (var kv in byLevel.OrderBy(k => k.Key))
            {
                var group = new GameObject("Lv" + kv.Key);
                group.transform.SetParent(container.transform, false);

                foreach (var child in kv.Value)
                {
                    // Visibility is the group's job now, so anything switched off by an older
                    // scheme is switched back on as it moves in.
                    child.gameObject.SetActive(true);
                    child.SetParent(group.transform, true);
                }

                perLevel[kv.Key].Add(group);
                log.AppendLine(container.name + "/Lv" + kv.Key + ": " + kv.Value.Count + " objects");
            }
        }

        foreach (var (path, level) in Features)
        {
            var go = Find(path);
            if (go == null) { log.AppendLine("missing feature: " + path); continue; }
            perLevel[level].Add(go);
        }

        WriteStages(expansion, areas, perLevel, log);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return log.ToString();
    }

    /// <summary>
    /// Ground tiles that were left at the scene root. They are island like any other, so they
    /// are taken into Ground before the sort rather than sorted where they lie.
    /// </summary>
    static void AdoptStrayTiles(System.Text.StringBuilder log)
    {
        var ground = Find("--- ENVIRONMENT ---/Ground");
        if (ground == null) return;

        int taken = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!root.name.StartsWith("Tile_")) continue;
            root.transform.SetParent(ground.transform, true);
            taken++;
        }
        if (taken > 0) log.AppendLine("adopted " + taken + " stray tiles into Ground");
    }

    /// <summary>Undo a previous sort, so a rebuild starts from a flat container every time.</summary>
    static void Flatten(GameObject container)
    {
        var groups = new List<Transform>();
        foreach (Transform child in container.transform)
            if (child.name.Length == 3 && child.name.StartsWith("Lv") && child.childCount >= 0)
                groups.Add(child);

        foreach (var group in groups)
        {
            // The group is switched off at levels below its own; its contents must not inherit
            // that on the way out, or a rebuild would quietly retire half the island.
            group.gameObject.SetActive(true);

            for (int i = group.childCount - 1; i >= 0; i--)
                group.GetChild(i).SetParent(container.transform, true);

            Object.DestroyImmediate(group.gameObject);
        }
    }

    static void WriteStages(FarmExpansion expansion, FarmAreas areas, List<GameObject>[] perLevel,
                            System.Text.StringBuilder log)
    {
        if (expansion.stages == null || expansion.stages.Length < Levels)
            expansion.stages = new FarmExpansion.Stage[Levels];

        for (int i = 0; i < Levels; i++)
            expansion.stages[i] ??= new FarmExpansion.Stage();

        var art1 = Find(ArtLv1);
        var art2 = Find(ArtLv2);
        var art3 = Find(ArtLv3);

        for (int lv = 1; lv <= Levels; lv++)
        {
            var stage = expansion.stages[lv - 1];
            var enable = new List<GameObject>(perLevel[lv]);
            var disable = new List<GameObject>();

            // The farmhouse grows into its better-looking self as the farm does. Level 5 opens
            // no new ground, so this is the whole of what it changes.
            if (lv == 3 && art2 != null) { enable.Add(art2); if (art1 != null) disable.Add(art1); }
            if (lv == 5 && art3 != null) { enable.Add(art3); if (art2 != null) disable.Add(art2); }

            stage.enable = enable.ToArray();
            stage.disable = disable.ToArray();

            SetBounds(stage, lv);
            log.AppendLine("Lv" + lv + ": enable " + enable.Count + ", disable " + disable.Count +
                           ", camera " + stage.cameraBoundsMin + ".." + stage.cameraBoundsMax);
        }

        EditorUtility.SetDirty(expansion);
    }

    /// <summary>
    /// How far the player may walk, and how far the camera may look, at each level.
    ///
    /// Not measured off the tiles: the island reaches well past where the player may go - the
    /// road is fenced off, and the shoreline is scenery. These figures describe the reachable
    /// island per level, with the camera inset inside it so it never frames the void past the
    /// edge. Only the camera reads them now - the player is stopped by the ground tiles
    /// themselves, see FarmColliders.
    /// </summary>
    static void SetBounds(FarmExpansion.Stage stage, int level)
    {
        Vector2 min, max;
        switch (level)
        {
            case 1: min = new Vector2(-7f, -5f);     max = new Vector2(11f, 11f); break;
            case 2: min = new Vector2(-11f, -9.5f);  max = new Vector2(11f, 11f); break;
            case 3: min = new Vector2(-11f, -14.3f); max = new Vector2(11f, 11f); break;
            default: min = new Vector2(-29f, -14.3f); max = new Vector2(11f, 11f); break;   // 4 and 5
        }

        // Same inset the hand-tuned level-3 framing used, so the camera keeps its distance from
        // the edge as the island grows.
        stage.cameraBoundsMin = new Vector2(min.x + 4f, min.y - 0.7f);
        stage.cameraBoundsMax = new Vector2(max.x - 4f, max.y - 4f);
    }

    static Vector3 CentreOf(GameObject go)
    {
        Bounds b = default;
        bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return first ? go.transform.position : b.center;
    }

    /// <summary>
    /// Look up by full path, including through switched-off parents - most of what this touches
    /// is switched off at the level the scene happens to be showing. GameObject.Find cannot.
    /// </summary>
    public static GameObject Find(string path)
    {
        var parts = path.Split('/');

        Transform current = null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == parts[0]) { current = root.transform; break; }

        for (int i = 1; i < parts.Length && current != null; i++)
        {
            Transform next = null;
            foreach (Transform child in current)
                if (child.name == parts[i]) { next = child; break; }
            current = next;
        }
        return current != null ? current.gameObject : null;
    }
}

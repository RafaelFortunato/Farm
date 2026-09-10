using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives the farm its collision, on the PREFABS rather than on the scene objects.
///
/// Two layers carry it:
///   Ground    - tiles and road. Nothing collides with these; the player raycasts them to ask
///               "is there floor where I am about to step?", which is what keeps the cat on the
///               island now that no hand-tuned rectangle does. The walkable area is therefore
///               whatever shape the island actually is, and grows with it.
///   Obstacle  - everything solid: buildings, trees, fences, decor, plots.
///
/// Where the collider lives matters as much as that it exists. An earlier pass added them to
/// the scene objects, which meant 500-odd instance overrides, no reuse, and the same work again
/// for anything placed later. Instead, each distinct source asset gets ONE project-owned prefab
/// carrying the collider, and the scene is re-pointed at it:
///
///   - a source already under Assets/Game is edited in place
///   - a source from Assets/AssetStore gets a VARIANT under Assets/Game/Prefabs/Env, so the
///     package stays untouched and still updates cleanly
///
/// Re-runnable: Farm > Rebuild Colliders.
/// </summary>
public static class FarmColliders
{
    const string VariantFolder = "Assets/Game/Prefabs/Env";

    /// <summary>A group of scene objects and how their collider should be shaped.</summary>
    struct Rule
    {
        public string path;
        public bool ground;
        /// <summary>
        /// How much of the footprint blocks. Trees use less than 1: a box around the canopy
        /// would wall off a whole tile, when the only thing you would really walk into is trunk.
        /// </summary>
        public float footprint;
    }

    static readonly Rule[] Rules =
    {
        new Rule { path = "--- ENVIRONMENT ---/Ground",   ground = true,  footprint = 1f },
        new Rule { path = "--- ENVIRONMENT ---/Road",     ground = true,  footprint = 1f },
        new Rule { path = "--- ENVIRONMENT ---/Fence",    ground = false, footprint = 1f },
        new Rule { path = "--- ENVIRONMENT ---/Decor",    ground = false, footprint = 1f },
        new Rule { path = "--- ENVIRONMENT ---/Trees",    ground = false, footprint = 0.35f },
        new Rule { path = "--- BUILDINGS ---/Farmhouse",  ground = false, footprint = 1f },
        new Rule { path = "--- BUILDINGS ---/SeedStore",  ground = false, footprint = 1f },
        new Rule { path = "--- BUILDINGS ---/Stove",      ground = false, footprint = 1f },
        new Rule { path = "--- BUILDINGS ---/SellCounter",ground = false, footprint = 1f },
        new Rule { path = "--- BUILDINGS ---/Ranch",      ground = false, footprint = 1f },

        // Soil plots are FLOOR, not furniture. The player walks over them to reach the crops,
        // and planting is picked by proximity rather than by any collider, so treating them as
        // obstacles only walled off the middle of the farm. They go on Ground rather than losing
        // their collider outright because there is no tile underneath a plot - the soil is the
        // only floor there, so it has to be what answers the ledge probe.
        new Rule { path = "--- FARM ---/Plots",           ground = true,  footprint = 1f },
    };

    [MenuItem("Farm/Rebuild Colliders")]
    public static void Rebuild()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int obstacle = LayerMask.NameToLayer("Obstacle");
        if (ground < 0 || obstacle < 0)
        {
            Debug.LogError("FarmColliders: add the 'Ground' and 'Obstacle' layers first.");
            return;
        }
        EnsureFolder();

        var log = new StringBuilder("Farm colliders rebuilt");

        // An earlier pass put colliders on the scene objects themselves. Those overrides would
        // otherwise stack on top of the ones the prefabs now carry, so they go first. Objects
        // that get re-pointed below are rebuilt from the prefab anyway; this matters for the
        // ones whose prefab we already owned and edit in place.
        int reverted = CleanInstanceOverrides();
        if (reverted > 0)
            log.Append("\n  reverted ").Append(reverted).Append(" old instance override(s)");

        var prepared = new Dictionary<string, GameObject>();   // source asset path -> project prefab
        int swapped = 0, inPlace = 0, orphans = 0, skipped = 0;

        foreach (var rule in Rules)
        {
            int layer = rule.ground ? ground : obstacle;

            foreach (var leaf in LeavesOf(rule.path))
            {
                // The NEAREST prefab, deliberately, not the original source. Walking to the
                // original lands on the .fbx, and a model file's materials are import stubs -
                // same name as the real ones, no texture, near-white. The authored .prefab beside
                // it is what assigns the actual palette material, so that is what we branch from.
                var source = PrefabUtility.GetCorrespondingObjectFromSource(leaf.gameObject);
                if (source == null)
                {
                    // Nothing to put a collider ON - this object exists only in the scene. Give
                    // it one directly and say so; inventing a prefab for a one-off buys nothing.
                    if (FitCollider(leaf.gameObject, layer, rule.footprint)) orphans++;
                    continue;
                }

                string sourcePath = AssetDatabase.GetAssetPath(source);
                if (!prepared.TryGetValue(sourcePath, out var prefab))
                {
                    prefab = Prepare(sourcePath, source, layer, rule.footprint, log);
                    prepared[sourcePath] = prefab;
                    if (sourcePath.StartsWith("Assets/Game")) inPlace++;
                }
                if (prefab == null) continue;

                // already pointing at the project prefab? then it is done
                var current = PrefabUtility.GetCorrespondingObjectFromSource(leaf.gameObject);
                if (current != null && AssetDatabase.GetAssetPath(current) == AssetDatabase.GetAssetPath(prefab))
                    continue;

                if (!CanSwap(leaf.gameObject, out string why))
                {
                    // Not swappable, so it keeps whatever it has - and it gets NO collider of
                    // ours either. Everything in this branch is a character: the livestock and
                    // the shopkeeper. Fitting boxes to them gave a chicken sixteen colliders and
                    // made the animals solid, which is not what a pen full of them should feel
                    // like. Scenery is what this tool is for.
                    skipped++;
                    log.Append("\n  left alone (").Append(why).Append("): ").Append(leaf.name);
                    continue;
                }

                if (Repoint(leaf, prefab)) swapped++;
            }
        }

        log.Append("\n  scene objects re-pointed at a project prefab: ").Append(swapped)
           .Append("\n  prefabs we already owned, edited in place:    ").Append(inPlace)
           .Append("\n  scene-only objects given a collider directly: ").Append(orphans)
           .Append("\n  characters left uncollided:                   ").Append(skipped);

        int stubs = ReportStubMaterials(log);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        if (stubs > 0) Debug.LogError(log.ToString());
        else Debug.Log(log.ToString());
    }

    /// <summary>
    /// Shout if any variant ended up on a material that lives inside a model file.
    ///
    /// Importing an .fbx generates placeholder materials named after the real ones but carrying
    /// no texture, and branching a variant off the model instead of the authored prefab silently
    /// picks those up - the meshes still draw, just untextured, so nothing errors and the only
    /// symptom is a scene gone white. Worth an explicit check precisely because it is invisible.
    /// </summary>
    static int ReportStubMaterials(StringBuilder log)
    {
        int stubs = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { VariantFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    var mp = AssetDatabase.GetAssetPath(m);
                    if (!mp.EndsWith(".fbx") && !mp.EndsWith(".obj") && !mp.EndsWith(".blend")) continue;
                    stubs++;
                    log.Append("\n  UNTEXTURED: ").Append(path).Append(" / ").Append(r.name)
                       .Append(" is using the placeholder material inside ").Append(mp);
                }
            }
        }
        if (stubs > 0)
            log.Append("\n  -> branch these off the authored .prefab, not the model file.");
        return stubs;
    }

    /// <summary>
    /// Undo collider work that was previously applied straight to scene objects, so a rerun
    /// does not leave a prefab collider and an instance collider fighting over the same box.
    /// </summary>
    static int CleanInstanceOverrides()
    {
        int n = 0;
        foreach (var rule in Rules)
            foreach (var leaf in LeavesOf(rule.path))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(leaf.gameObject)) continue;

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(leaf.gameObject);
                if (root == null) continue;

                foreach (var added in PrefabUtility.GetAddedComponents(root))
                {
                    if (!(added.instanceComponent is BoxCollider)) continue;
                    added.Revert();
                    n++;
                }
            }
        return n;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Game", "Prefabs");
        if (!AssetDatabase.IsValidFolder(VariantFolder))
            AssetDatabase.CreateFolder("Assets/Game/Prefabs", "Env");
    }

    /// <summary>
    /// Make sure a project-owned prefab exists for this source, carrying the collider.
    /// Ours is edited directly; a package asset gets a variant so the package is left alone.
    /// </summary>
    static GameObject Prepare(string sourcePath, Object source, int layer, float footprint, StringBuilder log)
    {
        if (sourcePath.StartsWith("Assets/Game") && sourcePath.EndsWith(".prefab"))
        {
            var contents = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                FitCollider(contents, layer, footprint);
                PrefabUtility.SaveAsPrefabAsset(contents, sourcePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            log.Append("\n  [ours]    ").Append(sourcePath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        }

        string variantPath = VariantFolder + "/" + SafeName(sourcePath) + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        if (existing != null) return existing;

        // Instantiating the package asset and saving that is what makes this a VARIANT rather
        // than a copy: it keeps following the package for meshes and materials, and only the
        // collider is ours.
        var temp = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (temp == null)
        {
            log.Append("\n  FAILED to instantiate ").Append(sourcePath);
            return null;
        }
        FitCollider(temp, layer, footprint);
        var saved = PrefabUtility.SaveAsPrefabAsset(temp, variantPath);
        Object.DestroyImmediate(temp);

        log.Append("\n  [variant] ").Append(variantPath).Append("   of ").Append(sourcePath);
        return saved;
    }

    /// <summary>
    /// Is this object safe to rebuild from a prefab, or would the swap destroy something?
    ///
    /// Repoint keeps only the transform and the name. Anything else the scene author put on the
    /// instance - a different material, an animator controller, an extra component - is gone the
    /// moment the old object is destroyed, and nothing reports it. That is not hypothetical: the
    /// shopkeeper is a rigged, animated cat placed as one of these, and swapping him for a plain
    /// model left a flattened, headless mesh standing at the counter.
    ///
    /// So the rule is inverted from what it was: swap only what is provably plain scenery, and
    /// leave everything else exactly as the author left it.
    /// </summary>
    static bool CanSwap(GameObject go, out string why)
    {
        why = null;

        // characters, not scenery - they carry rigs, avatars and controllers
        if (go.GetComponentInChildren<Animator>(true) != null) { why = "animated"; return false; }
        if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) { why = "skinned"; return false; }

        if (!PrefabUtility.IsPartOfPrefabInstance(go)) return true;
        var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if (root == null) return true;

        if (PrefabUtility.GetAddedComponents(root).Count > 0)   { why = "has added components"; return false; }
        if (PrefabUtility.GetRemovedComponents(root).Count > 0) { why = "has removed components"; return false; }
        if (PrefabUtility.GetAddedGameObjects(root).Count > 0)  { why = "has added children"; return false; }

        // Property overrides are fine when they are only the placement we copy over anyway.
        foreach (var o in PrefabUtility.GetObjectOverrides(root))
        {
            var target = o.instanceObject;
            if (target is Transform || target is GameObject) continue;
            why = "overrides " + (target != null ? target.GetType().Name : "something");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Swap a scene object for an instance of the project prefab, keeping where it sits.
    ///
    /// Anything pointing AT the old object has to come along. The level stages hold direct
    /// GameObject references to the farmhouse art and the ranch buildings, and destroying a leaf
    /// out from under them leaves a null in the array that nothing complains about until a player
    /// reaches that level and the building fails to appear.
    /// </summary>
    static bool Repoint(Transform old, GameObject prefab)
    {
        var parent = old.parent;
        int sibling = old.GetSiblingIndex();
        string name = old.name;
        bool active = old.gameObject.activeSelf;
        Vector3 lp = old.localPosition;
        Quaternion lr = old.localRotation;
        Vector3 ls = old.localScale;

        var fresh = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (fresh == null) return false;

        fresh.name = name;
        fresh.transform.localPosition = lp;
        fresh.transform.localRotation = lr;
        fresh.transform.localScale = ls;
        fresh.transform.SetSiblingIndex(sibling);
        fresh.SetActive(active);

        CarryReferences(old.gameObject, fresh);
        Object.DestroyImmediate(old.gameObject);
        return true;
    }

    /// <summary>
    /// Hand every reference to the old object - and to anything inside it - over to the
    /// replacement, before the old one is destroyed.
    ///
    /// This started out only fixing the level stages, which was too narrow and cost real damage:
    /// the stove's ready badge and the shop's focus anchor were both silently unplugged, and a
    /// null reference in the inspector looks exactly like one that was never filled in. Anything
    /// in the scene may point at a piece of scenery, so everything is searched.
    ///
    /// Matching is by path within the object, which is exact here because the replacement is
    /// built from the same prefab and CanSwap has already refused anything with added or removed
    /// children.
    /// </summary>
    static void CarryReferences(GameObject old, GameObject fresh)
    {
        var moved = new Dictionary<Object, Object>();
        MapPair(old.transform, fresh.transform, moved);
        if (moved.Count == 0) return;

        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;

            var so = new SerializedObject(mb);
            bool touched = false;

            var p = so.GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                var target = p.objectReferenceValue;
                if (target == null) continue;
                if (!moved.TryGetValue(target, out var replacement)) continue;

                p.objectReferenceValue = replacement;
                touched = true;
            }

            if (touched) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// Pair up an object with its replacement - the GameObject, its Transform and every
    /// component on it - then walk their children together.
    /// </summary>
    static void MapPair(Transform from, Transform to, Dictionary<Object, Object> moved)
    {
        moved[from.gameObject] = to.gameObject;
        moved[from] = to;

        var fromComponents = from.GetComponents<Component>();
        foreach (var c in fromComponents)
        {
            if (c == null) continue;
            var match = to.GetComponent(c.GetType());
            if (match != null) moved[c] = match;
        }

        for (int i = 0; i < from.childCount && i < to.childCount; i++)
            MapPair(from.GetChild(i), to.GetChild(i), moved);
    }

    /// <summary>
    /// Give this object collision, shaped to what it actually draws.
    ///
    /// Usually that is one box on the root, measured off the renderers rather than the transform:
    /// these are kitbashed prefabs whose pivots sit anywhere, so the transform says nothing about
    /// where the geometry is. Corners are pulled back into local space so the box survives
    /// rotation and scale.
    ///
    /// HOLLOW things are the exception, and they matter more than they sound. An animal pen is
    /// seventeen fence panels arranged in a ring; one box around them is thirteen times the
    /// volume of the fence itself, and all of that surplus is the pen INTERIOR - which is exactly
    /// where the player needs to walk to reach the plots inside. So when the enclosing box is
    /// much larger than the pieces it contains, each piece gets its own box instead.
    /// </summary>
    static bool FitCollider(GameObject go, int layer, float footprint)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);

        // start clean - a rerun must not stack a new box on top of the last one
        foreach (var existing in go.GetComponentsInChildren<BoxCollider>(true))
            Object.DestroyImmediate(existing);

        if (!Measure(go.transform, renderers, out Bounds whole)) return false;

        if (IsHollow(whole, renderers))
        {
            bool made = false;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                if (!Measure(r.transform, new[] { r }, out Bounds part)) continue;
                AddBox(r.gameObject, part, footprint);
                made = true;
            }
            if (made) { SetLayerDeep(go, layer); return true; }
        }

        AddBox(go, whole, footprint);
        SetLayerDeep(go, layer);
        return true;
    }

    /// <summary>
    /// Does this thing enclose far more space than it fills? A ring of fence does; a barn does
    /// not. Compared by volume because that is what decides whether the middle is walkable.
    /// </summary>
    static bool IsHollow(Bounds whole, Renderer[] renderers)
    {
        int drawn = 0;
        float parts = 0f;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            drawn++;
            var s = r.bounds.size;
            parts += s.x * s.y * s.z;
        }
        if (drawn < 2 || parts <= 0.0001f) return false;

        float enclosing = whole.size.x * whole.size.y * whole.size.z;
        return enclosing > parts * 2.5f;
    }

    /// <summary>World-space renderer bounds, expressed in the given transform's local space.</summary>
    static bool Measure(Transform t, Renderer[] renderers, out Bounds local)
    {
        local = default;
        bool any = false;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;      // sparkles are not geometry
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                         (i & 2) == 0 ? b.min.y : b.max.y,
                                         (i & 4) == 0 ? b.min.z : b.max.z);
                var p = t.InverseTransformPoint(corner);
                if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                else local.Encapsulate(p);
            }
        }
        return any;
    }

    static void AddBox(GameObject go, Bounds local, float footprint)
    {
        var box = go.AddComponent<BoxCollider>();
        box.center = local.center;
        box.size = new Vector3(Mathf.Max(local.size.x * footprint, 0.01f),
                               Mathf.Max(local.size.y, 0.01f),
                               Mathf.Max(local.size.z * footprint, 0.01f));
    }

    /// <summary>
    /// The individual objects under a container, seeing through the Lv1..Lv4 grouping and
    /// through plain grouping objects.
    ///
    /// "Plain" means an empty transform that only holds other things - a level group, or a rig
    /// like AnimalPen that is really seventeen separate fence prefabs. Those have to be opened
    /// up, or the pen gets one collider of its own instead of each panel inheriting one from the
    /// fence prefab, which is both the wrong shape and an instance override.
    ///
    /// Anything that draws or comes from a prefab is a leaf and is handled as one piece.
    /// </summary>
    static List<Transform> LeavesOf(string path)
    {
        var found = new List<Transform>();
        var container = FarmAreaBuilder.Find(path);
        if (container == null)
        {
            Debug.LogWarning("FarmColliders: missing " + path);
            return found;
        }

        Collect(container.transform, found, 0);
        return found;
    }

    static void Collect(Transform parent, List<Transform> found, int depth)
    {
        foreach (Transform child in parent)
        {
            if (depth < 3 && IsPlainGrouping(child)) Collect(child, found, depth + 1);
            else found.Add(child);
        }
    }

    /// <summary>An empty holder: no geometry of its own, not a prefab, and it has children.</summary>
    static bool IsPlainGrouping(Transform t) =>
        t.childCount > 0
        && t.GetComponent<Renderer>() == null
        && !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject);

    static string SafeName(string assetPath)
    {
        var n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        foreach (var bad in System.IO.Path.GetInvalidFileNameChars()) n = n.Replace(bad, '_');
        return n.Replace(' ', '_');
    }

    static void SetLayerDeep(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerDeep(c.gameObject, layer);
    }
}

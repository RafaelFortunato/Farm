using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Farm.Farming;

/// <summary>
/// Renders a UI icon for every ItemDef from its 3D model.
///
/// The project ships no 2D food art - Layer Lab is a fantasy RPG set - so icons are generated from
/// the models we already have. Run "Farm/Render Item Icons" again whenever an item is added or its
/// model changes; it overwrites in place and re-assigns ItemDef.icon.
///
/// The look lives in Assets/Game/Scenes/IconStudio.unity, which is deliberately NOT in Build
/// Settings. Open it and move the camera or lights to change how every icon looks, then re-run.
/// This code only decides framing - orthographic size and centring - so items keep equal weight.
/// </summary>
public static class IconStudio
{
    const string ScenePath = "Assets/Game/Scenes/IconStudio.unity";
    const string OutputFolder = "Assets/Game/Sprites/Icons";
    const int Size = 256;
    const float FramePadding = 1.16f;   // above 1 leaves air around the silhouette
    const float TrimMargin = 0.08f;

    [MenuItem("Farm/Render Item Icons")]
    public static void RenderAll()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("IconStudio: " + ScenePath + " is missing.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Game/Sprites", "Icons");

        // Leave whatever the user had open alone: load additively, and only close it again if we
        // were the ones who opened it.
        bool alreadyOpen = SceneManager.GetSceneByPath(ScenePath).isLoaded;
        Scene studio = alreadyOpen
            ? SceneManager.GetSceneByPath(ScenePath)
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        Scene previousActive = SceneManager.GetActiveScene();
        var suppressed = new List<Light>();
        int done = 0, skipped = 0;

        try
        {
            // The studio's own ambient and lighting must be what shows up, so make it the active
            // scene and switch off every light belonging to any other open scene - the farm's sun
            // would otherwise light the icons.
            EditorSceneManager.SetActiveScene(studio);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (light.gameObject.scene == studio || !light.enabled) continue;
                light.enabled = false;
                suppressed.Add(light);
            }

            var cam = FindInScene<Camera>(studio, "IconCamera");
            var anchor = FindRoot(studio, "Subject");
            if (cam == null || anchor == null)
            {
                Debug.LogError("IconStudio: the scene needs an IconCamera and a Subject object.");
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:ItemDef"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;

                if (item.DisplayPrefab == null)
                {
                    Debug.LogWarning("IconStudio: " + item.displayName + " has no model, skipped.");
                    skipped++;
                    continue;
                }

                if (Render(item, cam, anchor)) done++; else skipped++;
            }

            AssetDatabase.SaveAssets();
        }
        finally
        {
            foreach (var l in suppressed) if (l != null) l.enabled = true;
            if (previousActive.IsValid()) EditorSceneManager.SetActiveScene(previousActive);
            if (!alreadyOpen && studio.isLoaded) EditorSceneManager.CloseScene(studio, true);
        }

        Debug.Log("IconStudio: rendered " + done + " icon(s), skipped " + skipped + ". Output in " + OutputFolder);
    }

    static bool Render(ItemDef item, Camera cam, Transform anchor)
    {
        GameObject subject = null;
        RenderTexture rt = null;
        Texture2D shot = null;

        try
        {
            subject = Object.Instantiate(item.DisplayPrefab, anchor);
            subject.hideFlags = HideFlags.HideAndDontSave;
            subject.transform.localPosition = Vector3.zero;
            subject.transform.localRotation = Quaternion.identity;
            foreach (var c in subject.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            var b = SubjectBounds(subject);
            if (b.size == Vector3.zero) return false;

            // sit the model's centre exactly on the anchor the scene's camera is aimed at
            subject.transform.position += anchor.position - b.center;
            b = SubjectBounds(subject);

            // bounding-sphere radius, so the fit holds whatever angle the scene's camera uses
            float radius = Mathf.Max(b.extents.magnitude, 0.0001f);
            float prevSize = cam.orthographicSize;
            cam.orthographicSize = radius * FramePadding / Mathf.Max(item.iconScale, 0.01f);

            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            cam.orthographicSize = prevSize;   // never leave the saved scene dirty

            RenderTexture.active = rt;
            shot = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            var cropped = TrimToSilhouette(shot);
            if (cropped == null)
            {
                Debug.LogError("IconStudio: " + item.displayName + " rendered blank.");
                return false;
            }

            string path = OutputFolder + "/icon_" + item.displayName.ToLowerInvariant().Replace(' ', '_') + ".png";
            File.WriteAllBytes(path, cropped.EncodeToPNG());
            Object.DestroyImmediate(cropped);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();

            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(item);
            return true;
        }
        finally
        {
            RenderTexture.active = null;
            if (shot != null) Object.DestroyImmediate(shot);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (subject != null) Object.DestroyImmediate(subject);
        }
    }

    /// <summary>Square crop around the opaque pixels, so every icon sits centred and evenly margined.</summary>
    static Texture2D TrimToSilhouette(Texture2D src)
    {
        var px = src.GetPixels32();
        int w = src.width, h = src.height;
        int minX = w, maxX = -1, minY = h, maxY = -1;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            if (px[y * w + x].a > 16)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

        if (maxX < 0) return null;

        float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
        float side = Mathf.Max(maxX - minX, maxY - minY) * (1f + TrimMargin * 2f);
        int half = Mathf.CeilToInt(side * 0.5f);

        int x0 = Mathf.Clamp(Mathf.RoundToInt(cx) - half, 0, w - 1);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(cy) - half, 0, h - 1);
        int sideOut = Mathf.Min(half * 2, Mathf.Min(w - x0, h - y0));

        var outTex = new Texture2D(sideOut, sideOut, TextureFormat.RGBA32, false);
        outTex.SetPixels(src.GetPixels(x0, y0, sideOut, sideOut));
        outTex.Apply();
        return outTex;
    }

    static T FindInScene<T>(Scene scene, string name) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name)
            {
                var c = root.GetComponent<T>();
                if (c != null) return c;
            }
        return null;
    }

    static Transform FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root.transform;
        return null;
    }

    static Bounds SubjectBounds(GameObject go)
    {
        Bounds b = default; bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}

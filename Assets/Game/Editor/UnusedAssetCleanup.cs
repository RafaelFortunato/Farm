using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Farm.EditorTools
{
    /// <summary>
    /// Finds and deletes assets nothing in the shipped game reaches.
    ///
    /// "Unused" is defined as: not reachable by walking Unity's dependency graph out from the
    /// things a build actually starts from. That graph only follows SERIALIZED references, which
    /// is why the protection rules below exist - there are several ways an asset is genuinely
    /// needed while appearing to be referenced by nothing at all. Deleting on the raw graph alone
    /// will break the project.
    ///
    /// Run "Report" first. It changes nothing and writes the full list to UnusedAssetReport.txt
    /// beside the Assets folder for review; "Delete" then removes exactly what the report listed.
    ///
    /// Ported from the Tower Defense project. The Farm keeps TextMesh Pro and its settings in
    /// different places, owns editor tools that load assets by path, and has package content
    /// (TextMesh Pro, a third-party UI kit) that ships scripts and shader includes of its own.
    /// Packages in the manifest are not this tool's business - it only ever deletes under Assets/.
    /// </summary>
    public static class UnusedAssetCleanup
    {
        /// <summary>Where the report lands, relative to the project root. Not an asset.</summary>
        const string ReportPath = "UnusedAssetReport.txt";

        /// <summary>
        /// Assets an editor tool opens by literal path. Nothing serialized points at them, so the
        /// graph cannot see them, but deleting one breaks the tool that needs it. Everything they
        /// depend on is kept with them.
        /// </summary>
        static readonly string[] EditorToolRoots =
        {
            "Assets/Game/Scenes/IconStudio.unity",   // IconStudio renders item icons in this scene
        };

        /// <summary>
        /// Folders kept whole. TextMesh Pro resolves fonts, materials and shader variants by name
        /// at runtime, and the failure mode - every label in the game turning into blank boxes - is
        /// severe. Settings holds build profiles, the input actions asset and the render pipeline
        /// assets, several of which are referenced only from ProjectSettings or not at all.
        /// </summary>
        static readonly string[] ProtectedFolders =
        {
            "Assets/AssetStore/TextMesh Pro/",
            "Assets/Game/Settings/",
        };

        /// <summary>Our own code. Kept unconditionally - see <see cref="BuildDeleteList"/>.</summary>
        const string GameScriptsRoot = "Assets/Game/";

        static readonly string[] ShaderIncludeExtensions = { ".cginc", ".hlsl", ".glslinc" };
        static readonly string[] ShaderSourceExtensions = { ".shader", ".cginc", ".hlsl", ".glslinc", ".shadergraph", ".shadersubgraph", ".compute" };

        /// <summary>Scans without changing anything and writes the report.</summary>
        [MenuItem("Tools/Cleanup/1. Report Unused Assets (safe, no deletion)")]
        public static void Report()
        {
            string summary = WriteReport();
            Debug.Log(summary + "\nFull list written to " + Path.GetFullPath(ReportPath));
            EditorUtility.DisplayDialog("Unused Asset Report",
                summary + "\n\nFull list written to:\n" + Path.GetFullPath(ReportPath), "OK");
        }

        /// <summary>Scans, confirms, then deletes everything the scan found and any emptied folders.</summary>
        [MenuItem("Tools/Cleanup/2. Delete Unused Assets")]
        public static void DeleteUnused()
        {
            List<string> doomed = BuildDeleteList(out string summary);

            if (doomed.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing To Do", "No unused assets found.", "OK");
                return;
            }

            // Deliberately a confirm step: this is irreversible without version control, and the
            // count is large enough that a misconfigured root would do real damage.
            if (!EditorUtility.DisplayDialog("Delete Unused Assets?",
                    summary + "\n\nThis cannot be undone from inside Unity.\n" +
                    "Make sure your work is committed first.",
                    "Delete", "Cancel"))
            {
                return;
            }

            var failed = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                AssetDatabase.DeleteAssets(doomed.ToArray(), failed);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            DeleteEmptyFolders();

            string done = "Deleted " + (doomed.Count - failed.Count) + " assets. Failed: " + failed.Count;
            Debug.Log(done + (failed.Count > 0 ? "\n" + string.Join("\n", failed) : ""));
            EditorUtility.DisplayDialog("Cleanup Complete", done, "OK");
        }

        /// <summary>
        /// Runs the scan and writes the report, with no dialogs. Split out from the menu item so
        /// the scan can be driven from automation, where a modal dialog would block forever.
        /// </summary>
        /// <returns>The same summary the report file starts with.</returns>
        public static string WriteReport()
        {
            List<string> doomed = BuildDeleteList(out string summary);
            File.WriteAllLines(ReportPath, new[] { summary, "" }.Concat(doomed));
            return summary;
        }

        /// <summary>
        /// Produces the list of assets safe to delete, plus a human-readable summary of what was
        /// kept and why.
        /// </summary>
        public static List<string> BuildDeleteList(out string summary)
        {
            string[] allAssets = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p))
                .ToArray();

            HashSet<string> reachable = FindReachable(allAssets);

            var candidates = new List<string>();
            int keptGameScripts = 0, keptFolders = 0;

            foreach (string path in allAssets)
            {
                if (reachable.Contains(path)) continue;

                // Our scripts are never swept. The dependency graph only sees a .cs file when
                // something serialized points at it, so an abstract base, a static helper, an
                // interface or an editor tool all look unreferenced while the code that needs them
                // will not compile without them.
                if (path.EndsWith(".cs") && path.StartsWith(GameScriptsRoot))
                {
                    keptGameScripts++;
                    continue;
                }

                if (ProtectedFolders.Any(path.StartsWith))
                {
                    keptFolders++;
                    continue;
                }

                candidates.Add(path);
            }

            // Third-party scripts and shader includes are referenced by name from other source
            // files rather than by GUID, so neither shows up in the graph. Unlike our own scripts,
            // though, a package's leftovers are exactly what this tool is for - so each is kept
            // only while some surviving source file still names it.
            int keptVendorScripts = RescueNamedByText(candidates, allAssets,
                p => p.EndsWith(".cs"),
                p => p.EndsWith(".cs"),
                p => @"\b" + Regex.Escape(Path.GetFileNameWithoutExtension(p)) + @"\b");

            int keptIncludes = RescueNamedByText(candidates, allAssets,
                p => ShaderIncludeExtensions.Any(p.EndsWith),
                p => ShaderSourceExtensions.Any(p.EndsWith),
                p => Regex.Escape(Path.GetFileName(p)));

            double mb = candidates.Sum(p =>
            {
                var f = new FileInfo(p);
                return f.Exists ? f.Length : 0L;
            }) / 1048576.0;

            summary =
                "Total assets:  " + allAssets.Length + "\n" +
                "Reachable:     " + reachable.Count(p => p.StartsWith("Assets/")) + "\n" +
                "Protected:     " + (keptGameScripts + keptFolders + keptVendorScripts + keptIncludes) +
                "  (game scripts " + keptGameScripts + ", protected folders " + keptFolders +
                ", named third-party scripts " + keptVendorScripts + ", shader includes " + keptIncludes + ")\n" +
                "TO DELETE:     " + candidates.Count + "  (" + System.Math.Round(mb, 1) + " MB)";

            candidates.Sort(System.StringComparer.Ordinal);
            return candidates;
        }

        /// <summary>
        /// Pulls text-referenced files back out of the delete list. A file is kept when any
        /// surviving source file mentions it, and survivors are re-checked until nothing changes,
        /// so a chain of includes is kept whole while a group that only references itself goes.
        /// </summary>
        /// <param name="candidates">The delete list, edited in place.</param>
        /// <param name="allAssets">Every asset path, to find the surviving sources.</param>
        /// <param name="isSubject">Which candidates this pass considers rescuing.</param>
        /// <param name="isSource">Which files count as something that can name a subject.</param>
        /// <param name="pattern">The regex a source must match to count as naming the subject.</param>
        /// <returns>How many candidates were rescued.</returns>
        static int RescueNamedByText(List<string> candidates, IEnumerable<string> allAssets,
            System.Func<string, bool> isSubject, System.Func<string, bool> isSource,
            System.Func<string, string> pattern)
        {
            var doomed = new HashSet<string>(candidates);
            var pending = candidates.Where(isSubject).ToList();
            if (pending.Count == 0) return 0;

            var sources = allAssets.Where(p => isSource(p) && !doomed.Contains(p))
                .ToDictionary(p => p, File.ReadAllText);

            int rescued = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (string subject in pending.ToArray())
                {
                    var rx = new Regex(pattern(subject));
                    if (!sources.Any(s => s.Key != subject && rx.IsMatch(s.Value))) continue;

                    pending.Remove(subject);
                    doomed.Remove(subject);
                    candidates.Remove(subject);
                    if (isSource(subject)) sources[subject] = File.ReadAllText(subject);
                    rescued++;
                    changed = true;
                }
            }
            return rescued;
        }

        /// <summary>
        /// Everything a build can reach, walked recursively from the roots a build actually starts
        /// from, plus the handful of editor-only roots this project's tools depend on.
        /// </summary>
        static HashSet<string> FindReachable(string[] allAssets)
        {
            var roots = new HashSet<string>();

            // Only ENABLED scenes ship, so a disabled scene is not a root - it and whatever only
            // it uses are fair game.
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes.Where(s => s.enabled))
                roots.Add(scene.path);

            // Anything under a Resources folder can be pulled up by name at runtime, which leaves
            // no serialized reference to follow.
            foreach (string path in allAssets.Where(p => p.Contains("/Resources/")))
                roots.Add(path);

            foreach (Object asset in PlayerSettings.GetPreloadedAssets())
                AddIfInAssets(roots, asset);

            AddIfInAssets(roots, GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.count; i++)
                AddIfInAssets(roots, QualitySettings.GetRenderPipelineAssetAt(i));

            // Everything ProjectSettings points at by GUID: always-included shaders, the URP
            // global settings, the project-wide input actions registered as a config object, and
            // whatever a future package parks there. Reading the files catches all of them without
            // an API call per setting that would go stale.
            foreach (string file in Directory.GetFiles("ProjectSettings", "*.asset"))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"guid: ([0-9a-f]{32})"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/")) roots.Add(path);
                }
            }

            // Assembly definitions shape compilation rather than being referenced by it.
            foreach (string path in allAssets.Where(p => p.EndsWith(".asmdef") || p.EndsWith(".asmref")))
                roots.Add(path);

            foreach (string path in EditorToolRoots.Where(p => File.Exists(p)))
                roots.Add(path);

            var reachable = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
            reachable.UnionWith(roots);
            return reachable;
        }

        /// <summary>Adds an object's path to the set when it lives under Assets/.</summary>
        static void AddIfInAssets(HashSet<string> set, Object asset)
        {
            if (asset == null) return;

            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/")) set.Add(path);
        }

        /// <summary>
        /// Removes folders left holding nothing at all. Repeats until stable, so a branch that is
        /// empty only because its children were just removed collapses all the way up.
        /// </summary>
        static void DeleteEmptyFolders()
        {
            bool removedAny = true;
            while (removedAny)
            {
                removedAny = false;

                string[] folders = AssetDatabase.GetAllAssetPaths()
                    .Where(AssetDatabase.IsValidFolder)
                    .Where(p => p.StartsWith("Assets/"))
                    .OrderByDescending(p => p.Length)
                    .ToArray();

                foreach (string folder in folders)
                {
                    string full = Path.GetFullPath(folder);
                    if (!Directory.Exists(full)) continue;

                    bool empty = Directory.GetFiles(full).Length == 0 && Directory.GetDirectories(full).Length == 0;
                    if (empty && AssetDatabase.DeleteAsset(folder)) removedAny = true;
                }
            }

            AssetDatabase.Refresh();
        }
    }
}

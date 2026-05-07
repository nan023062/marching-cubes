using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MarchingCubes
{
    /// <summary>
    /// Editor window: batch-import case_*.fbx from an external directory into the project,
    /// and optionally auto-assign the imported prefabs to an ArtMeshCaseConfig asset.
    /// </summary>
    public class ArtMeshCaseImporter : EditorWindow
    {
        private string _sourceDir  = "";
        private string _targetPath = "Assets/MarchingCubes/ArtMesh/Cases";
        private ArtMeshCaseConfig _config;
        private Vector2 _scroll;
        private string  _log = "";

        [MenuItem("MarchingCubes/Art Mesh Case Importer")]
        static void Open() => GetWindow<ArtMeshCaseImporter>("Case Importer");

        void OnGUI()
        {
            GUILayout.Label("Art Mesh Case FBX Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ① Source directory
            EditorGUILayout.LabelField("① Source Directory (contains case_*.fbx)");
            using (new EditorGUILayout.HorizontalScope())
            {
                _sourceDir = EditorGUILayout.TextField(_sourceDir);
                if (GUILayout.Button("Browse…", GUILayout.Width(72)))
                    _sourceDir = EditorUtility.OpenFolderPanel("Select FBX Source", _sourceDir, "");
            }

            EditorGUILayout.Space();

            // ② Target path inside project
            EditorGUILayout.LabelField("② Target Path in Project (under Assets/)");
            _targetPath = EditorGUILayout.TextField(_targetPath);

            EditorGUILayout.Space();

            // ③ Optional config asset
            _config = (ArtMeshCaseConfig)EditorGUILayout.ObjectField(
                "③ Config Asset (auto-assign)", _config, typeof(ArtMeshCaseConfig), false);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_sourceDir)))
            {
                if (GUILayout.Button("Import All case_*.fbx", GUILayout.Height(32)))
                    DoImport();
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────────────

        void DoImport()
        {
            _log = "";

            if (!Directory.Exists(_sourceDir))
            {
                _log = $"[Error] Source directory not found:\n{_sourceDir}";
                Repaint(); return;
            }

            // Resolve full filesystem path for target
            string normalTarget = _targetPath.TrimEnd('/', '\\');
            string relative = normalTarget.StartsWith("Assets")
                ? normalTarget.Substring("Assets".Length).TrimStart('/', '\\')
                : normalTarget;
            string fullTarget = Path.Combine(Application.dataPath, relative);

            if (!Directory.Exists(fullTarget))
            {
                Directory.CreateDirectory(fullTarget);
                _log += $"Created: {normalTarget}\n";
            }

            // Collect case_*.fbx
            var regex = new Regex(@"case_(\d+)\.fbx$", RegexOptions.IgnoreCase);
            string[] files = Directory.GetFiles(_sourceDir, "case_*.fbx");

            if (files.Length == 0)
            {
                _log = "[Warn] No case_*.fbx found in source directory.";
                Repaint(); return;
            }

            // Copy files
            var importedCases = new List<int>();
            foreach (string src in files)
            {
                var m = regex.Match(Path.GetFileName(src));
                if (!m.Success) continue;

                int ci = int.Parse(m.Groups[1].Value);
                string dst = Path.Combine(fullTarget, $"case_{ci}.fbx");
                File.Copy(src, dst, overwrite: true);
                importedCases.Add(ci);
            }

            _log += $"Copied {importedCases.Count} files → {normalTarget}\n";
            _log += "Refreshing Asset Database…\n";
            AssetDatabase.Refresh();

            // Auto-assign prefabs to config
            if (_config != null && importedCases.Count > 0)
            {
                int assigned = 0;
                foreach (int ci in importedCases)
                {
                    string assetPath = $"{normalTarget}/case_{ci}.fbx";
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (go == null)
                    {
                        _log += $"  [Warn] Could not load asset: {assetPath}\n";
                        continue;
                    }
                    var entry = _config.GetEntry(ci);
                    if (entry != null)
                    {
                        entry.prefab = go;
                        assigned++;
                    }
                }
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();
                _log += $"Assigned {assigned} prefabs to Config.\n";
            }
            else if (_config == null)
            {
                _log += "(No Config asset selected — skipping prefab assignment)\n";
            }

            _log += "\n✓ Done.";
            Repaint();
        }
    }
}

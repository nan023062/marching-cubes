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
    /// Target directory is derived from the Config asset location (Config_dir/Cases/),
    /// or falls back to Assets/MarchingCubes/ArtMesh/Cases.
    /// </summary>
    public class ArtMeshCaseImporter : EditorWindow
    {
        private string            _sourceDir = "";
        private ArtMeshCaseConfig _config;
        private Vector2           _scroll;
        private string            _log = "";

        [MenuItem("MarchingCubes/Art Mesh Case Importer")]
        static void Open() => GetWindow<ArtMeshCaseImporter>("Case Importer");

        // ── 目标路径：从 Config 位置推断，否则用默认值 ────────────────────────
        string TargetAssetPath
        {
            get
            {
                if (_config != null)
                {
                    string configPath = AssetDatabase.GetAssetPath(_config);
                    string dir = Path.GetDirectoryName(configPath).Replace('\\', '/');
                    return dir + "/Cases";
                }
                return "Assets/MarchingCubes/ArtMesh/Cases";
            }
        }

        void OnGUI()
        {
            GUILayout.Label("Art Mesh Case FBX Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ① Source directory
            EditorGUILayout.LabelField("① Source Directory (Blender 导出的 case_*.fbx)");
            using (new EditorGUILayout.HorizontalScope())
            {
                _sourceDir = EditorGUILayout.TextField(_sourceDir);
                if (GUILayout.Button("Browse…", GUILayout.Width(72)))
                    _sourceDir = EditorUtility.OpenFolderPanel("Select FBX Source", _sourceDir, "");
            }

            EditorGUILayout.Space();

            // ② Config asset
            _config = (ArtMeshCaseConfig)EditorGUILayout.ObjectField(
                "② Config Asset (可选，自动赋值)", _config, typeof(ArtMeshCaseConfig), false);

            // 显示推断出的目标路径（只读提示）
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("   → 导入目标", TargetAssetPath);

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

            string targetAsset = TargetAssetPath;
            string relative    = targetAsset.StartsWith("Assets")
                ? targetAsset.Substring("Assets".Length).TrimStart('/', '\\')
                : targetAsset;
            string fullTarget = Path.Combine(Application.dataPath, relative);

            if (!Directory.Exists(fullTarget))
            {
                Directory.CreateDirectory(fullTarget);
                _log += $"Created: {targetAsset}\n";
            }

            var regex = new Regex(@"case_(\d+)\.fbx$", RegexOptions.IgnoreCase);
            string[] files = Directory.GetFiles(_sourceDir, "case_*.fbx");

            if (files.Length == 0)
            {
                _log = "[Warn] No case_*.fbx found in source directory.";
                Repaint(); return;
            }

            var importedCases = new List<int>();
            foreach (string src in files)
            {
                var m = regex.Match(Path.GetFileName(src));
                if (!m.Success) continue;

                int ci = int.Parse(m.Groups[1].Value);
                File.Copy(src, Path.Combine(fullTarget, $"case_{ci}.fbx"), overwrite: true);
                importedCases.Add(ci);
            }

            _log += $"Copied {importedCases.Count} files → {targetAsset}\n";
            _log += "Refreshing Asset Database…\n";
            AssetDatabase.Refresh();

            if (_config != null && importedCases.Count > 0)
            {
                int assigned = 0;
                foreach (int ci in importedCases)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{targetAsset}/case_{ci}.fbx");
                    if (go == null) { _log += $"  [Warn] Cannot load: case_{ci}.fbx\n"; continue; }
                    var entry = _config.GetEntry(ci);
                    if (entry != null) { entry.prefab = go; assigned++; }
                }
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();
                _log += $"Assigned {assigned} prefabs to Config.\n";
            }

            _log += "\n✓ Done.";
            Repaint();
        }
    }
}

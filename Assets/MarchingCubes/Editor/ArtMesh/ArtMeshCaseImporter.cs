using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

namespace MarchingCubes
{
    /// <summary>
    /// Editor window: scan a project-internal folder for case_*.fbx assets
    /// and assign them to an ArtMeshCaseConfig — no file copying needed.
    /// </summary>
    public class ArtMeshCaseImporter : EditorWindow
    {
        private string            _caseFolderAssetPath = "Assets/MarchingCubes/ArtMesh/Cases";
        private ArtMeshCaseConfig _config;
        private Vector2           _scroll;
        private string            _log = "";

        [MenuItem("MarchingCubes/Art Mesh Case Importer")]
        static void Open() => GetWindow<ArtMeshCaseImporter>("Case Importer");

        void OnGUI()
        {
            GUILayout.Label("Art Mesh Case Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ① Project-internal case folder
            EditorGUILayout.LabelField("① Case FBX Folder (项目内)");
            using (new EditorGUILayout.HorizontalScope())
            {
                _caseFolderAssetPath = EditorGUILayout.TextField(_caseFolderAssetPath);
                if (GUILayout.Button("Pick…", GUILayout.Width(56)))
                {
                    string picked = EditorUtility.OpenFolderPanel(
                        "Select Case FBX Folder",
                        Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                        "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        // 转换为 Assets/... 相对路径
                        string full = Path.GetFullPath(picked).Replace('\\', '/');
                        string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
                        if (full.StartsWith(data))
                            _caseFolderAssetPath = "Assets" + full.Substring(data.Length);
                    }
                }
            }

            EditorGUILayout.Space();

            // ② Config asset
            _config = (ArtMeshCaseConfig)EditorGUILayout.ObjectField(
                "② Config Asset", _config, typeof(ArtMeshCaseConfig), false);

            EditorGUILayout.Space();

            bool canRun = !string.IsNullOrEmpty(_caseFolderAssetPath) && _config != null;
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button("Assign case_*.fbx → Config", GUILayout.Height(32)))
                    DoAssign();
            }

            if (!canRun)
                EditorGUILayout.HelpBox("需要同时指定 Folder 和 Config Asset。", MessageType.Info);

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        void DoAssign()
        {
            _log = "";

            string folder = _caseFolderAssetPath.TrimEnd('/', '\\');
            string fullFolder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", folder)).Replace('\\', '/');

            if (!Directory.Exists(fullFolder))
            {
                _log = $"[Error] Folder not found: {folder}"; Repaint(); return;
            }

            var regex   = new Regex(@"case_(\d+)\.fbx$", RegexOptions.IgnoreCase);
            string[] files = Directory.GetFiles(fullFolder, "case_*.fbx");

            if (files.Length == 0)
            {
                _log = "[Warn] No case_*.fbx found in folder."; Repaint(); return;
            }

            int assigned = 0;
            foreach (string f in files)
            {
                var m = regex.Match(Path.GetFileName(f));
                if (!m.Success) continue;

                int ci = int.Parse(m.Groups[1].Value);
                string assetPath = $"{folder}/case_{ci}.fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (go == null) { _log += $"  [Warn] Not found in AssetDB: {assetPath}\n"; continue; }

                var entry = _config.GetEntry(ci);
                if (entry != null) { entry.prefab = go; assigned++; }
                else               { _log += $"  [Warn] No entry for ci={ci}\n"; }
            }

            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            _log += $"Assigned {assigned} / {files.Length} prefabs to Config.\n✓ Done.";
            Repaint();
        }
    }
}

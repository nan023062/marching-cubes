using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// 在 Unity 编辑器内程序化生成 255 个 case prefab（p_case_1 … p_case_254）。
    /// 完全在 Unity 坐标系内计算，无 Blender / FBX 依赖。
    /// 圆角方案参照 Blender mc_artmesh 插件：
    ///   - 连通分量 BFS + dissolve 共面边 + 1/4 圆弧 bevel
    ///   - 侧面封闭边 sideRadius / 顶面（+Y 法线）topRadius 独立设置
    ///   - 顶点色：封闭=蓝 / 开放=灰 / 顶面=棕
    /// </summary>
    public class CaseMeshProceduralGenerator : EditorWindow
    {
        private ArtMeshCaseConfig _config;
        private string            _outputFolder = "Assets/MarchingCubes/ArtMesh/Generated";

        [Range(0f, 0.24f)] private float _sideRadius = 0.08f;
        [Range(0f, 0.24f)] private float _topRadius  = 0f;
        [Range(1, 12)]     private int   _segments   = 4;

        private string  _log    = "";
        private Vector2 _scroll;

        [MenuItem("MarchingCubes/Procedural Case Mesh Generator")]
        static void Open() => GetWindow<CaseMeshProceduralGenerator>("Case Mesh Gen");

        void OnGUI()
        {
            GUILayout.Label("Procedural Case Mesh Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "直接在 Unity 坐标系内程序化生成，无 Blender/FBX 依赖。\n" +
                "圆角算法：连通分量 BFS + arc bevel（侧/顶独立半径）+ 顶点色。",
                MessageType.Info);
            EditorGUILayout.Space(4);

            _config = (ArtMeshCaseConfig)EditorGUILayout.ObjectField(
                "Config Asset", _config, typeof(ArtMeshCaseConfig), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Output Folder");
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField(_outputFolder);
                if (GUILayout.Button("Pick", GUILayout.Width(46)))
                {
                    string def = Path.GetFullPath(Path.Combine(Application.dataPath,
                        _outputFolder.StartsWith("Assets")
                            ? _outputFolder.Substring("Assets".Length).TrimStart('/', '\\')
                            : _outputFolder));
                    string picked = EditorUtility.OpenFolderPanel("Output Folder", def, "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string full = Path.GetFullPath(picked).Replace('\\', '/');
                        string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
                        if (full.StartsWith(data))
                            _outputFolder = "Assets" + full.Substring(data.Length);
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Rounding", EditorStyles.boldLabel);
            _sideRadius = EditorGUILayout.Slider("Side Arc Radius", _sideRadius, 0f, 0.24f);
            _topRadius  = EditorGUILayout.Slider("Top  Arc Radius", _topRadius,  0f, 0.24f);
            _segments   = EditorGUILayout.IntSlider("Arc Segments",  _segments,  1, 12);
            EditorGUILayout.HelpBox(
                "Side = 侧面封闭边圆弧 / Top = 顶面（地面朝向）封闭边圆弧\n" +
                "顶点色：蓝=封闭面  灰=开放面  棕=顶面地表",
                MessageType.None);

            EditorGUILayout.Space(4);
            bool canRun = _config != null && !string.IsNullOrEmpty(_outputFolder);
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button("Generate All 255 Case Prefabs", GUILayout.Height(32)))
                    DoGenerate();
            }
            if (!canRun) EditorGUILayout.HelpBox("请先指定 Config Asset。", MessageType.Warning);

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ════════════════════════════════════════════════════════════════════

        void DoGenerate()
        {
            _log = "";
            _config.EnsureSymmetry();

            string rel  = _outputFolder.TrimEnd('/', '\\');
            string full = Path.GetFullPath(Path.Combine(Application.dataPath,
                rel.StartsWith("Assets") ? rel.Substring("Assets".Length).TrimStart('/', '\\') : rel));
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);

            string meshDir  = rel  + "/Meshes";
            string meshFull = full + "/Meshes";
            if (!Directory.Exists(meshFull)) Directory.CreateDirectory(meshFull);
            AssetDatabase.Refresh();

            var meshCache = new Dictionary<string, Mesh>(); // key = "{canonical}_{sR}_{tR}_{segs}"

            int ok = 0, skip = 0;
            for (int ci = 1; ci <= 254; ci++)
            {
                int  canonical = _config.GetCanonicalIndex(ci);
                var  d4        = _config.GetRotation(ci);
                bool isFlipped = _config.GetFlipped(ci);

                // canonical mesh（带圆角参数）
                string key = $"{canonical}_{_sideRadius:F3}_{_topRadius:F3}_{_segments}";
                if (!meshCache.TryGetValue(key, out Mesh canonMesh))
                {
                    var raw = ProceduralCaseMesh.Build(
                        canonical, _sideRadius, _topRadius, _segments);
                    if (raw == null) { skip++; continue; }

                    string meshPath = $"{meshDir}/mesh_{canonical}.asset";
                    AssetDatabase.CreateAsset(raw, meshPath);
                    meshCache[key] = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                }
                canonMesh = meshCache[key];
                if (canonMesh == null) { skip++; continue; }

                // 构建 prefab 层级
                var root  = new GameObject($"p_case_{ci}");
                var gComp = root.AddComponent<CubedMeshPrefab>();
                gComp.mask = (CubeVertexMask)ci;

                var child = new GameObject("mesh");
                child.transform.SetParent(root.transform, false);
                var mf = child.AddComponent<MeshFilter>();
                mf.sharedMesh = canonMesh;
                child.AddComponent<MeshRenderer>();

                // D4 旋转（围绕 cube 中心 0.5,0.5,0.5）
                var s = new Vector3(0.5f, 0.5f, 0.5f);
                child.transform.localRotation = d4;
                child.transform.localPosition = s - d4 * s;
                child.transform.localScale    = isFlipped
                    ? new Vector3(-1f, 1f, 1f) : Vector3.one;

                string prefabPath = $"{rel}/p_case_{ci}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);

                _config.SetPrefab(ci, saved);
                ok++;

                if (ci % 20 == 0)
                    EditorUtility.DisplayProgressBar("Generating", $"p_case_{ci}", ci / 254f);
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _log = $"✓ Generated {ok} prefabs  (skipped {skip})\n→ {rel}";
            Repaint();
        }
    }
}

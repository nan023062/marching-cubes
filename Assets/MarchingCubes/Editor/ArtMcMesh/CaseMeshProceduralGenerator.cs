using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// 在 Unity 编辑器内程序化生成 254 个 case prefab（p_case_1 … p_case_254）。
    /// 具体 mesh 算法由 CaseMeshBuilderAsset 插槽决定——拖入不同的 Builder Asset 即可切换风格。
    /// </summary>
    public class CaseMeshProceduralGenerator : EditorWindow
    {
        [SerializeField] private D4FbxCaseConfig    _config;
        [SerializeField] private CaseMeshBuilderAsset _builder;

        private string  _outputFolder = "Assets/MarchingCubes/ArtMesh/Generated";
        private string  _log          = "";
        private Vector2 _scroll;

        private SerializedObject   _so;
        private SerializedProperty _spConfig;
        private SerializedProperty _spBuilder;

        [MenuItem("MarchingCubes/Procedural Case Mesh Generator")]
        static void Open() => GetWindow<CaseMeshProceduralGenerator>("Case Mesh Gen");

        void OnEnable()
        {
            _so        = new SerializedObject(this);
            _spConfig  = _so.FindProperty("_config");
            _spBuilder = _so.FindProperty("_builder");
        }

        void OnGUI()
        {
            _so.Update();

            GUILayout.Label("Procedural Case Mesh Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "算法由 CaseMeshBuilderAsset 决定。\n" +
                "右键 Assets → Create → MarchingCubes/CaseMeshBuilder 创建算法资产，拖入 Builder 字段。",
                MessageType.Info);
            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(_spConfig,  new GUIContent("Config Asset"));
            EditorGUILayout.PropertyField(_spBuilder, new GUIContent("Builder Asset"));

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

            _so.ApplyModifiedProperties();

            EditorGUILayout.Space(4);

            // Builder 的 Inspector（内联显示参数）
            if (_builder != null)
            {
                EditorGUILayout.LabelField("Builder Parameters", EditorStyles.boldLabel);
                var builderSo = new SerializedObject(_builder);
                builderSo.Update();
                SerializedProperty prop = builderSo.GetIterator();
                prop.NextVisible(true); // skip m_Script
                while (prop.NextVisible(false))
                    EditorGUILayout.PropertyField(prop, true);
                builderSo.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(4);
            bool canRun = _config != null && _builder != null && !string.IsNullOrEmpty(_outputFolder);
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button("Generate All 254 Case Prefabs", GUILayout.Height(32)))
                    DoGenerate();
            }
            if (_config  == null) EditorGUILayout.HelpBox("请指定 Config Asset。",  MessageType.Warning);
            if (_builder == null) EditorGUILayout.HelpBox("请指定 Builder Asset。", MessageType.Warning);

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ════════════════════════════════════════════════════════════════════

        void DoGenerate()
        {
            _log = "";
            _config.EnsureSymmetry();

            string rel      = _outputFolder.TrimEnd('/', '\\');
            string fullBase = Path.GetFullPath(Path.Combine(Application.dataPath,
                rel.StartsWith("Assets") ? rel.Substring("Assets".Length).TrimStart('/', '\\') : rel));
            if (!Directory.Exists(fullBase)) Directory.CreateDirectory(fullBase);

            string meshRel  = rel  + "/Meshes";
            string meshFull = fullBase + "/Meshes";
            if (!Directory.Exists(meshFull)) Directory.CreateDirectory(meshFull);
            AssetDatabase.Refresh();

            // canonical → Mesh 缓存（同一算法资产 + 同一 canonical index 只生成一次）
            var meshCache = new System.Collections.Generic.Dictionary<int, Mesh>();

            int ok = 0, skip = 0;
            for (int ci = 1; ci <= 254; ci++)
            {
                int      canonical = _config.GetCanonicalIndex(ci);
                var      d4        = _config.GetRotation(ci);
                bool     isFlipped = _config.GetFlipped(ci);

                if (!meshCache.TryGetValue(canonical, out Mesh canonMesh))
                {
                    var raw = _builder.Build(canonical);
                    if (raw == null) { meshCache[canonical] = null; skip++; continue; }

                    string meshPath = $"{meshRel}/mesh_{canonical}.asset";
                    AssetDatabase.CreateAsset(raw, meshPath);
                    meshCache[canonical] = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                }

                canonMesh = meshCache[canonical];
                if (canonMesh == null) { skip++; continue; }

                // ── prefab 层级 ──────────────────────────────────────────────
                var root  = new GameObject($"p_case_{ci}");
                var gComp = root.AddComponent<CubedMeshPrefab>();
                gComp.mask = (CubeVertexMask)ci;

                var child = new GameObject("mesh");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = canonMesh;
                child.AddComponent<MeshRenderer>();

                // ── D4 变换（围绕 cube 中心 0.5,0.5,0.5）────────────────────
                // EnsureSymmetry 存的是「ci→canonical」方向的旋转：
                //   non-flip 需取逆；flip 变换自逆直接用。
                // pivot：non-flip = S_CENTER；flip = (-0.5,0.5,0.5)。
                var s       = new Vector3(0.5f, 0.5f, 0.5f);
                var d4apply = isFlipped ? d4 : Quaternion.Inverse(d4);
                var pivot   = isFlipped ? new Vector3(-0.5f, 0.5f, 0.5f) : s;

                child.transform.localPosition = s - d4apply * pivot;
                child.transform.localRotation = d4apply;
                child.transform.localScale    = isFlipped ? new Vector3(-1f, 1f, 1f) : Vector3.one;

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
            _log = $"✓ Generated {ok}  skipped {skip}\n→ {rel}";
            Repaint();
        }
    }
}

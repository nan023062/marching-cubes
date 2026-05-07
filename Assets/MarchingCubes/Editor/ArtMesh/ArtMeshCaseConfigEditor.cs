using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(ArtMeshCaseConfig))]
    public sealed class ArtMeshCaseConfigEditor : UnityEditor.Editor
    {
        // ── State ────────────────────────────────────────────────────────────
        private string  _fbxFolder    = "Assets/MarchingCubes/ArtMesh/Cases";
        private string  _prefabFolder = "Assets/MarchingCubes/ArtMesh/Prefabs";
        private string  _log          = "";
        private bool    _canonicalOnly;
        private int     _selected = -1;
        private Vector2 _gridScroll;

        // ── Colours ──────────────────────────────────────────────────────────
        private static readonly Color ColHas  = new Color(0.25f, 0.65f, 0.30f);
        private static readonly Color ColNone = new Color(0.28f, 0.28f, 0.28f);
        private static readonly Color ColSel  = new Color(0.95f, 0.70f, 0.10f);
        private static readonly Color ColCan  = new Color(0.35f, 0.55f, 0.85f);
        private static readonly Color ColBg   = new Color(0.18f, 0.18f, 0.18f);

        private const float CellSz = 26f;
        private const int   Cols   = 16;

        private static readonly Vector3 S_CENTER = new Vector3(0.5f, 0.5f, 0.5f);

        // ════════════════════════════════════════════════════════════════════
        public override void OnInspectorGUI()
        {
            var cfg = (ArtMeshCaseConfig)target;
            serializedObject.Update();

            DrawBuildSection(cfg);
            EditorGUILayout.Space(6);
            DrawGrid(cfg);
            EditorGUILayout.Space(4);
            if (_selected >= 0) DrawDetail(cfg, _selected);

            serializedObject.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════════════
        // Build 255 Prefabs
        // ════════════════════════════════════════════════════════════════════

        void DrawBuildSection(ArtMeshCaseConfig cfg)
        {
            EditorGUILayout.LabelField("Build Case Prefabs (p_case_xx)", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("① Canonical FBX folder (case_*.fbx)");
            DrawFolderPicker(ref _fbxFolder, "FBX Folder");

            EditorGUILayout.LabelField("② Output prefab folder");
            DrawFolderPicker(ref _prefabFolder, "Prefab Output");

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Build All 255 Case Prefabs  (p_case_1 … p_case_254)",
                    GUILayout.Height(30)))
                DoBuild(cfg);

            if (!string.IsNullOrEmpty(_log))
                EditorGUILayout.LabelField(_log,
                    new GUIStyle(EditorStyles.helpBox) { wordWrap = true });
        }

        void DrawFolderPicker(ref string path, string title)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = EditorGUILayout.TextField(path);
                if (GUILayout.Button("Pick", GUILayout.Width(46)))
                {
                    string rel  = path.StartsWith("Assets") ? path.Substring("Assets".Length).TrimStart('/', '\\') : path;
                    string def  = Path.GetFullPath(Path.Combine(Application.dataPath, rel)).Replace('\\', '/');
                    if (!Directory.Exists(def)) def = Application.dataPath;
                    string picked = EditorUtility.OpenFolderPanel(title, def, "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string full = Path.GetFullPath(picked).Replace('\\', '/');
                        string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
                        if (full.StartsWith(data)) path = "Assets" + full.Substring(data.Length);
                    }
                }
            }
        }

        void DoBuild(ArtMeshCaseConfig cfg)
        {
            _log = "";
            cfg.EnsureSymmetry();

            // 确保输出目录存在
            string relOut = _prefabFolder.TrimEnd('/', '\\');
            string fullOut = Path.GetFullPath(
                Path.Combine(Application.dataPath,
                    relOut.StartsWith("Assets") ? relOut.Substring("Assets".Length).TrimStart('/', '\\') : relOut));
            if (!Directory.Exists(fullOut)) Directory.CreateDirectory(fullOut);
            AssetDatabase.Refresh();

            int ok = 0, skip = 0;
            for (int ci = 1; ci <= 254; ci++)
            {
                int  canonical = cfg.GetCanonicalIndex(ci);
                Quaternion d4  = cfg.GetRotation(ci);
                bool isFlipped = cfg.GetFlipped(ci);

                string fbxPath = $"{_fbxFolder.TrimEnd('/', '\\')}/case_{canonical}.fbx";
                var canonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (canonPrefab == null) { skip++; continue; }

                // 在场景中临时构建层级
                var root  = new GameObject($"p_case_{ci}");
                var pComp = root.AddComponent<CubedMeshPrefab>();
                pComp.mask = (CubeVertexMask)ci;

                // 实例化 canonical FBX 作为子节点
                var child = (GameObject)PrefabUtility.InstantiatePrefab(canonPrefab, root.transform);

                // FBX 自带轴旋转（bakeAxisConversion 可能未生效时仍有值）
                Quaternion fbxBase = child.transform.localRotation;
                Quaternion total   = d4 * fbxBase;
                child.transform.localPosition = S_CENTER - total * S_CENTER;
                child.transform.localRotation  = total;
                child.transform.localScale     = isFlipped
                    ? new Vector3(-1f, 1f, 1f) : Vector3.one;

                // 保存为 prefab
                string prefabPath = $"{relOut}/p_case_{ci}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);

                cfg.SetPrefab(ci, saved);
                ok++;

                if (ci % 20 == 0)
                    EditorUtility.DisplayProgressBar("Building Prefabs",
                        $"p_case_{ci}", ci / 254f);
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _log = $"✓ Built {ok} prefabs → {relOut}  (skipped {skip})";
            Repaint();
        }

        // ════════════════════════════════════════════════════════════════════
        // Grid
        // ════════════════════════════════════════════════════════════════════

        void DrawGrid(ArtMeshCaseConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cases", EditorStyles.boldLabel, GUILayout.Width(50));
                bool cOnly = GUILayout.Toggle(_canonicalOnly, "Canonical",  EditorStyles.miniButtonLeft,  GUILayout.Width(72));
                bool all   = GUILayout.Toggle(!_canonicalOnly, "All 256",  EditorStyles.miniButtonRight, GUILayout.Width(56));
                if (cOnly != _canonicalOnly || !all != !_canonicalOnly) _canonicalOnly = cOnly;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66))) DoValidate(cfg);
            }

            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0,0,0,0.3f));

            float h = _canonicalOnly ? CellSz * 6 : CellSz * 16;
            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(Mathf.Min(h, CellSz * 16)));
            Rect outer = GUILayoutUtility.GetRect(Cols * CellSz, h);
            EditorGUI.DrawRect(outer, ColBg);

            if (_canonicalOnly)
            {
                int n = 0;
                for (int ci = 1; ci <= 254; ci++)
                {
                    if (!cfg.IsCanonical(ci)) continue;
                    int col = n % Cols, row = n / Cols;
                    DrawCell(cfg, ci, new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1));
                    n++;
                }
            }
            else
            {
                for (int ci = 0; ci < 256; ci++)
                {
                    int col = ci % Cols, row = ci / Cols;
                    DrawCell(cfg, ci, new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1));
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                Dot(ColHas);  GUILayout.Label("Has Prefab", GUILayout.Width(74));
                Dot(ColNone); GUILayout.Label("Empty",      GUILayout.Width(50));
                Dot(ColCan);  GUILayout.Label("Canonical",  GUILayout.Width(70));
                Dot(ColSel);  GUILayout.Label("Selected",   GUILayout.Width(60));
            }
        }

        void DrawCell(ArtMeshCaseConfig cfg, int ci, Rect r)
        {
            bool hasPrefab = ci > 0 && ci < 255 && cfg.GetPrefab(ci) != null;
            bool isCanon   = ci > 0 && ci < 255 && cfg.IsCanonical(ci);
            bool isSel     = ci == _selected;

            Color bg = isSel ? ColSel : hasPrefab ? ColHas : isCanon ? ColCan : ColNone;
            EditorGUI.DrawRect(r, bg);
            GUI.Label(r, ci.ToString(),
                new GUIStyle(EditorStyles.miniLabel)
                    { fontSize = 7, alignment = TextAnchor.MiddleCenter,
                      normal = { textColor = Color.white } });

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                _selected = (_selected == ci) ? -1 : ci;
                Event.current.Use(); Repaint();
            }
        }

        static void Dot(Color c)
        {
            Rect r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
            r.y += 3; EditorGUI.DrawRect(r, c);
        }

        void DoValidate(ArtMeshCaseConfig cfg)
        {
            int total = 0, filled = 0;
            for (int ci = 1; ci <= 254; ci++) { total++; if (cfg.GetPrefab(ci) != null) filled++; }
            EditorUtility.DisplayDialog("Validate",
                $"Cases 1–254: {total}\nWith prefab: {filled}\nMissing: {total - filled}", "OK");
        }

        // ════════════════════════════════════════════════════════════════════
        // Detail
        // ════════════════════════════════════════════════════════════════════

        void DrawDetail(ArtMeshCaseConfig cfg, int ci)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0,0,0,0.3f));
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField($"Case {ci}", EditorStyles.boldLabel);
            if (ci > 0 && ci < 255)
            {
                EditorGUILayout.LabelField($"Canonical: {cfg.GetCanonicalIndex(ci)}", EditorStyles.miniLabel);
                var euler = cfg.GetRotation(ci).eulerAngles;
                EditorGUILayout.LabelField($"D4 Rotation Y: {euler.y:F0}°  Flip: {cfg.GetFlipped(ci)}", EditorStyles.miniLabel);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                var cur = cfg.GetPrefab(ci);
                var nxt = (GameObject)EditorGUILayout.ObjectField("p_case Prefab", cur, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                {
                    cfg.SetPrefab(ci, nxt);
                    EditorUtility.SetDirty(cfg);
                }

                // Asset preview
                if (cur != null)
                {
                    var tex = AssetPreview.GetAssetPreview(cur);
                    if (tex != null) GUILayout.Label(tex, GUILayout.Width(80), GUILayout.Height(80));
                }

                DrawTopology(ci, 80f);
            }
        }

        void DrawTopology(int cubeIndex, float size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f));
            float pad = size * 0.12f, w = size - pad * 2;
            Vector2 Proj(float x, float y, float z) => new Vector2(
                r.x + pad + (x + z * 0.45f) * w * 0.55f,
                r.yMax - pad - (y * 0.65f + (1f - z) * 0.25f) * w * 0.7f);
            var pts = new Vector2[8];
            for (int i = 0; i < 8; i++) { var v = CubeTable.Vertices[i]; pts[i] = Proj(v.x, v.y, v.z); }
            int[,] edges = {{0,1},{1,2},{2,3},{3,0},{4,5},{5,6},{6,7},{7,4},{0,4},{1,5},{2,6},{3,7}};
            for (int e = 0; e < 12; e++)
                { Handles.color = new Color(0.5f,0.5f,0.5f);
                  Handles.DrawLine(new Vector3(pts[edges[e,0]].x, pts[edges[e,0]].y),
                                   new Vector3(pts[edges[e,1]].x, pts[edges[e,1]].y)); }
            float dotR = size * 0.065f;
            for (int i = 0; i < 8; i++)
            {
                bool active = (cubeIndex & (1 << i)) != 0;
                EditorGUI.DrawRect(new Rect(pts[i].x - dotR, pts[i].y - dotR, dotR * 2, dotR * 2),
                    active ? new Color(1f, 0.25f, 0.25f) : new Color(0.4f, 0.4f, 0.4f));
            }
        }
    }
}

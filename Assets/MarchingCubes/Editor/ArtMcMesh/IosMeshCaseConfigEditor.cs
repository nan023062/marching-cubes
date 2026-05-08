using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(IosMeshCaseConfig))]
    public sealed class IosMeshCaseConfigEditor : UnityEditor.Editor
    {
        // ── State ────────────────────────────────────────────────────────────
        private string   _meshFolder   = "Assets/MarchingCubes/Sample/Resources/ios_mesh/meshes";
        private string   _prefabFolder = "Assets/MarchingCubes/ArtMesh/IosPrefabs";
        private Material _material;
        private string   _log          = "";
        private int     _selected     = -1;
        private Vector2 _gridScroll;

        // ── Colours ──────────────────────────────────────────────────────────
        private static readonly Color ColHas  = new Color(0.25f, 0.65f, 0.30f);
        private static readonly Color ColNone = new Color(0.28f, 0.28f, 0.28f);
        private static readonly Color ColSel  = new Color(0.95f, 0.70f, 0.10f);
        private static readonly Color ColBg   = new Color(0.18f, 0.18f, 0.18f);

        private const float CellSz = 26f;
        private const int   Cols   = 16;

        // ════════════════════════════════════════════════════════════════════
        public override void OnInspectorGUI()
        {
            var cfg = (IosMeshCaseConfig)target;
            serializedObject.Update();

            DrawBuildSection(cfg);
            EditorGUILayout.Space(6);
            DrawGrid(cfg);
            EditorGUILayout.Space(4);
            if (_selected >= 0) DrawDetail(cfg, _selected);

            serializedObject.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════════════
        // Build
        // ════════════════════════════════════════════════════════════════════

        void DrawBuildSection(IosMeshCaseConfig cfg)
        {
            EditorGUILayout.LabelField("Build Case Prefabs (cm_xx)", EditorStyles.boldLabel);

            DrawFolderField("① Mesh folder (cm_*.asset)", ref _meshFolder);
            DrawFolderField("② Output prefab folder",    ref _prefabFolder);
            _material = (Material)EditorGUILayout.ObjectField("③ Material", _material, typeof(Material), false);

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Build All 256 Case Prefabs  (cm_0 … cm_255)", GUILayout.Height(30)))
                DoBuild(cfg);

            if (!string.IsNullOrEmpty(_log))
                EditorGUILayout.LabelField(_log,
                    new GUIStyle(EditorStyles.helpBox) { wordWrap = true });
        }

        void DrawFolderField(string label, ref string path)
        {
            EditorGUILayout.LabelField(label);
            var current = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            EditorGUI.BeginChangeCheck();
            var dragged = (DefaultAsset)EditorGUILayout.ObjectField(
                current, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck() && dragged != null)
            {
                string p = AssetDatabase.GetAssetPath(dragged);
                if (AssetDatabase.IsValidFolder(p)) path = p;
            }
        }

        void DoBuild(IosMeshCaseConfig cfg)
        {
            _log = "";

            string relOut  = _prefabFolder.TrimEnd('/', '\\');
            string fullOut = Path.GetFullPath(Path.Combine(Application.dataPath,
                relOut.StartsWith("Assets")
                    ? relOut.Substring("Assets".Length).TrimStart('/', '\\')
                    : relOut));
            if (!Directory.Exists(fullOut)) Directory.CreateDirectory(fullOut);
            AssetDatabase.Refresh();

            string meshRel = _meshFolder.TrimEnd('/', '\\');

            int ok = 0, skip = 0;
            for (int ci = 0; ci < 256; ci++)
            {
                string meshPath = $"{meshRel}/cm_{ci}.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh == null) { skip++; continue; }

                var root  = new GameObject($"cm_{ci}");
                var pComp = root.AddComponent<CubedMeshPrefab>();
                pComp.mask = (CubeVertexMask)ci;

                var child = new GameObject("mesh");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = _material;

                string prefabPath = $"{relOut}/cm_{ci}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);

                cfg.SetPrefab(ci, saved);
                ok++;

                if (ci % 20 == 0)
                    EditorUtility.DisplayProgressBar("Building Prefabs", $"cm_{ci}", ci / 255f);
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

        void DrawGrid(IosMeshCaseConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cases", EditorStyles.boldLabel, GUILayout.Width(50));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66))) DoValidate(cfg);
            }

            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));

            float h = CellSz * 16;
            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(Mathf.Min(h, CellSz * 16)));
            Rect outer = GUILayoutUtility.GetRect(Cols * CellSz, h);
            EditorGUI.DrawRect(outer, ColBg);

            for (int ci = 0; ci < 256; ci++)
            {
                int col = ci % Cols, row = ci / Cols;
                DrawCell(cfg, ci, new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1));
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                Dot(ColHas);  GUILayout.Label("Has Prefab", GUILayout.Width(74));
                Dot(ColNone); GUILayout.Label("Empty",      GUILayout.Width(50));
                Dot(ColSel);  GUILayout.Label("Selected",   GUILayout.Width(60));
            }
        }

        void DrawCell(IosMeshCaseConfig cfg, int ci, Rect r)
        {
            bool hasPrefab = cfg.GetPrefab(ci) != null;
            bool isSel     = ci == _selected;

            Color bg = isSel ? ColSel : hasPrefab ? ColHas : ColNone;
            EditorGUI.DrawRect(r, bg);
            GUI.Label(r, ci.ToString(),
                new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 7, alignment = TextAnchor.MiddleCenter,
                    normal   = { textColor = Color.white }
                });

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

        void DoValidate(IosMeshCaseConfig cfg)
        {
            int filled = 0;
            for (int ci = 0; ci < 256; ci++)
                if (cfg.GetPrefab(ci) != null) filled++;
            EditorUtility.DisplayDialog("Validate",
                $"Cases 0–255: 256\nWith prefab: {filled}\nMissing: {256 - filled}", "OK");
        }

        // ════════════════════════════════════════════════════════════════════
        // Detail
        // ════════════════════════════════════════════════════════════════════

        void DrawDetail(IosMeshCaseConfig cfg, int ci)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField($"Case {ci}", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var cur = cfg.GetPrefab(ci);
            var nxt = (GameObject)EditorGUILayout.ObjectField("cm Prefab", cur, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                cfg.SetPrefab(ci, nxt);
                EditorUtility.SetDirty(cfg);
            }

            if (cur != null)
            {
                var tex = AssetPreview.GetAssetPreview(cur);
                if (tex != null) GUILayout.Label(tex, GUILayout.Width(80), GUILayout.Height(80));
            }

            DrawTopology(ci, 80f);
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
            int[,] edges = { {0,1},{1,2},{2,3},{3,0},{4,5},{5,6},{6,7},{7,4},{0,4},{1,5},{2,6},{3,7} };
            for (int e = 0; e < 12; e++)
            {
                Handles.color = new Color(0.5f, 0.5f, 0.5f);
                Handles.DrawLine(new Vector3(pts[edges[e, 0]].x, pts[edges[e, 0]].y),
                                 new Vector3(pts[edges[e, 1]].x, pts[edges[e, 1]].y));
            }
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

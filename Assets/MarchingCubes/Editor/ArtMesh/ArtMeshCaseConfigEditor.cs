using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(ArtMeshCaseConfig))]
    public sealed class ArtMeshCaseConfigEditor : UnityEditor.Editor
    {
        // ── Import state ─────────────────────────────────────────────────────
        private string _importFolder = "Assets/MarchingCubes/ArtMesh/Cases";
        private string _importLog    = "";

        // ── Grid state ───────────────────────────────────────────────────────
        private bool    _canonicalOnly = false;
        private int     _selected      = -1;
        private Vector2 _gridScroll;

        // ── Styles (lazy) ────────────────────────────────────────────────────
        private GUIStyle _cellStyle;
        private GUIStyle CellStyle => _cellStyle ??= new GUIStyle(GUI.skin.box)
            { padding = new RectOffset(0,0,0,0), margin = new RectOffset(1,1,1,1) };

        // ── Colours ──────────────────────────────────────────────────────────
        private static readonly Color ColHasPrefab  = new Color(0.25f, 0.65f, 0.30f);
        private static readonly Color ColNoPrefab   = new Color(0.28f, 0.28f, 0.28f);
        private static readonly Color ColSelected   = new Color(0.95f, 0.70f, 0.10f);
        private static readonly Color ColCanonical  = new Color(0.35f, 0.55f, 0.85f);
        private static readonly Color ColBackground = new Color(0.18f, 0.18f, 0.18f);

        private const float CellSize  = 26f;
        private const int   GridCols  = 16;
        private const float GridH     = CellSize * 16 + 8f;   // 256/16 = 16 rows

        // ────────────────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var config = (ArtMeshCaseConfig)target;
            serializedObject.Update();

            DrawImportSection(config);
            EditorGUILayout.Space(6);
            DrawGrid(config);
            EditorGUILayout.Space(4);
            if (_selected >= 0)
                DrawDetail(config, _selected);

            serializedObject.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════════════
        // Import
        // ════════════════════════════════════════════════════════════════════

        void DrawImportSection(ArtMeshCaseConfig config)
        {
            EditorGUILayout.LabelField("Import FBX", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _importFolder = EditorGUILayout.TextField(_importFolder);
                if (GUILayout.Button("Pick", GUILayout.Width(46)))
                {
                    // 默认打开当前 _importFolder 所在的绝对路径
                    string relative = _importFolder.StartsWith("Assets")
                        ? _importFolder.Substring("Assets".Length).TrimStart('/', '\\')
                        : _importFolder;
                    string defaultPath = Path.GetFullPath(
                        Path.Combine(Application.dataPath, relative)).Replace('\\', '/');
                    if (!Directory.Exists(defaultPath))
                        defaultPath = Application.dataPath;

                    string picked = EditorUtility.OpenFolderPanel("Case FBX Folder", defaultPath, "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string full = Path.GetFullPath(picked).Replace('\\', '/');
                        string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
                        if (full.StartsWith(data))
                            _importFolder = "Assets" + full.Substring(data.Length);
                    }
                }
            }

            if (GUILayout.Button("Import & Assign All case_*.fbx"))
                DoImport(config);

            if (!string.IsNullOrEmpty(_importLog))
            {
                var style = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                EditorGUILayout.LabelField(_importLog, style);
            }
        }

        void DoImport(ArtMeshCaseConfig config)
        {
            _importLog = "";
            string folder = _importFolder.TrimEnd('/', '\\');
            string relative = folder.StartsWith("Assets")
                ? folder.Substring("Assets".Length).TrimStart('/', '\\')
                : folder;
            string full = Path.Combine(Application.dataPath, relative).Replace('\\', '/');

            if (!Directory.Exists(full)) { _importLog = "[Error] Folder not found: " + folder; return; }

            var regex = new Regex(@"case_(\d+)\.fbx$", RegexOptions.IgnoreCase);
            string[] files = Directory.GetFiles(full, "case_*.fbx");
            if (files.Length == 0) { _importLog = "[Warn] No case_*.fbx found."; return; }

            int assigned = 0;
            foreach (string f in files)
            {
                var m = regex.Match(Path.GetFileName(f));
                if (!m.Success) continue;
                int ci = int.Parse(m.Groups[1].Value);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/case_{ci}.fbx");
                if (go == null) continue;
                var entry = config.GetEntry(ci);
                if (entry != null) { entry.prefab = go; assigned++; }
            }
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            _importLog = $"✓ Assigned {assigned} / {files.Length} prefabs.";
            Repaint();
        }

        // ════════════════════════════════════════════════════════════════════
        // Grid
        // ════════════════════════════════════════════════════════════════════

        void DrawGrid(ArtMeshCaseConfig config)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cases", EditorStyles.boldLabel, GUILayout.Width(50));
                bool cOnly = GUILayout.Toggle(_canonicalOnly, "Canonical 53",
                    EditorStyles.miniButtonLeft, GUILayout.Width(90));
                bool all   = GUILayout.Toggle(!_canonicalOnly, "All 256",
                    EditorStyles.miniButtonRight, GUILayout.Width(60));
                if (cOnly != _canonicalOnly || !all != !_canonicalOnly) _canonicalOnly = cOnly;

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66)))
                    DoValidate(config);
            }

            EditorGUI.DrawRect(
                GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true), GUILayout.Height(1)),
                new Color(0,0,0,0.3f));

            float h = _canonicalOnly ? (CellSize * 6 + 8f) : GridH;
            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll,
                GUILayout.Height(Mathf.Min(h, GridH)));

            Rect gridOuter = GUILayoutUtility.GetRect(
                GridCols * CellSize, _canonicalOnly ? CellSize * 6 : CellSize * 16);
            EditorGUI.DrawRect(gridOuter, ColBackground);

            if (_canonicalOnly)
                DrawCanonicalGrid(config, gridOuter);
            else
                DrawAll256Grid(config, gridOuter);

            EditorGUILayout.EndScrollView();

            // Legend
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLegendDot(ColHasPrefab);  GUILayout.Label("Has Prefab", GUILayout.Width(74));
                DrawLegendDot(ColNoPrefab);   GUILayout.Label("Empty",      GUILayout.Width(50));
                DrawLegendDot(ColCanonical);  GUILayout.Label("Canonical",  GUILayout.Width(70));
                DrawLegendDot(ColSelected);   GUILayout.Label("Selected",   GUILayout.Width(60));
            }
        }

        void DrawAll256Grid(ArtMeshCaseConfig config, Rect outer)
        {
            for (int ci = 0; ci < 256; ci++)
            {
                int col = ci % GridCols, row = ci / GridCols;
                Rect r = new Rect(outer.x + col * CellSize, outer.y + row * CellSize,
                                  CellSize - 1, CellSize - 1);
                DrawCell(config, ci, r);
            }
        }

        void DrawCanonicalGrid(ArtMeshCaseConfig config, Rect outer)
        {
            var canonicals = GetCanonicals(config);
            for (int n = 0; n < canonicals.Count; n++)
            {
                int col = n % GridCols, row = n / GridCols;
                Rect r = new Rect(outer.x + col * CellSize, outer.y + row * CellSize,
                                  CellSize - 1, CellSize - 1);
                DrawCell(config, canonicals[n], r);
            }
        }

        void DrawCell(ArtMeshCaseConfig config, int ci, Rect r)
        {
            bool isCanon  = config.IsCanonical(ci);
            bool hasPrefab = config.GetEntry(config.GetCanonicalIndex(ci))?.prefab != null;
            bool isSel    = ci == _selected;

            Color bg = isSel ? ColSelected
                     : hasPrefab ? ColHasPrefab
                     : isCanon   ? ColCanonical
                     : ColNoPrefab;

            EditorGUI.DrawRect(r, bg);

            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize  = 7,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(r, ci.ToString(), labelStyle);

            // Click
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                _selected = (_selected == ci) ? -1 : ci;
                Event.current.Use();
                Repaint();
            }
        }

        static void DrawLegendDot(Color c)
        {
            Rect r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
            r.y += 3;
            EditorGUI.DrawRect(r, c);
        }

        void DoValidate(ArtMeshCaseConfig config)
        {
            int filled = 0;
            var canonicals = GetCanonicals(config);
            foreach (int ci in canonicals)
                if (config.GetEntry(ci)?.prefab != null) filled++;
            EditorUtility.DisplayDialog("Validate",
                $"Canonical cases: {canonicals.Count}\nWith prefab: {filled}\nMissing: {canonicals.Count - filled}",
                "OK");
        }

        List<int> GetCanonicals(ArtMeshCaseConfig config)
        {
            var list = new List<int>();
            for (int ci = 1; ci < 255; ci++)
                if (config.IsCanonical(ci)) list.Add(ci);
            return list;
        }

        // ════════════════════════════════════════════════════════════════════
        // Detail
        // ════════════════════════════════════════════════════════════════════

        void DrawDetail(ArtMeshCaseConfig config, int ci)
        {
            EditorGUI.DrawRect(
                GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true), GUILayout.Height(1)),
                new Color(0,0,0,0.3f));
            EditorGUILayout.Space(4);

            int canonical = config.GetCanonicalIndex(ci);
            var rot  = config.GetRotation(ci);
            bool flip = config.GetFlipped(ci);
            var euler = rot.eulerAngles;

            EditorGUILayout.LabelField($"Case {ci}", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(160)))
                {
                    EditorGUILayout.LabelField($"Canonical: {canonical}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        $"Rotation Y: {euler.y:F0}°", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        $"Flip: {(flip ? "Yes" : "No")}", EditorStyles.miniLabel);

                    EditorGUILayout.Space(4);

                    // Prefab field on the CANONICAL entry
                    var entry = config.GetEntry(canonical);
                    if (entry != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        var newPrefab = (GameObject)EditorGUILayout.ObjectField(
                            "Prefab", entry.prefab, typeof(GameObject), false,
                            GUILayout.Width(156));
                        if (EditorGUI.EndChangeCheck())
                        {
                            entry.prefab = newPrefab;
                            EditorUtility.SetDirty(config);
                        }
                        bool newOvr = EditorGUILayout.Toggle("Override", entry.isManualOverride);
                        if (newOvr != entry.isManualOverride)
                        {
                            entry.isManualOverride = newOvr;
                            EditorUtility.SetDirty(config);
                        }
                        if (GUILayout.Button("Clear", GUILayout.Width(60)))
                        {
                            entry.prefab = null;
                            EditorUtility.SetDirty(config);
                        }
                    }
                }

                // Thumbnail + topology
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawTopology(ci, 80f);

                    var prefab = config.GetEntry(canonical)?.prefab;
                    if (prefab != null)
                    {
                        var tex = AssetPreview.GetAssetPreview(prefab);
                        if (tex != null)
                            GUILayout.Label(tex, GUILayout.Width(80), GUILayout.Height(80));
                    }
                }
            }
        }

        // ── Vertex topology diagram ──────────────────────────────────────────

        void DrawTopology(int cubeIndex, float size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size,
                GUILayout.Width(size), GUILayout.Height(size));
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f));

            // Simple isometric projection of the 8 vertices
            // Blender: V0(0,0,1) V1(1,0,1) V2(1,0,0) V3(0,0,0)
            //          V4(0,1,1) V5(1,1,1) V6(1,1,0) V7(0,1,0)
            float pad = size * 0.12f;
            float w   = size - pad * 2;

            // Isometric offset: x→right, y→up, z→right+down
            Vector2 Proj(float x, float y, float z)
            {
                float px = r.x + pad + (x + z * 0.45f) * w * 0.55f;
                float py = r.yMax - pad - (y * 0.65f + (1f - z) * 0.25f) * w * 0.7f;
                return new Vector2(px, py);
            }

            Vector2[] pts = new Vector2[8];
            var verts = CubeTable.Vertices;
            for (int i = 0; i < 8; i++)
            {
                var v = verts[i];
                pts[i] = Proj(v.x, v.y, v.z);
            }

            // Edges
            int[,] edges =
            {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7}
            };
            for (int e = 0; e < 12; e++)
                DrawLine(pts[edges[e,0]], pts[edges[e,1]], new Color(0.5f, 0.5f, 0.5f));

            // Vertices
            float dotR = size * 0.065f;
            for (int i = 0; i < 8; i++)
            {
                bool active = (cubeIndex & (1 << i)) != 0;
                Color col = active ? new Color(1f, 0.25f, 0.25f) : new Color(0.4f, 0.4f, 0.4f);
                EditorGUI.DrawRect(
                    new Rect(pts[i].x - dotR, pts[i].y - dotR, dotR * 2, dotR * 2), col);
            }
        }

        static void DrawLine(Vector2 a, Vector2 b, Color col)
        {
            // Approximate a line with a thin rect
            Handles.color = col;
            Handles.DrawLine(new Vector3(a.x, a.y, 0), new Vector3(b.x, b.y, 0));
        }
    }
}

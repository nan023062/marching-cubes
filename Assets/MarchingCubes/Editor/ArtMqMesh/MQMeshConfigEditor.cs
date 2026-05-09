using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(MarchingSquares.MqMeshConfig))]
    public sealed class MQMeshConfigEditor : UnityEditor.Editor
    {
        // ── 地形 ──────────────────────────────────────────────────────────────
        private string   _fbxFolder    = "Assets/MarchingCubes/Sample/Resources/mq";
        private string   _prefabFolder = "Assets/MarchingCubes/Sample/Resources/mq/prefabs";
        private Material _terrainMat;
        private string   _terrainLog   = "";

        // ── 悬崖 ──────────────────────────────────────────────────────────────
        private string   _cliffFbxFolder    = "Assets/MarchingCubes/Sample/Resources/mq";
        private string   _cliffPrefabFolder = "Assets/MarchingCubes/Sample/Resources/mq/prefabs";
        private Material _cliffMat;
        private string   _cliffLog          = "";

        // ── Grid / Detail ─────────────────────────────────────────────────────
        private int     _selectedTerrain = -1;
        private int     _selectedCliff   = -1;
        private Vector2 _terrainScroll;
        private Vector2 _cliffScroll;

        private static readonly Color ColHas  = new Color(0.25f, 0.65f, 0.30f);
        private static readonly Color ColNone = new Color(0.28f, 0.28f, 0.28f);
        private static readonly Color ColSel  = new Color(0.95f, 0.70f, 0.10f);
        private static readonly Color ColBg   = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color ColCliffHas = new Color(0.30f, 0.50f, 0.80f);

        private const float CellSz = 36f;
        private const int   Cols   = 8;

        // ── Inspector ─────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var cfg = (MarchingSquares.MqMeshConfig)target;
            serializedObject.Update();

            // ── 一键全 Build ───────────────────────────────────────────────────
            if (GUILayout.Button("Build All  19 Terrain + 16 Cliff  Cases", GUILayout.Height(34)))
            {
                DoTerrainBuild(cfg);
                DoCliffBuild(cfg);
            }
            EditorGUILayout.Space(6);

            // ── 地形 ──────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("── 地形 Tile（mq_case_*.fbx）──", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "共 19 个 case：\n• 0-14：标准（四角高差 ≤ 1）\n• 15-18：对角高差 == 2 特殊 case",
                MessageType.Info);
            DrawFolderField("FBX 文件夹 (mq_case_N.fbx)", ref _fbxFolder);
            DrawFolderField("Prefab 输出文件夹", ref _prefabFolder);
            _terrainMat = (Material)EditorGUILayout.ObjectField("地形材质", _terrainMat, typeof(Material), false);
            EditorGUILayout.Space(2);
            if (GUILayout.Button("Build All 19 Terrain Prefabs", GUILayout.Height(28)))
                DoTerrainBuild(cfg);
            if (!string.IsNullOrEmpty(_terrainLog))
                EditorGUILayout.LabelField(_terrainLog, new GUIStyle(EditorStyles.helpBox) { wordWrap = true });

            EditorGUILayout.Space(4);
            DrawTerrainGrid(cfg);
            if (_selectedTerrain >= 0) DrawTerrainDetail(cfg, _selectedTerrain);

            // ── 悬崖 ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("── 悬崖 Tile（mq_cliff_*.fbx，D4 旋转）──", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "只需 5 个规范 FBX（mq_cliff_1/3/5/7/15.fbx），其余 10 个 prefab 由旋转派生。\n" +
                "Mesh 以格子中心为原点（XZ 中心在 0,0）。",
                MessageType.Info);
            DrawFolderField("悬崖 FBX 文件夹", ref _cliffFbxFolder);
            DrawFolderField("悬崖 Prefab 输出文件夹", ref _cliffPrefabFolder);
            _cliffMat = (Material)EditorGUILayout.ObjectField("悬崖材质", _cliffMat, typeof(Material), false);
            EditorGUILayout.Space(2);
            if (GUILayout.Button("Build All 15 Cliff Prefabs（D4）", GUILayout.Height(28)))
                DoCliffBuild(cfg);
            if (!string.IsNullOrEmpty(_cliffLog))
                EditorGUILayout.LabelField(_cliffLog, new GUIStyle(EditorStyles.helpBox) { wordWrap = true });

            EditorGUILayout.Space(4);
            DrawCliffGrid(cfg);
            if (_selectedCliff >= 0) DrawCliffDetail(cfg, _selectedCliff);

            serializedObject.ApplyModifiedProperties();
        }

        // ── 通用 ──────────────────────────────────────────────────────────────

        void DrawFolderField(string label, ref string path)
        {
            EditorGUILayout.LabelField(label);
            var current = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            EditorGUI.BeginChangeCheck();
            var dragged = (DefaultAsset)EditorGUILayout.ObjectField(current, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck() && dragged != null)
            {
                string p = AssetDatabase.GetAssetPath(dragged);
                if (AssetDatabase.IsValidFolder(p)) path = p;
            }
        }

        static void EnsureDir(string relPath)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath,
                relPath.StartsWith("Assets") ? relPath.Substring("Assets".Length).TrimStart('/', '\\') : relPath));
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }

        static void Dot(Color c)
        {
            Rect r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
            r.y += 3; EditorGUI.DrawRect(r, c);
        }

        // ── 地形 Build ────────────────────────────────────────────────────────

        void DoTerrainBuild(MarchingSquares.MqMeshConfig cfg)
        {
            _terrainLog = "";
            string relOut = _prefabFolder.TrimEnd('/', '\\');
            EnsureDir(relOut);
            AssetDatabase.Refresh();

            int ok = 0, skip = 0;
            int total = MarchingSquares.MqMeshConfig.TerrainCaseCount;

            for (int ci = 0; ci < total; ci++)
            {
                string fbxPath = $"{_fbxFolder.TrimEnd('/', '\\')}/mq_case_{ci}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null) { skip++; continue; }

                var root = new GameObject($"mq_case_{ci}");
                var dbg  = root.AddComponent<MarchingSquares.TilePrefab>();
                dbg.caseIndex = ci;

                var child = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale    = Vector3.one;

                if (_terrainMat != null)
                    foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
                        mr.sharedMaterial = _terrainMat;

                var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{relOut}/mq_case_{ci}.prefab");
                Object.DestroyImmediate(root);
                cfg.SetPrefab(ci, saved);
                ok++;

                if (ci % 4 == 0)
                    EditorUtility.DisplayProgressBar("Building Terrain Prefabs", $"mq_case_{ci}", ci / (float)(total - 1));
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _terrainLog = $"✓ Built {ok} terrain prefabs  (skipped {skip})";
            Repaint();
        }

        // ── 悬崖 Build（D4 旋转）──────────────────────────────────────────────

        void DoCliffBuild(MarchingSquares.MqMeshConfig cfg)
        {
            _cliffLog = "";
            string relOut = _cliffPrefabFolder.TrimEnd('/', '\\');
            EnsureDir(relOut);
            AssetDatabase.Refresh();

            int ok = 0, skip = 0;

            for (int ci = 1; ci < MarchingSquares.MqMeshConfig.CliffCaseCount; ci++)
            {
                var (canonical, rotCount) = MarchingSquares.TileTable.CliffD4Map[ci];
                string fbxPath = $"{_cliffFbxFolder.TrimEnd('/', '\\')}/mq_cliff_{canonical}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null) { skip++; continue; }

                var root  = new GameObject($"mq_cliff_{ci}");
                var dbg   = root.AddComponent<MarchingSquares.TilePrefab>();
                dbg.tileType  = MarchingSquares.TileType.Cliff;
                dbg.caseIndex = ci;

                var child = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.Euler(0, 90f * rotCount, 0);
                child.transform.localScale    = Vector3.one;

                if (_cliffMat != null)
                    foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
                        mr.sharedMaterial = _cliffMat;

                var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{relOut}/mq_cliff_{ci}.prefab");
                Object.DestroyImmediate(root);
                cfg.SetCliffPrefab(ci, saved);
                ok++;

                if (ci % 4 == 0)
                    EditorUtility.DisplayProgressBar("Building Cliff Prefabs", $"mq_cliff_{ci}", ci / 14f);
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _cliffLog = $"✓ Built {ok} cliff prefabs  (skipped {skip})";
            Repaint();
        }

        // ── 地形 Grid / Detail ────────────────────────────────────────────────

        void DrawTerrainGrid(MarchingSquares.MqMeshConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Terrain Cases (0–18)", EditorStyles.boldLabel, GUILayout.Width(160));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66)))
                {
                    int total = MarchingSquares.MqMeshConfig.TerrainCaseCount;
                    int filled = 0;
                    for (int ci = 0; ci < total; ci++) if (cfg.GetPrefab(ci) != null) filled++;
                    EditorUtility.DisplayDialog("Terrain Validate", $"Cases 0–{total-1}: {total}\n有: {filled}  缺: {total-filled}", "OK");
                }
            }
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0,0,0,0.3f));

            int cnt = MarchingSquares.MqMeshConfig.TerrainCaseCount;
            float h = Mathf.Ceil((float)cnt / Cols) * CellSz;
            _terrainScroll = EditorGUILayout.BeginScrollView(_terrainScroll, GUILayout.Height(h + 4));
            Rect outer = GUILayoutUtility.GetRect(Cols * CellSz, h);
            EditorGUI.DrawRect(outer, ColBg);
            for (int ci = 0; ci < cnt; ci++)
            {
                int col = ci % Cols, row = ci / Cols;
                Rect r = new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1);
                bool has = cfg.GetPrefab(ci) != null, sel = ci == _selectedTerrain;
                EditorGUI.DrawRect(r, sel ? ColSel : has ? ColHas : ColNone);
                DrawMiniQuad(ci, r);
                GUI.Label(new Rect(r.x, r.yMax - 11, r.width, 11), ci.ToString(),
                    new GUIStyle(EditorStyles.miniLabel) { fontSize = 7, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                { _selectedTerrain = (sel ? -1 : ci); Event.current.Use(); Repaint(); }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                Dot(ColHas);  GUILayout.Label("有 Prefab", GUILayout.Width(58));
                Dot(ColNone); GUILayout.Label("空",        GUILayout.Width(30));
                Dot(ColSel);  GUILayout.Label("选中",      GUILayout.Width(40));
            }
        }

        void DrawTerrainDetail(MarchingSquares.MqMeshConfig cfg, int ci)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0,0,0,0.3f));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Terrain Case {ci}  ({System.Convert.ToString(ci, 2).PadLeft(4, '0')})", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var cur = cfg.GetPrefab(ci);
            var nxt = (GameObject)EditorGUILayout.ObjectField("Prefab", cur, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { cfg.SetPrefab(ci, nxt); EditorUtility.SetDirty(cfg); }
            DrawPreview(cur);
        }

        // ── 悬崖 Grid / Detail ────────────────────────────────────────────────

        void DrawCliffGrid(MarchingSquares.MqMeshConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cliff Cases (0–15)", EditorStyles.boldLabel, GUILayout.Width(160));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66)))
                {
                    int filled = 0;
                    for (int ci = 1; ci < 16; ci++) if (cfg.GetCliffPrefab(ci) != null) filled++;
                    EditorUtility.DisplayDialog("Cliff Validate", $"Cases 1–15: 15\n有: {filled}  缺: {15 - filled}", "OK");
                }
            }
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0,0,0,0.3f));

            float h = Mathf.Ceil(16f / Cols) * CellSz;
            _cliffScroll = EditorGUILayout.BeginScrollView(_cliffScroll, GUILayout.Height(h + 4));
            Rect outer = GUILayoutUtility.GetRect(Cols * CellSz, h);
            EditorGUI.DrawRect(outer, ColBg);
            for (int ci = 0; ci < 16; ci++)
            {
                int col = ci % Cols, row = ci / Cols;
                Rect r = new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1);
                bool has = ci > 0 && cfg.GetCliffPrefab(ci) != null, sel = ci == _selectedCliff;
                EditorGUI.DrawRect(r, sel ? ColSel : has ? ColCliffHas : ColNone);
                DrawMiniCliff(ci, r);
                GUI.Label(new Rect(r.x, r.yMax - 11, r.width, 11), ci.ToString(),
                    new GUIStyle(EditorStyles.miniLabel) { fontSize = 7, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                { _selectedCliff = (sel ? -1 : ci); Event.current.Use(); Repaint(); }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                Dot(ColCliffHas); GUILayout.Label("有 Prefab", GUILayout.Width(58));
                Dot(ColNone);     GUILayout.Label("空",        GUILayout.Width(30));
                Dot(ColSel);      GUILayout.Label("选中",      GUILayout.Width(40));
            }
        }

        void DrawCliffDetail(MarchingSquares.MqMeshConfig cfg, int ci)
        {
            if (ci == 0) return;
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0,0,0,0.3f));
            EditorGUILayout.Space(4);
            var (canon, rot) = MarchingSquares.TileTable.CliffD4Map[ci];
            EditorGUILayout.LabelField($"Cliff Case {ci}  ({System.Convert.ToString(ci, 2).PadLeft(4, '0')})  ← mq_cliff_{canon}.fbx × {rot}×90°", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var cur = cfg.GetCliffPrefab(ci);
            var nxt = (GameObject)EditorGUILayout.ObjectField("Prefab", cur, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { cfg.SetCliffPrefab(ci, nxt); EditorUtility.SetDirty(cfg); }
            DrawPreview(cur);
        }

        // ── Mini 图标绘制 ─────────────────────────────────────────────────────

        void DrawMiniQuad(int ci, Rect r)
        {
            float pad = r.width * 0.12f, w = r.width - pad * 2, h = r.height - pad * 2 - 12f;
            if (h < 4) return;
            float dotR = w * 0.15f;
            var corners = new[]
            {
                new Vector2(r.x + pad,     r.y + pad + h),
                new Vector2(r.x + pad + w, r.y + pad + h),
                new Vector2(r.x + pad + w, r.y + pad),
                new Vector2(r.x + pad,     r.y + pad),
            };
            for (int i = 0; i < 4; i++)
            {
                bool high = (ci & (1 << i)) != 0;
                EditorGUI.DrawRect(new Rect(corners[i].x - dotR, corners[i].y - dotR, dotR * 2, dotR * 2),
                    high ? new Color(1f, 0.4f, 0.1f) : new Color(0.4f, 0.4f, 0.4f));
            }
        }

        void DrawMiniCliff(int ci, Rect r)
        {
            float pad = r.width * 0.15f, w = r.width - pad * 2, h = r.height - pad * 2 - 12f;
            if (h < 4) return;
            float thick = 2f;
            // E0=南(bottom), E1=东(right), E2=北(top), E3=西(left)
            if ((ci & 1) != 0) EditorGUI.DrawRect(new Rect(r.x + pad,         r.y + pad + h - thick, w, thick),         Color.cyan);
            if ((ci & 2) != 0) EditorGUI.DrawRect(new Rect(r.x + pad + w - thick, r.y + pad,         thick, h),         Color.cyan);
            if ((ci & 4) != 0) EditorGUI.DrawRect(new Rect(r.x + pad,         r.y + pad,             w, thick),         Color.cyan);
            if ((ci & 8) != 0) EditorGUI.DrawRect(new Rect(r.x + pad,         r.y + pad,             thick, h),         Color.cyan);
        }

        void DrawPreview(GameObject cur)
        {
            if (cur == null) return;
            Rect pr = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80), GUILayout.Height(80));
            var tex = AssetPreview.GetAssetPreview(cur);
            if (Event.current.type == EventType.Repaint)
            {
                if (tex != null) GUI.DrawTexture(pr, tex, ScaleMode.ScaleToFit);
                else             EditorGUI.DrawRect(pr, new Color(0.15f, 0.15f, 0.15f));
            }
            if (tex == null) Repaint();
        }
    }
}

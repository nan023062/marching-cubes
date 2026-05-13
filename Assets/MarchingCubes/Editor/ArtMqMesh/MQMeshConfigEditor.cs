using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(MarchingSquareTerrain.TileCaseConfig))]
    public sealed class MQMeshConfigEditor : UnityEditor.Editor
    {
        // ── 日志（每次 Build 后刷新，不需要持久化）──────────────────────────────
        private string _terrainLog = "";

        // ── Grid / Detail ─────────────────────────────────────────────────────
        private int     _selectedTerrain = -1;
        private Vector2 _terrainScroll;

        private static readonly Color ColHas  = new Color(0.25f, 0.65f, 0.30f);
        private static readonly Color ColNone = new Color(0.28f, 0.28f, 0.28f);
        private static readonly Color ColSel  = new Color(0.95f, 0.70f, 0.10f);
        private static readonly Color ColBg   = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color ColDead = new Color(0.10f, 0.10f, 0.10f);

        private const float CellSz = 36f;
        private const int   Cols   = 9;   // 9×9 grid（81 槽，65 实显 + 16 死槽灰显）

        // ── Inspector ─────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var cfg = (MarchingSquareTerrain.TileCaseConfig)target;
            serializedObject.Update();

            // ── 统一路径 / 材质 / Build ───────────────────────────────────────
            EditorGUILayout.LabelField("── 公共配置 ──", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "地形 mq_case_<N>.fbx，N ∈ [0,80] 中的 65 个 base-3 编码有效 case_idx。\n" +
                "case_idx = r0 + r1*3 + r2*9 + r3*27，r_i ∈ {0,1,2}，min(r) == 0。\n" +
                "16 个死槽（min(r) > 0）无对应 FBX，构建时自动跳过。",
                MessageType.Info);
            DrawFolderField("FBX 文件夹", cfg, ref cfg.editorFbxFolder);
            DrawFolderField("Prefab 输出文件夹", cfg, ref cfg.editorPrefabFolder);
            EditorGUI.BeginChangeCheck();
            var newTerrainMat = (Material)EditorGUILayout.ObjectField("地形材质", cfg.editorTerrainMat, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
            {
                cfg.editorTerrainMat = newTerrainMat;
                EditorUtility.SetDirty(cfg);
            }
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Build All 65 Terrain Cases", GUILayout.Height(34)))
            {
                DoTerrainBuild(cfg);
            }
            if (!string.IsNullOrEmpty(_terrainLog))
            {
                var style = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                EditorGUILayout.LabelField(_terrainLog, style);
            }

            // ── 地形 Grid ─────────────────────────────────────────────────────
            EditorGUILayout.Space(8);
            DrawTerrainGrid(cfg);
            if (_selectedTerrain >= 0) DrawTerrainDetail(cfg, _selectedTerrain);

            serializedObject.ApplyModifiedProperties();
        }

        // ── 通用 ──────────────────────────────────────────────────────────────

        void DrawFolderField(string label, Object dirtyTarget, ref string path)
        {
            EditorGUILayout.LabelField(label);
            var current = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            EditorGUI.BeginChangeCheck();
            var dragged = (DefaultAsset)EditorGUILayout.ObjectField(current, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck() && dragged != null)
            {
                string p = AssetDatabase.GetAssetPath(dragged);
                if (AssetDatabase.IsValidFolder(p)) { path = p; if (dirtyTarget != null) EditorUtility.SetDirty(dirtyTarget); }
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

        void DoTerrainBuild(MarchingSquareTerrain.TileCaseConfig cfg)
        {
            _terrainLog = "";
            string relOut = cfg.editorPrefabFolder.TrimEnd('/', '\\');
            EnsureDir(relOut);
            AssetDatabase.Refresh();

            int ok = 0, skipDead = 0, skipMissing = 0;
            int total = MarchingSquareTerrain.TileCaseConfig.TerrainCaseCount;

            for (int ci = 0; ci < total; ci++)
            {
                if (!MarchingSquareTerrain.TileTable.IsValidCase(ci)) { skipDead++; continue; }

                string fbxPath = $"{cfg.editorFbxFolder.TrimEnd('/', '\\')}/mq_case_{ci}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null) { skipMissing++; continue; }

                var root = new GameObject($"mq_case_{ci}");
                var dbg  = root.AddComponent<MarchingSquareTerrain.TilePrefab>();
                dbg.caseIndex = ci;

                var child = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale    = Vector3.one;

                if (cfg.editorTerrainMat != null)
                    foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
                        mr.sharedMaterial = cfg.editorTerrainMat;

                var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{relOut}/mq_case_{ci}.prefab");
                Object.DestroyImmediate(root);
                cfg.SetPrefab(ci, saved);
                ok++;

                if (ci % 8 == 0)
                    EditorUtility.DisplayProgressBar("Building Terrain Prefabs", $"mq_case_{ci}", ci / (float)(total - 1));
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _terrainLog = $"✓ Built {ok} terrain prefabs  (dead {skipDead}, missing FBX {skipMissing})";
            Repaint();
        }

        // ── 地形 Grid / Detail ────────────────────────────────────────────────

        void DrawTerrainGrid(MarchingSquareTerrain.TileCaseConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Terrain Cases (0–80, 65 valid)", EditorStyles.boldLabel, GUILayout.Width(220));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66)))
                {
                    int total = MarchingSquareTerrain.TileCaseConfig.TerrainCaseCount;
                    int valid = 0, filled = 0;
                    for (int ci = 0; ci < total; ci++)
                    {
                        if (!MarchingSquareTerrain.TileTable.IsValidCase(ci)) continue;
                        valid++;
                        if (cfg.GetPrefab(ci) != null) filled++;
                    }
                    EditorUtility.DisplayDialog("Terrain Validate", $"Valid cases: {valid}\n有: {filled}  缺: {valid - filled}", "OK");
                }
            }
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0,0,0,0.3f));

            int cnt = MarchingSquareTerrain.TileCaseConfig.TerrainCaseCount;
            float h = Mathf.Ceil((float)cnt / Cols) * CellSz;
            _terrainScroll = EditorGUILayout.BeginScrollView(_terrainScroll, GUILayout.Height(h + 4));
            Rect outer = GUILayoutUtility.GetRect(Cols * CellSz, h);
            EditorGUI.DrawRect(outer, ColBg);
            for (int ci = 0; ci < cnt; ci++)
            {
                int col = ci % Cols, row = ci / Cols;
                Rect r = new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1);
                bool dead = !MarchingSquareTerrain.TileTable.IsValidCase(ci);
                bool has  = !dead && cfg.GetPrefab(ci) != null;
                bool sel  = ci == _selectedTerrain;
                Color cellColor = dead ? ColDead : (sel ? ColSel : (has ? ColHas : ColNone));
                EditorGUI.DrawRect(r, cellColor);
                if (!dead) DrawMiniQuad(ci, r);
                GUI.Label(new Rect(r.x, r.yMax - 11, r.width, 11), ci.ToString(),
                    new GUIStyle(EditorStyles.miniLabel) { fontSize = 7, alignment = TextAnchor.MiddleCenter, normal = { textColor = dead ? new Color(0.4f, 0.4f, 0.4f) : Color.white } });
                if (!dead && Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                { _selectedTerrain = (sel ? -1 : ci); Event.current.Use(); Repaint(); }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                Dot(ColHas);  GUILayout.Label("有 Prefab", GUILayout.Width(58));
                Dot(ColNone); GUILayout.Label("空",        GUILayout.Width(30));
                Dot(ColDead); GUILayout.Label("死槽",      GUILayout.Width(40));
                Dot(ColSel);  GUILayout.Label("选中",      GUILayout.Width(40));
            }
        }

        void DrawTerrainDetail(MarchingSquareTerrain.TileCaseConfig cfg, int ci)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0,0,0,0.3f));
            EditorGUILayout.Space(4);
            int r0 = ci % 3, r1 = (ci / 3) % 3, r2 = (ci / 9) % 3, r3 = (ci / 27) % 3;
            EditorGUILayout.LabelField($"Terrain Case {ci}  r=({r0},{r1},{r2},{r3})", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var cur = cfg.GetPrefab(ci);
            var nxt = (GameObject)EditorGUILayout.ObjectField("Prefab", cur, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { cfg.SetPrefab(ci, nxt); EditorUtility.SetDirty(cfg); }
            DrawPreview(cur);
        }

        // ── Mini 图标绘制 ─────────────────────────────────────────────────────

        // base-3 4 角高度小图：r_i ∈ {0,1,2} → 灰 / 橙 / 红
        void DrawMiniQuad(int ci, Rect r)
        {
            float pad = r.width * 0.12f, w = r.width - pad * 2, h = r.height - pad * 2 - 12f;
            if (h < 4) return;
            float dotR = w * 0.15f;
            var corners = new[]
            {
                new Vector2(r.x + pad,     r.y + pad + h),  // V0 BL
                new Vector2(r.x + pad + w, r.y + pad + h),  // V1 BR
                new Vector2(r.x + pad + w, r.y + pad),      // V2 TR
                new Vector2(r.x + pad,     r.y + pad),      // V3 TL
            };
            for (int i = 0; i < 4; i++)
            {
                int ri = (ci / (i == 0 ? 1 : i == 1 ? 3 : i == 2 ? 9 : 27)) % 3;
                Color c = ri == 0 ? new Color(0.4f, 0.4f, 0.4f)
                       : ri == 1 ? new Color(1f,   0.55f, 0.15f)
                       :           new Color(1f,   0.15f, 0.10f);
                EditorGUI.DrawRect(new Rect(corners[i].x - dotR, corners[i].y - dotR, dotR * 2, dotR * 2), c);
            }
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

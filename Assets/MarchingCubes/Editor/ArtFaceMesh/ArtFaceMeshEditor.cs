using System.IO;
using UnityEditor;
using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(MeCaseConfig))]
    public sealed class ArtFaceMeshEditor : UnityEditor.Editor
    {
        // ── State ────────────────────────────────────────────────────────────
        string  _fbxFolder    = "Assets/MarchingCubes/ArtFace/Cases";
        string  _prefabFolder = "Assets/MarchingCubes/ArtFace/Prefabs";
        string  _log          = "";
        int     _selected     = -1;
        Vector2 _gridScroll;

        // ── Colours ──────────────────────────────────────────────────────────
        static readonly Color ColHas  = new Color(0.25f, 0.65f, 0.30f);
        static readonly Color ColNone = new Color(0.28f, 0.28f, 0.28f);
        static readonly Color ColSel  = new Color(0.95f, 0.70f, 0.10f);
        static readonly Color ColBg   = new Color(0.18f, 0.18f, 0.18f);
        static readonly Color ColX    = new Color(0.90f, 0.30f, 0.30f);
        static readonly Color ColY    = new Color(0.30f, 0.80f, 0.40f);
        static readonly Color ColZ    = new Color(0.30f, 0.50f, 0.90f);

        const float CellSz = 20f;
        const int   Cols   = 33;

        // 12 个面槽的标签（与 FaceBuilder bit 顺序一致）
        static readonly string[] SlotLabels =
        {
            "+Y+Z", "-Y+Z", "-Y-Z", "+Y-Z",   // X 组 bits 0-3
            "+X+Z", "-X+Z", "-X-Z", "+X-Z",   // Y 组 bits 4-7
            "+X+Y", "-X+Y", "-X-Y", "+X-Y",   // Z 组 bits 8-11
        };

        // ════════════════════════════════════════════════════════════════════
        public override void OnInspectorGUI()
        {
            var cfg = (MeCaseConfig)target;
            serializedObject.Update();

            DrawBuildSection(cfg);
            EditorGUILayout.Space(6);
            DrawGrid(cfg);
            EditorGUILayout.Space(4);
            if (_selected >= 0) DrawDetail(cfg, _selected);

            serializedObject.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════════════
        // Build Prefabs
        // ════════════════════════════════════════════════════════════════════

        void DrawBuildSection(MeCaseConfig cfg)
        {
            EditorGUILayout.LabelField("Build Case Prefabs  (me_case_N.fbx → me_case_N.prefab)", EditorStyles.boldLabel);
            DrawFolderField("① ME Case FBX 文件夹", ref _fbxFolder);
            DrawFolderField("② 输出 Prefab 文件夹", ref _prefabFolder);

            EditorGUILayout.Space(2);
            if (GUILayout.Button($"Build All {MeCaseConfig.CaseCount} Case Prefabs", GUILayout.Height(30)))
                DoBuild(cfg);

            if (!string.IsNullOrEmpty(_log))
                EditorGUILayout.LabelField(_log,
                    new GUIStyle(EditorStyles.helpBox) { wordWrap = true });
        }

        void DrawFolderField(string label, ref string path)
        {
            EditorGUILayout.LabelField(label);
            var cur = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            EditorGUI.BeginChangeCheck();
            var dragged = (DefaultAsset)EditorGUILayout.ObjectField(cur, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck() && dragged != null)
            {
                string p = AssetDatabase.GetAssetPath(dragged);
                if (AssetDatabase.IsValidFolder(p)) path = p;
            }
        }

        void DoBuild(MeCaseConfig cfg)
        {
            _log = "";
            FaceBuilder.InitLookup();

            string relOut  = _prefabFolder.TrimEnd('/', '\\');
            string fullOut = Path.GetFullPath(Path.Combine(Application.dataPath,
                relOut.StartsWith("Assets") ? relOut.Substring("Assets".Length).TrimStart('/', '\\') : relOut));
            if (!Directory.Exists(fullOut)) Directory.CreateDirectory(fullOut);
            AssetDatabase.Refresh();

            int ok = 0, skip = 0;
            int total = MeCaseConfig.CaseCount;

            for (int ci = 0; ci < total; ci++)
            {
                string fbxPath = $"{_fbxFolder.TrimEnd('/', '\\')}/me_case_{ci}.fbx";
                var fbxAsset   = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbxAsset == null) { skip++; continue; }

                var root  = new GameObject($"me_case_{ci}");
                var child = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, root.transform);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one;

                string prefabPath = $"{relOut}/me_case_{ci}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);

                cfg.SetPrefab(ci, saved);
                ok++;

                if (ci % 50 == 0)
                    EditorUtility.DisplayProgressBar("Building ME Prefabs",
                        $"me_case_{ci}", ci / (float)total);
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _log = $"✓ Built {ok} prefabs → {relOut}  (skipped {skip})";
            Repaint();
        }

        // ════════════════════════════════════════════════════════════════════
        // Grid (1044 canonical cases)
        // ════════════════════════════════════════════════════════════════════

        void DrawGrid(MeCaseConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Cases  ({MeCaseConfig.CaseCount} canonical)", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66))) DoValidate(cfg);
            }

            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));

            int   total = MeCaseConfig.CaseCount;
            int   rows  = Mathf.CeilToInt((float)total / Cols);
            float h     = rows * CellSz;

            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll,
                GUILayout.Height(Mathf.Min(h, CellSz * 16)));
            Rect outer = GUILayoutUtility.GetRect(Cols * CellSz, h);
            EditorGUI.DrawRect(outer, ColBg);

            for (int ci = 0; ci < total; ci++)
            {
                int col = ci % Cols, row = ci / Cols;
                DrawCell(cfg, ci,
                    new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1));
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                Dot(ColHas);  GUILayout.Label("Has Prefab", GUILayout.Width(74));
                Dot(ColNone); GUILayout.Label("Empty",      GUILayout.Width(50));
                Dot(ColSel);  GUILayout.Label("Selected",   GUILayout.Width(60));
            }
        }

        void DrawCell(MeCaseConfig cfg, int ci, Rect r)
        {
            bool has = cfg.GetPrefab(ci) != null;
            bool sel = ci == _selected;
            EditorGUI.DrawRect(r, sel ? ColSel : has ? ColHas : ColNone);
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

        void DoValidate(MeCaseConfig cfg)
        {
            int total = MeCaseConfig.CaseCount, filled = 0;
            for (int i = 0; i < total; i++)
                if (cfg.GetPrefab(i) != null) filled++;
            EditorUtility.DisplayDialog("Validate ME Case Config",
                $"总 Case 数：{total}\n已配置 prefab：{filled}\n缺失：{total - filled}", "OK");
        }

        // ════════════════════════════════════════════════════════════════════
        // Detail
        // ════════════════════════════════════════════════════════════════════

        void DrawDetail(MeCaseConfig cfg, int ci)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Case  {ci}", EditorStyles.boldLabel);

            int mask = FaceBuilder.GetCanonicalMask(ci);
            EditorGUILayout.LabelField(
                $"12-bit mask: 0x{mask:X3}  ({mask})   " +
                $"X:{mask & 0xF:04b}  Y:{(mask >> 4) & 0xF:04b}  Z:{(mask >> 8) & 0xF:04b}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            DrawMaskVisual(mask);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            var cur = cfg.GetPrefab(ci);
            var nxt = (GameObject)EditorGUILayout.ObjectField("Prefab", cur, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { cfg.SetPrefab(ci, nxt); EditorUtility.SetDirty(cfg); }

            if (cur != null)
            {
                var tex = AssetPreview.GetAssetPreview(cur);
                if (tex != null) GUILayout.Label(tex, GUILayout.Width(80), GUILayout.Height(80));
            }
        }

        // 12 个面槽 bit 可视化：3 组（X/Y/Z）各 2×2 方格
        void DrawMaskVisual(int mask)
        {
            string[] groupNames  = { "X 面 (YZ 平面)", "Y 面 (XZ 平面)", "Z 面 (XY 平面)" };
            Color[]  groupColors = { ColX, ColY, ColZ };

            const float cellW = 52f, cellH = 18f, gap = 8f;

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int g = 0; g < 3; g++)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(cellW * 2 + gap)))
                    {
                        EditorGUILayout.LabelField(groupNames[g], EditorStyles.miniLabel,
                            GUILayout.Width(cellW * 2 + gap));

                        for (int row = 0; row < 2; row++)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            for (int col = 0; col < 2; col++)
                            {
                                int  bit    = g * 4 + row * 2 + col;
                                bool active = ((mask >> bit) & 1) != 0;
                                Color c = active ? groupColors[g] : new Color(0.22f, 0.22f, 0.22f);
                                Rect r = GUILayoutUtility.GetRect(cellW, cellH);
                                EditorGUI.DrawRect(r, c);
                                GUI.Label(r, SlotLabels[bit],
                                    new GUIStyle(EditorStyles.miniLabel)
                                        { fontSize = 7, alignment = TextAnchor.MiddleCenter,
                                          normal = { textColor = Color.white } });
                            }
                        }
                    }
                    if (g < 2) GUILayout.Space(gap);
                }
            }
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(MarchingSquares.MqMeshConfig))]
    public sealed class MQMeshConfigEditor : UnityEditor.Editor
    {
        private string   _fbxFolder    = "Assets/MarchingCubes/Sample/Resources/mq";
        private string   _prefabFolder = "Assets/MarchingCubes/Sample/Resources/mq/prefabs";
        private Material _material;
        private string   _log          = "";
        private int      _selected     = -1;
        private Vector2  _gridScroll;

        private static readonly Color ColHas = new Color(0.25f, 0.65f, 0.30f);
        private static readonly Color ColNone= new Color(0.28f, 0.28f, 0.28f);
        private static readonly Color ColSel = new Color(0.95f, 0.70f, 0.10f);
        private static readonly Color ColBg  = new Color(0.18f, 0.18f, 0.18f);

        private const float CellSz = 36f;
        private const int   Cols   = 8;

        // ── Inspector ────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var cfg = (MarchingSquares.MqMeshConfig)target;
            serializedObject.Update();

            DrawBuildSection(cfg);
            EditorGUILayout.Space(6);
            DrawGrid(cfg);
            EditorGUILayout.Space(4);
            if (_selected >= 0) DrawDetail(cfg, _selected);

            serializedObject.ApplyModifiedProperties();
        }

        // ── Build ─────────────────────────────────────────────────────────────

        void DrawBuildSection(MarchingSquares.MqMeshConfig cfg)
        {
            EditorGUILayout.LabelField("Build MQ Case Prefabs (mq_case_*.fbx)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "需要 16 个独立 FBX（mq_case_0.fbx … mq_case_15.fbx）。\n" +
                "每个 case 有独立 UV，满足 Mesh 几何 + 纹理双重组合要求。\n" +
                "Case 0 与 case 15 均为平 quad，可共用同一 FBX。",
                MessageType.Info);

            DrawFolderField("① FBX 文件夹 (mq_case_N.fbx)", ref _fbxFolder);
            DrawFolderField("② Prefab 输出文件夹", ref _prefabFolder);
            _material = (Material)EditorGUILayout.ObjectField(
                "③ 地形材质", _material, typeof(Material), false);

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Build All 16 Case Prefabs", GUILayout.Height(30)))
                DoBuild(cfg);

            if (!string.IsNullOrEmpty(_log))
                EditorGUILayout.LabelField(_log, new GUIStyle(EditorStyles.helpBox) { wordWrap = true });
        }

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

        void DoBuild(MarchingSquares.MqMeshConfig cfg)
        {
            _log = "";

            string relOut  = _prefabFolder.TrimEnd('/', '\\');
            string fullOut = Path.GetFullPath(Path.Combine(Application.dataPath,
                relOut.StartsWith("Assets") ? relOut.Substring("Assets".Length).TrimStart('/', '\\') : relOut));
            if (!Directory.Exists(fullOut)) Directory.CreateDirectory(fullOut);
            AssetDatabase.Refresh();

            int ok = 0, skip = 0;

            for (int ci = 0; ci < 16; ci++)
            {
                // Case 15（全高平 quad）复用 case 0 的 FBX
                int fbxCase  = (ci == 15) ? 0 : ci;
                string fbxPath = $"{_fbxFolder.TrimEnd('/', '\\')}/mq_case_{fbxCase}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null) { skip++; continue; }

                // 直接实例化，不做任何旋转/翻转
                var root  = new GameObject($"mq_case_{ci}");
                var dbg   = root.AddComponent<MarchingSquares.MqTilePrefab>();
                dbg.caseIndex = ci;

                var child = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale    = Vector3.one;

                if (_material != null)
                    foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
                        mr.sharedMaterial = _material;

                string prefabPath = $"{relOut}/mq_case_{ci}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);

                cfg.SetPrefab(ci, saved);
                ok++;

                if (ci % 4 == 0)
                    EditorUtility.DisplayProgressBar("Building MQ Prefabs", $"mq_case_{ci}", ci / 15f);
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _log = $"✓ Built {ok} prefabs → {relOut}  (skipped {skip})";
            Repaint();
        }

        // ── Grid ──────────────────────────────────────────────────────────────

        void DrawGrid(MarchingSquares.MqMeshConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cases (0–15)", EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66))) DoValidate(cfg);
            }

            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));

            float h = Mathf.Ceil(16f / Cols) * CellSz;
            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(h + 4));
            Rect outer = GUILayoutUtility.GetRect(Cols * CellSz, h);
            EditorGUI.DrawRect(outer, ColBg);

            for (int ci = 0; ci < 16; ci++)
            {
                int col = ci % Cols, row = ci / Cols;
                DrawCell(cfg, ci, new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1));
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                Dot(ColHas);  GUILayout.Label("有 Prefab", GUILayout.Width(58));
                Dot(ColNone); GUILayout.Label("空",        GUILayout.Width(30));
                Dot(ColSel);  GUILayout.Label("选中",      GUILayout.Width(40));
            }
        }

        void DrawCell(MarchingSquares.MqMeshConfig cfg, int ci, Rect r)
        {
            bool hasPrefab = cfg.GetPrefab(ci) != null;
            bool isSel     = ci == _selected;
            EditorGUI.DrawRect(r, isSel ? ColSel : hasPrefab ? ColHas : ColNone);
            DrawMiniQuad(ci, r);
            GUI.Label(new Rect(r.x, r.yMax - 11, r.width, 11), ci.ToString(),
                new GUIStyle(EditorStyles.miniLabel) { fontSize = 7, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white } });
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                _selected = (_selected == ci) ? -1 : ci;
                Event.current.Use(); Repaint();
            }
        }

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

        static void Dot(Color c)
        {
            Rect r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
            r.y += 3; EditorGUI.DrawRect(r, c);
        }

        void DoValidate(MarchingSquares.MqMeshConfig cfg)
        {
            int filled = 0;
            for (int ci = 0; ci < 16; ci++) if (cfg.GetPrefab(ci) != null) filled++;
            EditorUtility.DisplayDialog("Validate",
                $"Cases 0–15: 16\n有 Prefab: {filled}\n缺失: {16 - filled}", "OK");
        }

        // ── Detail ────────────────────────────────────────────────────────────

        void DrawDetail(MarchingSquares.MqMeshConfig cfg, int ci)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Case {ci}  (binary: {System.Convert.ToString(ci, 2).PadLeft(4, '0')})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"V0(BL)={(((ci&1)>0)?'H':'L')}  V1(BR)={(((ci&2)>0)?'H':'L')}  V2(TR)={(((ci&4)>0)?'H':'L')}  V3(TL)={(((ci&8)>0)?'H':'L')}",
                EditorStyles.miniLabel);
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
    }
}

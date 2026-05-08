using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(MarchingSquares.MQMeshConfig))]
    public sealed class MQMeshConfigEditor : UnityEditor.Editor
    {
        private string  _fbxFolder    = "Assets/MarchingCubes/ArtMesh/MQ/Cases";
        private string  _prefabFolder = "Assets/MarchingCubes/ArtMesh/MQ/Prefabs";
        private string  _log          = "";
        private int     _selected     = -1;
        private Vector2 _gridScroll;
        private bool    _canonicalOnly;

        private static readonly Color ColHas  = new Color(0.25f, 0.65f, 0.30f);
        private static readonly Color ColNone = new Color(0.28f, 0.28f, 0.28f);
        private static readonly Color ColSel  = new Color(0.95f, 0.70f, 0.10f);
        private static readonly Color ColCan  = new Color(0.35f, 0.55f, 0.85f);
        private static readonly Color ColBg   = new Color(0.18f, 0.18f, 0.18f);

        private const float CellSz = 36f;
        private const int   Cols   = 8;

        // Canonical cases for MQ (D4 reduced from 16): 0,1,3,5,7
        // Case 15（全高）与 case 0（全低）几何相同（均为平 quad），复用 mq_case_0.fbx
        private static readonly int[] CanonicalCases = { 0, 1, 3, 5, 7 };

        // ── Inspector ────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var cfg = (MarchingSquares.MQMeshConfig)target;
            serializedObject.Update();

            DrawBuildSection(cfg);
            EditorGUILayout.Space(6);
            DrawGrid(cfg);
            EditorGUILayout.Space(4);
            if (_selected >= 0) DrawDetail(cfg, _selected);

            serializedObject.ApplyModifiedProperties();
        }

        // ── Build ─────────────────────────────────────────────────────────────

        void DrawBuildSection(MarchingSquares.MQMeshConfig cfg)
        {
            EditorGUILayout.LabelField("Build MQ Case Prefabs (mq_case_*.fbx)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Canonical cases: 0, 1, 3, 5, 7  →  5 FBX files required.\n" +
                "Case 15（全高）= Case 0（全低），几何相同，自动复用 mq_case_0.fbx。\n" +
                "Remaining 11 cases auto-generated via D4 rotation/flip.",
                MessageType.Info);

            DrawFolderField("① Canonical FBX folder (mq_case_N.fbx)", ref _fbxFolder);
            DrawFolderField("② Output prefab folder", ref _prefabFolder);

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

        void DoBuild(MarchingSquares.MQMeshConfig cfg)
        {
            _log = "";
            cfg.EnsureSymmetry();

            string relOut  = _prefabFolder.TrimEnd('/', '\\');
            string fullOut = Path.GetFullPath(Path.Combine(Application.dataPath,
                relOut.StartsWith("Assets") ? relOut.Substring("Assets".Length).TrimStart('/', '\\') : relOut));
            if (!Directory.Exists(fullOut)) Directory.CreateDirectory(fullOut);
            AssetDatabase.Refresh();

            int ok = 0, skip = 0;

            for (int ci = 0; ci < 16; ci++)
            {
                int      canonical = cfg.GetCanonicalIndex(ci);
                Quaternion d4      = cfg.GetRotation(ci);
                bool     isFlipped = cfg.GetFlipped(ci);

                string fbxPath = $"{_fbxFolder.TrimEnd('/', '\\')}/mq_case_{canonical}.fbx";
                var canonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (canonPrefab == null) { skip++; continue; }

                var root = new GameObject($"mq_case_{ci}");

                // Pivot center of quad in XZ = (0.5, 0, 0.5)
                var center   = new Vector3(0.5f, 0f, 0.5f);
                var d4apply  = isFlipped ? d4 : Quaternion.Inverse(d4);

                var child = (GameObject)PrefabUtility.InstantiatePrefab(canonPrefab, root.transform);
                child.transform.localRotation = d4apply;
                child.transform.localPosition = center - d4apply * center;
                child.transform.localScale    = isFlipped ? new Vector3(-1f, 1f, 1f) : Vector3.one;

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

        void DrawGrid(MarchingSquares.MQMeshConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cases", EditorStyles.boldLabel, GUILayout.Width(50));
                bool cOnly = GUILayout.Toggle(_canonicalOnly, "Canonical", EditorStyles.miniButtonLeft, GUILayout.Width(72));
                bool all   = GUILayout.Toggle(!_canonicalOnly, "All 16",   EditorStyles.miniButtonRight, GUILayout.Width(56));
                if (cOnly != _canonicalOnly || !all != !_canonicalOnly) _canonicalOnly = cOnly;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", GUILayout.Width(66))) DoValidate(cfg);
            }

            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));

            int count = _canonicalOnly ? CanonicalCases.Length : 16;
            float h = Mathf.Ceil((float)count / Cols) * CellSz;
            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(Mathf.Min(h + 4, CellSz * 4)));
            Rect outer = GUILayoutUtility.GetRect(Cols * CellSz, h);
            EditorGUI.DrawRect(outer, ColBg);

            if (_canonicalOnly)
            {
                for (int n = 0; n < CanonicalCases.Length; n++)
                {
                    int ci = CanonicalCases[n];
                    int col = n % Cols, row = n / Cols;
                    DrawCell(cfg, ci, new Rect(outer.x + col * CellSz, outer.y + row * CellSz, CellSz - 1, CellSz - 1));
                }
            }
            else
            {
                for (int ci = 0; ci < 16; ci++)
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

        void DrawCell(MarchingSquares.MQMeshConfig cfg, int ci, Rect r)
        {
            bool hasPrefab = cfg.GetPrefab(ci) != null;
            bool isCanon   = cfg.IsCanonical(ci);
            bool isSel     = ci == _selected;

            Color bg = isSel ? ColSel : hasPrefab ? ColHas : isCanon ? ColCan : ColNone;
            EditorGUI.DrawRect(r, bg);

            // Mini quad visualization (2×2 dots showing which corners are high)
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
            float pad = r.width * 0.12f;
            float w   = r.width - pad * 2;
            float h   = r.height - pad * 2 - 12f;
            if (h < 4) return;

            float dotR = w * 0.15f;
            // V0=BL, V1=BR, V2=TR, V3=TL
            var corners = new[]
            {
                new Vector2(r.x + pad,          r.y + pad + h),    // V0 BL
                new Vector2(r.x + pad + w,      r.y + pad + h),    // V1 BR
                new Vector2(r.x + pad + w,      r.y + pad),        // V2 TR
                new Vector2(r.x + pad,          r.y + pad),        // V3 TL
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

        void DoValidate(MarchingSquares.MQMeshConfig cfg)
        {
            int filled = 0;
            for (int ci = 0; ci < 16; ci++) if (cfg.GetPrefab(ci) != null) filled++;
            EditorUtility.DisplayDialog("Validate",
                $"Cases 0–15: 16\nWith prefab: {filled}\nMissing: {16 - filled}", "OK");
        }

        // ── Detail ────────────────────────────────────────────────────────────

        void DrawDetail(MarchingSquares.MQMeshConfig cfg, int ci)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField($"Case {ci}  (binary: {System.Convert.ToString(ci, 2).PadLeft(4, '0')})", EditorStyles.boldLabel);
            cfg.EnsureSymmetry();
            EditorGUILayout.LabelField($"Canonical: {cfg.GetCanonicalIndex(ci)}   D4 rotY: {cfg.GetRotation(ci).eulerAngles.y:F0}°   Flip: {cfg.GetFlipped(ci)}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Corners: V0(BL)={((ci&1)>0?'H':'L')}  V1(BR)={((ci&2)>0?'H':'L')}  V2(TR)={((ci&4)>0?'H':'L')}  V3(TL)={((ci&8)>0?'H':'L')}", EditorStyles.miniLabel);
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

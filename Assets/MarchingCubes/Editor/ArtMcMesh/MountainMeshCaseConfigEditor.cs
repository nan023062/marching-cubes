using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    [CustomEditor(typeof(MountainMeshCaseConfig))]
    public sealed class MountainMeshCaseConfigEditor : UnityEditor.Editor
    {
        // ── State ────────────────────────────────────────────────────────────
        // 路径/材质/bias 序列化在 MountainMeshCaseConfig 本体，无需 Editor 实例变量
        private string   _log      = "";
        private int      _selected = -1;
        private Vector2  _gridScroll;

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
            var cfg = (MountainMeshCaseConfig)target;
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

        void DrawBuildSection(MountainMeshCaseConfig cfg)
        {
            EditorGUILayout.LabelField("Build Mountain Case Prefabs", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            DrawFolderField("① Output mesh folder",   ref cfg.editorMeshFolder);
            DrawFolderField("② Output prefab folder", ref cfg.editorPrefabFolder);
            cfg.editorMaterial  = (Material)EditorGUILayout.ObjectField("③ Material",   cfg.editorMaterial,  typeof(Material), false);
            cfg.editorSolidBias = EditorGUILayout.Slider(
                new GUIContent("④ Solid Bias", "0=顶点贴实体角（最大实体）  0.5=中点  1=贴空白角（最小实体）"),
                cfg.editorSolidBias, 0f, 1f);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(cfg);

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Build All 254 Mountain Case Prefabs  (mountain_1 … mountain_254)", GUILayout.Height(30)))
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

        void DoBuild(MountainMeshCaseConfig cfg)
        {
            _log = "";
            EnsureFolder(cfg.editorMeshFolder);
            EnsureFolder(cfg.editorPrefabFolder);
            AssetDatabase.Refresh();

            int ok = 0, skip = 0;

            for (int ci = 1; ci <= 254; ci++)
            {
                var mesh = BuildMesh(ci, cfg.editorSolidBias);
                if (mesh == null) { skip++; continue; }

                // 保存 mesh asset（已存在则原地更新，保留既有 prefab 引用不断）
                string meshPath    = $"{cfg.editorMeshFolder}/mountain_{ci}.asset";
                var    existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existingMesh != null)
                {
                    EditorUtility.CopySerialized(mesh, existingMesh);
                    Object.DestroyImmediate(mesh);
                    mesh = existingMesh;
                }
                else
                {
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }

                // 创建 prefab
                var root = new GameObject($"mountain_{ci}");
                root.AddComponent<MeshFilter>().sharedMesh  = mesh;
                var mr = root.AddComponent<MeshRenderer>();
                if (cfg.editorMaterial != null) mr.sharedMaterial = cfg.editorMaterial;

                string prefabPath = $"{cfg.editorPrefabFolder}/mountain_{ci}.prefab";
                var    saved      = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);

                cfg.SetPrefab(ci, saved);
                ok++;

                if (ci % 20 == 0)
                    EditorUtility.DisplayProgressBar("Building Mountain Prefabs", $"mountain_{ci}", ci / 254f);
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _log = $"✓ Built {ok} prefabs → {cfg.editorPrefabFolder}  (skipped {skip})";
            Repaint();
        }

        // flat-shaded mesh：顶点偏向实体角（solidBias=0 贴角，0.5 中点，1 贴空白角）
        static Mesh BuildMesh(int caseIndex, float solidBias)
        {
            ref readonly int[] triTable = ref CubeTable.GetCubeKindTriangles(caseIndex);
            var verts   = new List<Vector3>();
            var indices = new List<int>();

            for (int i = 0; i + 2 < triTable.Length && triTable[i] >= 0; i += 3)
            {
                int e0 = triTable[i + 2], e1 = triTable[i + 1], e2 = triTable[i];
                int b = verts.Count;
                verts.Add(EdgeVertex(e0, caseIndex, solidBias));
                verts.Add(EdgeVertex(e1, caseIndex, solidBias));
                verts.Add(EdgeVertex(e2, caseIndex, solidBias));
                indices.Add(b); indices.Add(b + 1); indices.Add(b + 2);
            }

            if (verts.Count == 0) return null;

            var mesh = new Mesh { name = $"mountain_{caseIndex}" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // solidBias=0: 顶点贴实体角（坡面最靠实体侧）
        // solidBias=0.5: 边中点（标准 MC）
        // solidBias=1: 顶点贴空白角（坡面最靠空白侧，实体看起来最大）
        static Vector3 EdgeVertex(int edgeIndex, int caseIndex, float solidBias)
        {
            var (p1, p2) = CubeTable.Edges[edgeIndex];
            bool p1Active = (caseIndex & (1 << p1)) != 0;
            int  actIdx   = p1Active ? p1 : p2;
            int  inactIdx = p1Active ? p2 : p1;
            var  act      = CubeTable.Vertices[actIdx];
            var  inact    = CubeTable.Vertices[inactIdx];
            return new Vector3(
                act.x + solidBias * (inact.x - act.x),
                act.y + solidBias * (inact.y - act.y),
                act.z + solidBias * (inact.z - act.z));
        }

        static void EnsureFolder(string unityPath)
        {
            string full = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                unityPath.StartsWith("Assets/") ? unityPath.Substring("Assets/".Length) : unityPath));
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }

        // ════════════════════════════════════════════════════════════════════
        // Grid
        // ════════════════════════════════════════════════════════════════════

        void DrawGrid(MountainMeshCaseConfig cfg)
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

        void DrawCell(MountainMeshCaseConfig cfg, int ci, Rect r)
        {
            bool hasPrefab = cfg.GetPrefab(ci) != null;
            bool isSel     = ci == _selected;
            Color bg = isSel ? ColSel : hasPrefab ? ColHas : ColNone;

            EditorGUI.DrawRect(r, bg);
            GUI.Label(r, ci.ToString(),
                new GUIStyle(EditorStyles.miniLabel)
                    { fontSize = 7, alignment = TextAnchor.MiddleCenter,
                      normal   = { textColor  = Color.white } });

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                _selected = (_selected == ci) ? -1 : ci;
                Event.current.Use(); Repaint();
            }
        }

        static void Dot(Color c)
        {
            Rect r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
            r.y += 3;
            EditorGUI.DrawRect(r, c);
        }

        void DoValidate(MountainMeshCaseConfig cfg)
        {
            int filled = 0;
            for (int ci = 1; ci <= 254; ci++)
                if (cfg.GetPrefab(ci) != null) filled++;
            EditorUtility.DisplayDialog("Validate",
                $"Cases 1–254: 254\nWith prefab: {filled}\nMissing: {254 - filled}", "OK");
        }

        // ════════════════════════════════════════════════════════════════════
        // Detail
        // ════════════════════════════════════════════════════════════════════

        void DrawDetail(MountainMeshCaseConfig cfg, int ci)
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(1)), new Color(0, 0, 0, 0.3f));
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField($"Case {ci}", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var cur = cfg.GetPrefab(ci);
            var nxt = (GameObject)EditorGUILayout.ObjectField("Prefab", cur, typeof(GameObject), false);
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
                EditorGUI.DrawRect(
                    new Rect(pts[i].x - dotR, pts[i].y - dotR, dotR * 2, dotR * 2),
                    active ? new Color(1f, 0.25f, 0.25f) : new Color(0.4f, 0.4f, 0.4f));
            }
        }
    }
}

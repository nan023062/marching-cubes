using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// 在 Unity 编辑器内程序化生成 53 个 D4 canonical case mesh，
    /// 完全在 Unity 坐标系下计算，不依赖 Blender 导出/FBX 导入。
    /// 生成的每个 p_case_{ci}.prefab 包含：
    ///   - CubedMeshPrefab 组件（Gizmos 线框 + 顶点标注）
    ///   - MeshFilter / MeshRenderer（程序化 mesh）
    /// </summary>
    public class CaseMeshProceduralGenerator : EditorWindow
    {
        private ArtMeshCaseConfig _config;
        private string            _outputFolder = "Assets/MarchingCubes/ArtMesh/Generated";
        private string            _log          = "";
        private Vector2           _scroll;

        [MenuItem("MarchingCubes/Procedural Case Mesh Generator")]
        static void Open() => GetWindow<CaseMeshProceduralGenerator>("Case Mesh Gen");

        void OnGUI()
        {
            GUILayout.Label("Procedural Case Mesh Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "直接在 Unity 坐标系内生成 53 个 canonical case mesh，\n" +
                "无需 Blender 导出 / FBX 导入，无坐标系转换问题。",
                MessageType.Info);
            EditorGUILayout.Space(4);

            _config = (ArtMeshCaseConfig)EditorGUILayout.ObjectField(
                "Config Asset", _config, typeof(ArtMeshCaseConfig), false);

            EditorGUILayout.LabelField("Output Folder");
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField(_outputFolder);
                if (GUILayout.Button("Pick", GUILayout.Width(46)))
                {
                    string def  = Path.GetFullPath(
                        Path.Combine(Application.dataPath, _outputFolder.StartsWith("Assets")
                            ? _outputFolder.Substring("Assets".Length).TrimStart('/', '\\')
                            : _outputFolder));
                    string picked = EditorUtility.OpenFolderPanel("Output Folder", def, "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string full = Path.GetFullPath(picked).Replace('\\', '/');
                        string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
                        if (full.StartsWith(data)) _outputFolder = "Assets" + full.Substring(data.Length);
                    }
                }
            }

            EditorGUILayout.Space(4);
            bool canRun = _config != null && !string.IsNullOrEmpty(_outputFolder);
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button("Generate All 255 Case Prefabs", GUILayout.Height(32)))
                    DoGenerate();
            }
            if (!canRun) EditorGUILayout.HelpBox("请先指定 Config Asset。", MessageType.Warning);

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ═══════════════════════════════════════════════════════════════════
        // 生成主流程
        // ═══════════════════════════════════════════════════════════════════

        void DoGenerate()
        {
            _log = "";
            _config.EnsureSymmetry();

            // 确保输出目录存在
            string rel  = _outputFolder.TrimEnd('/', '\\');
            string full = Path.GetFullPath(Path.Combine(Application.dataPath,
                rel.StartsWith("Assets") ? rel.Substring("Assets".Length).TrimStart('/', '\\') : rel));
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);

            string meshDir   = rel + "/Meshes";
            string meshFull  = full + "/Meshes";
            if (!Directory.Exists(meshFull)) Directory.CreateDirectory(meshFull);
            AssetDatabase.Refresh();

            // 缓存：每个 canonical 的 mesh 只生成一次（供多个 ci 共享）
            var meshCache = new Dictionary<int, Mesh>();

            int ok = 0, skip = 0;
            for (int ci = 1; ci <= 254; ci++)
            {
                int  canonical = _config.GetCanonicalIndex(ci);
                var  d4        = _config.GetRotation(ci);
                bool isFlipped = _config.GetFlipped(ci);

                // ── 获取或生成 canonical mesh ──────────────────────────────
                if (!meshCache.TryGetValue(canonical, out Mesh canonMesh))
                {
                    canonMesh = BuildCaseMesh(canonical);
                    if (canonMesh == null) { skip++; continue; }
                    string meshPath = $"{meshDir}/mesh_case_{canonical}.asset";
                    AssetDatabase.CreateAsset(canonMesh, meshPath);
                    meshCache[canonical] = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    canonMesh = meshCache[canonical];
                }
                if (canonMesh == null) { skip++; continue; }

                // ── 构建 prefab 层级 ────────────────────────────────────────
                var root  = new GameObject($"p_case_{ci}");
                var gComp = root.AddComponent<CubedMeshPrefab>();
                gComp.mask = (CubeVertexMask)ci;

                var child = new GameObject("mesh");
                child.transform.SetParent(root.transform, false);

                var mf = child.AddComponent<MeshFilter>();
                mf.sharedMesh = canonMesh;
                child.AddComponent<MeshRenderer>();   // 默认材质

                // D4 旋转（围绕 cube 中心 0.5,0.5,0.5）
                var   s      = new Vector3(0.5f, 0.5f, 0.5f);
                child.transform.localRotation = d4;
                child.transform.localPosition = s - d4 * s;
                child.transform.localScale    = isFlipped
                    ? new Vector3(-1f, 1f, 1f) : Vector3.one;

                // ── 保存 prefab ─────────────────────────────────────────────
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
            _log = $"✓ Generated {ok} prefabs  (skipped {skip})\n→ {rel}";
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════════════
        // 八分体填充 mesh 生成（Unity 坐标系，无 Blender 依赖）
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 在 Unity [0,1]³ 坐标系内，为指定 cubeIndex 生成八分体填充 mesh。
        /// 每个 active 顶点对应一个 0.5³ 角块；相邻 active 顶点自然合并。
        /// </summary>
        static Mesh BuildCaseMesh(int cubeIndex)
        {
            // active 八分体集合：顶点坐标 (0或1, 0或1, 0或1) 即八分体格位
            var active = new HashSet<(int, int, int)>();
            for (int vi = 0; vi < 8; vi++)
            {
                if ((cubeIndex & (1 << vi)) != 0)
                {
                    var v = CubeTable.Vertices[vi];
                    active.Add((v.x, v.y, v.z));
                }
            }
            if (active.Count == 0) return null;

            // 顶点缓存（去重）
            var verts   = new List<Vector3>();
            var tris    = new List<int>();
            var vertMap = new Dictionary<Vector3, int>();

            int V(Vector3 p)
            {
                if (!vertMap.TryGetValue(p, out int idx))
                { idx = verts.Count; verts.Add(p); vertMap[p] = idx; }
                return idx;
            }

            // 6方向：(方向向量, 面4顶点局部偏移)
            // 偏移 0→轴最小端, 1→轴最大端（在本八分体内）
            // CCW 绕序确保法线朝外（Unity 左手坐标系下可见）
            var dirs = new (int dx, int dy, int dz,
                           (int,int,int)[] corners)[]
            {
                // +X  法线 +X：从 +X 方向看，CCW
                (+1,0,0, new[]{(1,0,0),(1,1,0),(1,1,1),(1,0,1)}),
                // -X  法线 -X
                (-1,0,0, new[]{(0,0,0),(0,0,1),(0,1,1),(0,1,0)}),
                // +Y  法线 +Y
                (0,+1,0, new[]{(0,1,0),(0,1,1),(1,1,1),(1,1,0)}),
                // -Y  法线 -Y
                (0,-1,0, new[]{(0,0,0),(1,0,0),(1,0,1),(0,0,1)}),
                // +Z  法线 +Z
                (0,0,+1, new[]{(0,0,1),(1,0,1),(1,1,1),(0,1,1)}),
                // -Z  法线 -Z
                (0,0,-1, new[]{(0,0,0),(0,1,0),(1,1,0),(1,0,0)}),
            };

            foreach (var (gx, gy, gz) in active)
            {
                float x0 = gx * 0.5f, x1 = (gx + 1) * 0.5f;
                float y0 = gy * 0.5f, y1 = (gy + 1) * 0.5f;
                float z0 = gz * 0.5f, z1 = (gz + 1) * 0.5f;

                Vector3 C(int cx, int cy, int cz) => new Vector3(
                    cx == 0 ? x0 : x1, cy == 0 ? y0 : y1, cz == 0 ? z0 : z1);

                foreach (var (dx, dy, dz, corners) in dirs)
                {
                    if (active.Contains((gx + dx, gy + dy, gz + dz))) continue;

                    // Quad → 2 triangles (CCW)
                    int a = V(C(corners[0].Item1, corners[0].Item2, corners[0].Item3));
                    int b = V(C(corners[1].Item1, corners[1].Item2, corners[1].Item3));
                    int c = V(C(corners[2].Item1, corners[2].Item2, corners[2].Item3));
                    int d = V(C(corners[3].Item1, corners[3].Item2, corners[3].Item3));
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(d);
                }
            }

            if (verts.Count == 0) return null;

            var mesh = new Mesh { name = $"case_{cubeIndex}" };
            mesh.vertices  = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

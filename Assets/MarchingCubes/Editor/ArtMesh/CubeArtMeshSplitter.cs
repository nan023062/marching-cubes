using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// Static tool for generating reference templates and splitting a large art mesh into
    /// per-cube-index Mesh assets and Prefabs, then wiring them into CubeArtMeshConfig.
    /// </summary>
    public static class CubeArtMeshSplitter
    {
        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Creates a reference template in the current scene.
        /// Each cell is a labelled semi-transparent unit cube so artists can model
        /// their mesh atlas to the correct grid coordinates.
        /// </summary>
        public static GameObject GenerateTemplate(bool plan256, CubeArtMeshConfig config)
        {
            if (!plan256 && config == null)
            {
                Debug.LogWarning("[CubeArtMeshSplitter] Plan A requires a CubeArtMeshConfig. Aborting.");
                return null;
            }

            List<int> cubeIndices;
            int gridWidth;

            if (plan256)
            {
                cubeIndices = BuildPlan256Indices();
                gridWidth = 16;
            }
            else
            {
                cubeIndices = BuildPlanAIndices(config);
                if (cubeIndices == null)
                    return null;
                gridWidth = 8;
            }

            string rootName = plan256 ? "ArtMeshTemplate_256" : "ArtMeshTemplate_55";
            GameObject root = new GameObject(rootName);

            Material templateMat = CreateTransparentMaterial();
            Mesh cubeMesh = GetUnitCubeMesh();

            for (int n = 0; n < cubeIndices.Count; n++)
            {
                int cubeIndex = cubeIndices[n];
                int col = n % gridWidth;
                int row = n / gridWidth;

                string childName = cubeIndex + "_col" + col + "_row" + row;
                GameObject cell = new GameObject(childName);
                cell.transform.SetParent(root.transform);
                cell.transform.localPosition = new Vector3(col + 0.5f, 0.5f, row + 0.5f);
                cell.transform.localScale = new Vector3(0.98f, 0.98f, 0.98f);

                MeshFilter mf = cell.AddComponent<MeshFilter>();
                mf.sharedMesh = cubeMesh;

                MeshRenderer mr = cell.AddComponent<MeshRenderer>();
                mr.sharedMaterial = templateMat;

                CubedMeshPrefab marker = cell.AddComponent<CubedMeshPrefab>();
                marker.mask = (CubeVertexMask)cubeIndex;
            }

            return root;
        }

        /// <summary>
        /// Splits a large mesh by grid cell, saves each cell as a Mesh asset and Prefab,
        /// and assigns them into the provided CubeArtMeshConfig.
        /// </summary>
        public static void SplitAndAssign(
            Mesh sourceMesh,
            bool plan256,
            CubeArtMeshConfig config,
            string outputFolder,
            Material defaultMaterial)
        {
            if (sourceMesh == null)
            {
                Debug.LogError("[CubeArtMeshSplitter] sourceMesh is null.");
                return;
            }
            if (config == null)
            {
                Debug.LogError("[CubeArtMeshSplitter] config is null.");
                return;
            }
            if (string.IsNullOrEmpty(outputFolder))
            {
                Debug.LogError("[CubeArtMeshSplitter] outputFolder is empty.");
                return;
            }

            // Build plan A canonical list for index lookup (needed even in plan B for validation)
            List<int> planACanonicals = null;
            if (!plan256)
            {
                planACanonicals = BuildPlanAIndices(config);
                if (planACanonicals == null)
                    return;
            }

            // Group triangles by (col, row)
            Dictionary<long, List<int>> cellTriangles = GroupTrianglesByCell(sourceMesh);

            Vector3[] srcVerts = sourceMesh.vertices;
            Vector2[] srcUVs = sourceMesh.uv;
            Vector3[] srcNormals = sourceMesh.normals;
            bool hasUV = srcUVs != null && srcUVs.Length == srcVerts.Length;
            bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;

            EnsureFolder(outputFolder);

            int splitCount = 0;

            foreach (KeyValuePair<long, List<int>> pair in cellTriangles)
            {
                long key = pair.Key;
                List<int> triIndices = pair.Value;

                int col = (int)(key & 0xFFFF);
                int row = (int)((key >> 16) & 0xFFFF);

                int cubeIndex = ResolveCubeIndex(col, row, plan256, planACanonicals);
                if (cubeIndex <= 0)
                    continue; // skip invalid or index 0

                // Build remapped vertex arrays
                Dictionary<int, int> vertexRemap = new Dictionary<int, int>();
                List<Vector3> newVerts = new List<Vector3>();
                List<Vector2> newUVs = new List<Vector2>();
                List<Vector3> newNormals = new List<Vector3>();
                List<int> newTris = new List<int>();

                Vector3 cellOrigin = new Vector3(col, 0, row);

                foreach (int srcIdx in triIndices)
                {
                    int remapped;
                    if (!vertexRemap.TryGetValue(srcIdx, out remapped))
                    {
                        remapped = newVerts.Count;
                        vertexRemap[srcIdx] = remapped;
                        newVerts.Add(srcVerts[srcIdx] - cellOrigin);
                        if (hasUV) newUVs.Add(srcUVs[srcIdx]);
                        if (hasNormals) newNormals.Add(srcNormals[srcIdx]);
                    }
                    newTris.Add(remapped);
                }

                string meshName = "art_cm_" + cubeIndex;
                Mesh mesh = new Mesh();
                mesh.name = meshName;
                mesh.vertices = newVerts.ToArray();
                mesh.triangles = newTris.ToArray();
                if (hasUV) mesh.uv = newUVs.ToArray();
                if (hasNormals)
                    mesh.normals = newNormals.ToArray();
                else
                    mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                string meshPath = outputFolder + "/" + meshName + ".asset";
                AssetDatabase.CreateAsset(mesh, meshPath);

                // Create temporary GameObject for prefab export
                GameObject go = new GameObject(meshName);
                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = defaultMaterial != null ? defaultMaterial : GetDefaultMaterial();

                string prefabPath = outputFolder + "/" + meshName + ".prefab";
                GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);

                // Assign to config
                var entry = config.GetEntry(cubeIndex);
                if (entry != null)
                {
                    entry.prefab = prefabAsset;
                    EditorUtility.SetDirty(config);
                }

                splitCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log("[CubeArtMeshSplitter] Split " + splitCount + " cells into " + outputFolder);
        }

        // -----------------------------------------------------------------------
        // Triangle grouping
        // -----------------------------------------------------------------------

        private static Dictionary<long, List<int>> GroupTrianglesByCell(Mesh mesh)
        {
            int[] indices = mesh.triangles;
            Vector3[] verts = mesh.vertices;
            var groups = new Dictionary<long, List<int>>();

            for (int t = 0; t < indices.Length; t += 3)
            {
                int i0 = indices[t];
                int i1 = indices[t + 1];
                int i2 = indices[t + 2];

                int col0 = Mathf.FloorToInt(verts[i0].x);
                int row0 = Mathf.FloorToInt(verts[i0].z);
                int col1 = Mathf.FloorToInt(verts[i1].x);
                int row1 = Mathf.FloorToInt(verts[i1].z);
                int col2 = Mathf.FloorToInt(verts[i2].x);
                int row2 = Mathf.FloorToInt(verts[i2].z);

                int col;
                int row;

                if (col0 == col1 && col1 == col2 && row0 == row1 && row1 == row2)
                {
                    col = col0;
                    row = row0;
                }
                else
                {
                    Vector3 centroid = (verts[i0] + verts[i1] + verts[i2]) / 3f;
                    col = Mathf.FloorToInt(centroid.x);
                    row = Mathf.FloorToInt(centroid.z);
                    Debug.LogWarning("[CubeArtMeshSplitter] Triangle spans multiple cells; using centroid to assign cell (" + col + ", " + row + ").");
                }

                long key = (long)(col & 0xFFFF) | ((long)(row & 0xFFFF) << 16);

                List<int> list;
                if (!groups.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    groups[key] = list;
                }
                list.Add(i0);
                list.Add(i1);
                list.Add(i2);
            }

            return groups;
        }

        // -----------------------------------------------------------------------
        // Index helpers
        // -----------------------------------------------------------------------

        private static int ResolveCubeIndex(int col, int row, bool plan256, List<int> planACanonicals)
        {
            if (plan256)
            {
                return col + row * 16;
            }
            else
            {
                int n = col + row * 8;
                if (planACanonicals == null || n < 0 || n >= planACanonicals.Count)
                    return -1;
                return planACanonicals[n];
            }
        }

        private static List<int> BuildPlan256Indices()
        {
            var list = new List<int>(255);
            for (int cubeIndex = 1; cubeIndex < 256; cubeIndex++)
                list.Add(cubeIndex);
            return list;
        }

        /// <summary>
        /// Returns the ordered list of canonical cube indices from config (IsCanonical==true, ascending).
        /// Returns null and logs a warning if config symmetry has not been computed yet.
        /// </summary>
        private static List<int> BuildPlanAIndices(CubeArtMeshConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[CubeArtMeshSplitter] config is null. Cannot build Plan A index list.");
                return null;
            }

            var canonicals = new List<int>();
            for (int i = 0; i < 256; i++)
            {
                if (config.IsCanonical(i))
                    canonicals.Add(i);
            }

            // A freshly-created config with no symmetry computed will have canonicalIndex[i]==i
            // for all i (default 0-initialized then set to i in EnsureInitialized... actually
            // default is 0 everywhere). We detect this by checking count: a valid symmetry
            // table produces exactly 55 canonical entries.
            if (canonicals.Count == 0 || canonicals.Count == 256)
            {
                Debug.LogWarning("[CubeArtMeshSplitter] Plan A requires symmetry table to be computed first (Compute Symmetry). Aborting.");
                return null;
            }

            // Sort ascending (they should already be in order but sort for safety)
            canonicals.Sort();
            return canonicals;
        }

        // -----------------------------------------------------------------------
        // Material / Mesh helpers
        // -----------------------------------------------------------------------

        private static Material CreateTransparentMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.color = new Color(0.5f, 0.8f, 1f, 0.15f);

            // Enable Standard transparent mode if using Standard shader
            if (shader != null && shader.name == "Standard")
            {
                mat.SetFloat("_Mode", 3f);  // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            return mat;
        }

        private static Mesh GetUnitCubeMesh()
        {
            // Use a temporary GameObject to grab Unity's built-in cube mesh
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static Material GetDefaultMaterial()
        {
            return new Material(Shader.Find("Standard"));
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            // Walk up and create each missing segment
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

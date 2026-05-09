using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MarchingCubes
{
    /// <summary>
    /// mesh operation tool
    /// </summary>
    public static class MeshUtility
    {
        private const string MaterialPath = "Assets/MarchingCubes/Sample/Resources/m_marching.mat";
        private const string GenerateDir = "Assets/MarchingCubes/Sample/Resources/GenerateCubeMesh/";
        private const string GeneratePath = GenerateDir + "cube_mesh_256.prefab";
        
#if UNITY_EDITOR
        [MenuItem("Assets/Create/MarchingCubes/Gen 256-Mesh")]
        public static void CreateMeshAsset()
        {
            Transform root = new GameObject("cube_mesh_256").transform;
            Transform[] vertexRoots = new Transform[CubeTable.VertexCount];
            for (int i = 0; i < CubeTable.VertexCount; i++)
            {
                Transform child = new GameObject($"vertex_{i}").transform;
                vertexRoots[i] = child;
                child.SetParent(root);
                Vector3 pos = new Vector3(0, 0, i * 3);
                child.localPosition = pos;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
            }
            
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            
            Mesh[] meshes = CreateAllKindMeshed();
            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh mesh = meshes[i];
                if(null == mesh)
                    continue;
                
                // save mesh asset
                string path = GenerateDir + $"cm_{i}.asset";
                AssetDatabase.CreateAsset(mesh, path);
                Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                
                GameObject subGo = new GameObject($"{i}", typeof(MeshFilter), typeof(MeshRenderer),typeof(CubedMeshPrefab));
                subGo.GetComponent<MeshFilter>().sharedMesh = meshAsset;
                subGo.GetComponent<MeshRenderer>().sharedMaterial = material; 
                CubedMeshPrefab cubedMeshPrefab = subGo.GetComponent<CubedMeshPrefab>();
                cubedMeshPrefab.mask = (CubeVertexMask)i;
                
                int vertexCount = 0;
                for (int j = 0; j < CubeTable.VertexCount; j++)
                    vertexCount += (i >> j) & 1;
                
                Transform subTransform = subGo.transform;
                subTransform.SetParent(vertexRoots[vertexCount]);
            }
            
            // 按照顶点数量 分开排列
            for (int i = 0; i < CubeTable.VertexCount; i++)
            {
                Transform vertexRoot = vertexRoots[i];
                int childCount = vertexRoot.childCount;
                vertexRoot.gameObject.name = $"{i}个顶点_({childCount}个)";
                
                for (int j = 0; j < childCount; j++)
                {
                    Transform child = vertexRoot.GetChild(j);
                    Vector3 pos = new Vector3( j * 2, 0, 0);
                    child.localPosition = pos;
                    child.localRotation = Quaternion.identity;
                    child.localScale = Vector3.one;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, GeneratePath);
            Object.DestroyImmediate(root.gameObject);
        }
#endif
        public static Mesh[] CreateAllKindMeshed()
        {
            Mesh[] meshes = new Mesh[CubeTable.CubeKind];
            for (int cubeIndex = 0; cubeIndex < CubeTable.CubeKind; cubeIndex++)
            {
                int edgeMask = CubeTable.GetCubeKindEdgeMask(cubeIndex);
                if (edgeMask == 0)
                {
                    Debug.LogError($"cubeIndex: {cubeIndex} edgeMask: {edgeMask}");
                    continue;
                }
                
                Mesh mesh = new Mesh();
                mesh.name = $"{cubeIndex}";
                
                byte[] points = new byte[CubeTable.VertexCount];
                Vector3[] vertices = new Vector3[CubeTable.EdgeCount];
                for (int edge = 0; edge < CubeTable.EdgeCount; edge++)
                {
                    if ((edgeMask & (1 << edge)) > 0)
                    {
                        ref readonly var t = ref CubeTable.Edges[edge];
                        points[t.p1] += 1;
                        points[t.p2] += 1;
                        
                        ref readonly var p1 = ref CubeTable.Vertices[t.p1];
                        ref readonly var p2 =  ref CubeTable.Vertices[t.p2];
                        vertices[edge] = (p1.ToVector3() + p2.ToVector3()) * 0.5f;
                    }
                }
                
                List<Vector3> vertexList = new List<Vector3>(12);
                ref readonly int[] cubeTri = ref CubeTable.GetCubeKindTriangles(cubeIndex);
                for (int i = 0; cubeTri[i] != -1; i += 3)
                {
                    vertexList.Add( vertices[cubeTri[i + 2]]);
                    vertexList.Add( vertices[cubeTri[i + 1]]);
                    vertexList.Add( vertices[cubeTri[i]]);
                }
                
                int[] triangles = new int[vertexList.Count];
                Vector2[] uvs = new Vector2[vertexList.Count];
                for (int i = 0; i < vertexList.Count; i++)
                {
                    triangles[i] = i;
                    uvs[i] = new Vector2(vertexList[i].x, vertexList[i].z);
                }
                
                mesh.SetVertices(vertexList);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                meshes[cubeIndex] = mesh;
            }
            return meshes;
        }
        
        
    }
}
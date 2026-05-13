using System.Collections.Generic;
using UnityEngine;

namespace MarchingEdges
{
    /// <summary>
    /// Marching Edges 程序化网格生成器。
    ///
    /// 给定 12-bit 面槽 mask，为每个激活槽生成双面 0.5×0.5 quad。
    /// 生成结果可用于调试可视化或作为 placeholder 网格。
    /// 精细美术资产请使用 MeCaseConfig prefab 路径。
    /// </summary>
    public static class EdgeMesh
    {
        /// <summary>单个顶点的 12-bit case → Mesh（cube-local 坐标，中心在原点）。</summary>
        public static Mesh BuildSingle(int mask)
        {
            var verts = new List<Vector3>();
            var tris  = new List<int>();
            var norms = new List<Vector3>();

            for (int slot = 0; slot < 12; slot++)
            {
                if ((mask >> slot & 1) == 0) continue;
                AddQuad(verts, tris, norms,
                    EdgeTable.SlotVerts[slot],
                    EdgeTable.SlotNormals[slot]);
            }

            return Build(verts, tris, norms);
        }

        /// <summary>
        /// 全格点网格批量生成（连续 Mesh，适合 MeshCollider / 可视化）。
        /// positions[x,y,z] = 顶点世界坐标；masks[x,y,z] = 该顶点的 12-bit case。
        /// </summary>
        public static Mesh BuildGrid(Vector3[,,] positions, int[,,] masks)
        {
            int nx = masks.GetLength(0);
            int ny = masks.GetLength(1);
            int nz = masks.GetLength(2);

            var verts = new List<Vector3>();
            var tris  = new List<int>();
            var norms = new List<Vector3>();

            for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
            for (int z = 0; z < nz; z++)
            {
                int mask = masks[x, y, z];
                if (mask == 0) continue;

                Vector3 center = positions[x, y, z];
                for (int slot = 0; slot < 12; slot++)
                {
                    if ((mask >> slot & 1) == 0) continue;

                    var localVerts = EdgeTable.SlotVerts[slot];
                    var worldVerts = new Vector3[4];
                    for (int i = 0; i < 4; i++)
                        worldVerts[i] = center + localVerts[i];

                    AddQuad(verts, tris, norms, worldVerts, EdgeTable.SlotNormals[slot]);
                }
            }

            var m = Build(verts, tris, norms);
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            return m;
        }

        // ── 内部辅助 ─────────────────────────────────────────────────────────

        static void AddQuad(List<Vector3> verts, List<int> tris, List<Vector3> norms,
                            Vector3[] corners, Vector3 normal)
        {
            int v = verts.Count;
            verts.Add(corners[0]); verts.Add(corners[1]);
            verts.Add(corners[2]); verts.Add(corners[3]);
            norms.Add(normal); norms.Add(normal);
            norms.Add(normal); norms.Add(normal);

            // 正面（逆时针）
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            // 背面（顺时针，同顶点）
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
            tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
        }

        static Mesh Build(List<Vector3> verts, List<int> tris, List<Vector3> norms)
        {
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(norms);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

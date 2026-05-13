using System.Collections.Generic;
using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MarchingSquares 地板砖程序化网格生成器。
    ///
    /// BuildGrid：给定 2D 激活格子网格，生成平铺于 Y=0 的连续 Mesh（可用于碰撞/调试）。
    /// BuildSingle：给定单格的 4-bit 邻居 mask，生成该格的平面 quad。
    ///
    /// 精细美术资产请使用 SquareCaseConfig prefab 路径（6 canonical 美术砖）。
    /// </summary>
    public static class SquareMesh
    {
        /// <summary>
        /// 为 2D 格子网格中的所有激活格生成平面 Mesh（Y=0, 每格 cellSize×cellSize 的 quad）。
        /// origin 为格子 [0,0] 的世界坐标。
        /// </summary>
        public static Mesh BuildGrid(bool[,] grid, float cellSize = 1f, Vector3 origin = default)
        {
            int W = grid.GetLength(0), D = grid.GetLength(1);

            var verts = new List<Vector3>();
            var tris  = new List<int>();
            var norms = new List<Vector3>();

            for (int x = 0; x < W; x++)
            for (int z = 0; z < D; z++)
            {
                if (!grid[x, z]) continue;
                AddQuad(verts, tris, norms,
                    origin + new Vector3(x * cellSize,       0f, z * cellSize),
                    origin + new Vector3((x + 1) * cellSize, 0f, z * cellSize),
                    origin + new Vector3((x + 1) * cellSize, 0f, (z + 1) * cellSize),
                    origin + new Vector3(x * cellSize,       0f, (z + 1) * cellSize));
            }

            return Build(verts, tris, norms);
        }

        /// <summary>
        /// 为单个格子生成 1×1 平面 quad（cellSize=1, 以格子左下角为原点）。
        /// 可叠加多个 quad 用于 Collider 构建。
        /// </summary>
        public static Mesh BuildSingle(float cellSize = 1f)
        {
            var verts = new List<Vector3>();
            var tris  = new List<int>();
            var norms = new List<Vector3>();

            AddQuad(verts, tris, norms,
                new Vector3(0f,        0f, 0f),
                new Vector3(cellSize,  0f, 0f),
                new Vector3(cellSize,  0f, cellSize),
                new Vector3(0f,        0f, cellSize));

            return Build(verts, tris, norms);
        }

        // ── 内部辅助 ─────────────────────────────────────────────────────────

        static void AddQuad(List<Vector3> verts, List<int> tris, List<Vector3> norms,
                            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int idx = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            norms.Add(Vector3.up); norms.Add(Vector3.up);
            norms.Add(Vector3.up); norms.Add(Vector3.up);

            // 正面（仰视不可见，建议用双面 shader 或额外背面三角）
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
            tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
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

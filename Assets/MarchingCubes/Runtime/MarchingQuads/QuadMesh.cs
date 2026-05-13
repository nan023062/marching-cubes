using System.Collections.Generic;
using UnityEngine;

namespace MarchingQuads
{
    /// <summary>
    /// MarchingQuads 围墙面板程序化网格生成器。
    ///
    /// BuildPanels：将激活的 xPanels / zPanels 生成为垂直 quad Mesh（可用于碰撞/调试）。
    /// BuildSingle：生成单块面板 quad（宽=cellSize，高=wallHeight）。
    ///
    /// 精细美术资产请使用 QuadCaseConfig prefab 路径（6 canonical 顶点连接件）。
    /// </summary>
    public static class QuadMesh
    {
        /// <summary>
        /// 将所有激活面板生成为竖直 Mesh（Y=0 到 Y=wallHeight）。
        /// xPanels[vx, cz]：在 x=vx 处跨越 z ∈ [cz, cz+1] 的面板。
        /// zPanels[cx, vz]：在 z=vz 处跨越 x ∈ [cx, cx+1] 的面板。
        /// </summary>
        public static Mesh BuildPanels(bool[,] xPanels, bool[,] zPanels,
                                       float cellSize = 1f, float wallHeight = 3f,
                                       Vector3 origin = default)
        {
            int Xw = xPanels.GetLength(0), Xd = xPanels.GetLength(1);
            int Zw = zPanels.GetLength(0), Zd = zPanels.GetLength(1);

            var verts = new List<Vector3>();
            var tris  = new List<int>();
            var norms = new List<Vector3>();

            // xPanels：面板法线朝 ±X，面板在 x=vx 处沿 Z 方向延伸
            for (int vx = 0; vx < Xw; vx++)
            for (int cz = 0; cz < Xd; cz++)
            {
                if (!xPanels[vx, cz]) continue;
                float wx = origin.x + vx  * cellSize;
                float z0 = origin.z + cz  * cellSize;
                float z1 = origin.z + (cz + 1) * cellSize;
                AddVerticalQuad(verts, tris, norms,
                    new Vector3(wx, origin.y,             z0),
                    new Vector3(wx, origin.y,             z1),
                    new Vector3(wx, origin.y + wallHeight, z1),
                    new Vector3(wx, origin.y + wallHeight, z0),
                    Vector3.right);
            }

            // zPanels：面板法线朝 ±Z，面板在 z=vz 处沿 X 方向延伸
            for (int cx = 0; cx < Zw; cx++)
            for (int vz = 0; vz < Zd; vz++)
            {
                if (!zPanels[cx, vz]) continue;
                float wz = origin.z + vz  * cellSize;
                float x0 = origin.x + cx  * cellSize;
                float x1 = origin.x + (cx + 1) * cellSize;
                AddVerticalQuad(verts, tris, norms,
                    new Vector3(x0, origin.y,              wz),
                    new Vector3(x1, origin.y,              wz),
                    new Vector3(x1, origin.y + wallHeight, wz),
                    new Vector3(x0, origin.y + wallHeight, wz),
                    Vector3.forward);
            }

            return Build(verts, tris, norms);
        }

        /// <summary>生成单块面板的竖直 quad（宽=cellSize，高=wallHeight，法线=+X）。</summary>
        public static Mesh BuildSingle(float cellSize = 1f, float wallHeight = 3f)
        {
            var verts = new List<Vector3>();
            var tris  = new List<int>();
            var norms = new List<Vector3>();

            AddVerticalQuad(verts, tris, norms,
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, cellSize),
                new Vector3(0f, wallHeight, cellSize),
                new Vector3(0f, wallHeight, 0f),
                Vector3.right);

            return Build(verts, tris, norms);
        }

        // ── 内部辅助 ─────────────────────────────────────────────────────────

        static void AddVerticalQuad(List<Vector3> verts, List<int> tris, List<Vector3> norms,
                                    Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
                                    Vector3 normal)
        {
            int idx = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            norms.Add(normal); norms.Add(normal);
            norms.Add(normal); norms.Add(normal);

            // 正面
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
            // 背面
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

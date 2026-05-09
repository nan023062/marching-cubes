//****************************************************************************
// File: DualContouring.cs
// Author: Li Nan
// Date: 2026-05-09
// Version: 1.0
//
// 与 CubeMesh.cs 对标——两者处理相同的 iso 场，但 Mesh 构建方式根本不同：
//
//   CubeMesh          : 顶点放在【边上】（插值），查 triTable 出三角面，每格最多 5 个三角
//   DualContouringMesh: 每个 active 格一个顶点放在【格内】（边交叉均值），
//                       每条穿越边触发相邻 4 格的顶点连成四边形
//****************************************************************************

using System.Collections.Generic;
using UnityEngine;

namespace DualContouring
{
    public sealed class DualContouringMesh
    {
        public readonly int X, Y, Z;

        // 与 CubeMesh 相同的采样点布局：(X+1)×(Y+1)×(Z+1)
        private readonly float[,,] _iso;
        public float IsoLevel = 0.5f;

        public Mesh mesh;

        // --- 格内顶点缓存（Key = 格坐标） ---
        // CubeMesh 无此结构；DC 每格最多一个顶点，共享给相邻边的四边形复用
        private readonly Dictionary<int, int> _cellVertex;
        private readonly List<Vector3> _vertices;
        private readonly List<int>     _triangles;

        // 8 个顶点相对坐标，与 CubeTable.Vertices 一致（不引用 MarchingCubes 模块）
        private static readonly (int x, int y, int z)[] CubeVerts =
        {
            (0,0,1),(1,0,1),(1,0,0),(0,0,0),
            (0,1,1),(1,1,1),(1,1,0),(0,1,0)
        };

        // 12 条边的端点索引对，与 CubeTable.Edges 一致
        private static readonly (int p1, int p2)[] CubeEdges =
        {
            (0,1),(1,2),(2,3),(3,0),
            (4,5),(5,6),(6,7),(7,4),
            (0,4),(1,5),(2,6),(3,7)
        };

        public DualContouringMesh(int x, int y, int z)
        {
            X = x; Y = y; Z = z;
            _iso       = new float[x + 1, y + 1, z + 1];
            _cellVertex = new Dictionary<int, int>(x * y * z);
            _vertices  = new List<Vector3>(x * y * z);
            _triangles = new List<int>(x * y * z * 6);
            mesh = new Mesh();
        }

        // 与 CubeMesh.SetPointISO 签名相同
        public void SetPointISO(int x, int y, int z, float iso)
        {
            x = Mathf.Clamp(x, 0, X);
            y = Mathf.Clamp(y, 0, Y);
            z = Mathf.Clamp(z, 0, Z);
            _iso[x, y, z] = iso;
        }

        // -----------------------------------------------------------------------
        // Rebuild — 对标 CubeMesh.Rebuild
        //
        // CubeMesh  : 以"格"为驱动，每格查表出三角
        // DC        : 以"边"为驱动，每条穿越边决定一个四边形
        //             ─ X 轴穿越边 → 关联 4 格 (varying y,z)
        //             ─ Y 轴穿越边 → 关联 4 格 (varying x,z)
        //             ─ Z 轴穿越边 → 关联 4 格 (varying x,y)
        // -----------------------------------------------------------------------
        public void Rebuild()
        {
            _vertices.Clear();
            _triangles.Clear();
            _cellVertex.Clear();

            // X 轴边：遍历所有 (x,y,z)→(x+1,y,z) 边
            for (int y = 0; y <= Y; y++)
            for (int z = 0; z <= Z; z++)
            for (int x = 0; x < X;  x++)
            {
                if (!Crosses(x,y,z, x+1,y,z)) continue;
                bool flip = _iso[x, y, z] > IsoLevel;
                EmitQuad(x,y-1,z-1,  x,y,z-1,  x,y,z,  x,y-1,z,  flip);
            }

            // Y 轴边：遍历所有 (x,y,z)→(x,y+1,z) 边
            for (int x = 0; x <= X; x++)
            for (int z = 0; z <= Z; z++)
            for (int y = 0; y < Y;  y++)
            {
                if (!Crosses(x,y,z, x,y+1,z)) continue;
                bool flip = _iso[x, y, z] > IsoLevel;
                EmitQuad(x-1,y,z-1,  x,y,z-1,  x,y,z,  x-1,y,z,  flip);
            }

            // Z 轴边：遍历所有 (x,y,z)→(x,y,z+1) 边
            for (int x = 0; x <= X; x++)
            for (int y = 0; y <= Y; y++)
            for (int z = 0; z < Z;  z++)
            {
                if (!Crosses(x,y,z, x,y,z+1)) continue;
                bool flip = _iso[x, y, z] > IsoLevel;
                EmitQuad(x-1,y-1,z,  x,y-1,z,  x,y,z,  x-1,y,z,  flip);
            }

            mesh.Clear();
            mesh.SetVertices(_vertices);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
        }

        // 边是否穿越等值面（两端 iso 符号不同）
        private bool Crosses(int x1, int y1, int z1, int x2, int y2, int z2)
            => (_iso[x1,y1,z1] < IsoLevel) != (_iso[x2,y2,z2] < IsoLevel);

        private bool Valid(int cx, int cy, int cz)
            => cx >= 0 && cx < X && cy >= 0 && cy < Y && cz >= 0 && cz < Z;

        private int Key(int cx, int cy, int cz) => cx + cy * X + cz * X * Y;

        // 取或创建格内顶点
        //
        // CubeMesh  : 顶点 = 边上的线性插值点（CubeTable.InterpolateVerts）
        // DC        : 顶点 = 该格所有穿越边插值点的【均值】（简化版；完整版用 QEF 最小化误差以保留锐角）
        private int GetOrCreate(int cx, int cy, int cz)
        {
            int key = Key(cx, cy, cz);
            if (_cellVertex.TryGetValue(key, out int idx)) return idx;

            Vector3 sum  = Vector3.zero;
            int     count = 0;

            foreach (var (p1i, p2i) in CubeEdges)
            {
                var v1 = CubeVerts[p1i];
                var v2 = CubeVerts[p2i];
                int gx1 = cx+v1.x, gy1 = cy+v1.y, gz1 = cz+v1.z;
                int gx2 = cx+v2.x, gy2 = cy+v2.y, gz2 = cz+v2.z;

                if (Crosses(gx1,gy1,gz1, gx2,gy2,gz2))
                {
                    float s1 = _iso[gx1,gy1,gz1], s2 = _iso[gx2,gy2,gz2];
                    float t  = (IsoLevel - s1) / (s2 - s1);
                    sum += new Vector3(gx1+t*(gx2-gx1), gy1+t*(gy2-gy1), gz1+t*(gz2-gz1));
                    count++;
                }
            }

            if (count == 0) { _cellVertex[key] = -1; return -1; }

            idx = _vertices.Count;
            _vertices.Add(sum / count);
            _cellVertex[key] = idx;
            return idx;
        }

        // 四边形 → 两个三角；flip 由穿越边梯度方向决定，保证法线朝外
        private void EmitQuad(
            int ax, int ay, int az,
            int bx, int by, int bz,
            int cx, int cy, int cz,
            int dx, int dy, int dz,
            bool flip)
        {
            if (!Valid(ax,ay,az) || !Valid(bx,by,bz) ||
                !Valid(cx,cy,cz) || !Valid(dx,dy,dz)) return;

            int va = GetOrCreate(ax,ay,az);
            int vb = GetOrCreate(bx,by,bz);
            int vc = GetOrCreate(cx,cy,cz);
            int vd = GetOrCreate(dx,dy,dz);
            if (va<0||vb<0||vc<0||vd<0) return;

            if (flip)
            {
                _triangles.Add(va); _triangles.Add(vb); _triangles.Add(vc);
                _triangles.Add(va); _triangles.Add(vc); _triangles.Add(vd);
            }
            else
            {
                _triangles.Add(va); _triangles.Add(vc); _triangles.Add(vb);
                _triangles.Add(va); _triangles.Add(vd); _triangles.Add(vc);
            }
        }
    }
}

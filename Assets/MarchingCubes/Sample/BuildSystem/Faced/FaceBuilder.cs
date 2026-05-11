using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MarchingCubes.Sample
{
    /// <summary>
    /// Marching Edges 纯 C# 核心：
    /// 管理 3D 格点间的面槽数据，计算 12-bit case，提供 canonical 归约查表。
    /// </summary>
    public class FaceBuilder
    {
        public readonly int Nx, Ny, Nz;

        readonly bool[,,] _xFaces; // [Nx,   Ny-1, Nz-1]  YZ平面面
        readonly bool[,,] _yFaces; // [Nx-1, Ny,   Nz-1]  XZ平面面
        readonly bool[,,] _zFaces; // [Nx-1, Ny-1, Nz  ]  XY平面面

        // ── 静态 canonical 查表 ──────────────────────────────────────────────

        static int[]        s_canonIdx;
        static Quaternion[] s_canonRot;
        public static int   CanonicalCount { get; private set; }

        // ── 构造 ─────────────────────────────────────────────────────────────

        public FaceBuilder(int nx, int ny, int nz)
        {
            Nx = nx; Ny = ny; Nz = nz;
            _xFaces = new bool[Nx,     Ny - 1, Nz - 1];
            _yFaces = new bool[Nx - 1, Ny,     Nz - 1];
            _zFaces = new bool[Nx - 1, Ny - 1, Nz    ];
            EnsureLookup();
        }

        // ── 面槽读写 ─────────────────────────────────────────────────────────

        public bool GetXFace(int vx, int j,  int k)  => InBoundsX(vx, j,  k)  && _xFaces[vx, j,  k];
        public bool GetYFace(int i,  int vy, int k)  => InBoundsY(i,  vy, k)  && _yFaces[i,  vy, k];
        public bool GetZFace(int i,  int j,  int vz) => InBoundsZ(i,  j,  vz) && _zFaces[i,  j,  vz];

        /// <summary>设置 xFace，返回受影响的 4 个格点（需刷新 case）</summary>
        public List<Vector3Int> SetXFace(int vx, int j, int k, bool active)
        {
            if (!InBoundsX(vx, j, k)) return null;
            _xFaces[vx, j, k] = active;
            return XFaceVertices(vx, j, k);
        }

        public List<Vector3Int> SetYFace(int i, int vy, int k, bool active)
        {
            if (!InBoundsY(i, vy, k)) return null;
            _yFaces[i, vy, k] = active;
            return YFaceVertices(i, vy, k);
        }

        public List<Vector3Int> SetZFace(int i, int j, int vz, bool active)
        {
            if (!InBoundsZ(i, j, vz)) return null;
            _zFaces[i, j, vz] = active;
            return ZFaceVertices(i, j, vz);
        }

        // ── 边界检查（public 供 Controller 使用）────────────────────────────

        public bool InBoundsX(int vx, int j,  int k)  => vx >= 0 && vx < Nx     && j >= 0 && j < Ny - 1 && k >= 0 && k < Nz - 1;
        public bool InBoundsY(int i,  int vy, int k)  => i  >= 0 && i  < Nx - 1 && vy >= 0 && vy < Ny   && k >= 0 && k < Nz - 1;
        public bool InBoundsZ(int i,  int j,  int vz) => i  >= 0 && i  < Nx - 1 && j  >= 0 && j  < Ny - 1 && vz >= 0 && vz < Nz;

        // ── 受影响格点（面槽的 4 个角） ─────────────────────────────────────

        List<Vector3Int> XFaceVertices(int vx, int j, int k)
        {
            var l = new List<Vector3Int>(4);
            AddIfValid(l, vx, j,     k);     AddIfValid(l, vx, j + 1, k);
            AddIfValid(l, vx, j + 1, k + 1); AddIfValid(l, vx, j,     k + 1);
            return l;
        }

        List<Vector3Int> YFaceVertices(int i, int vy, int k)
        {
            var l = new List<Vector3Int>(4);
            AddIfValid(l, i,     vy, k);     AddIfValid(l, i + 1, vy, k);
            AddIfValid(l, i + 1, vy, k + 1); AddIfValid(l, i,     vy, k + 1);
            return l;
        }

        List<Vector3Int> ZFaceVertices(int i, int j, int vz)
        {
            var l = new List<Vector3Int>(4);
            AddIfValid(l, i,     j,     vz); AddIfValid(l, i + 1, j,     vz);
            AddIfValid(l, i + 1, j + 1, vz); AddIfValid(l, i,     j + 1, vz);
            return l;
        }

        void AddIfValid(List<Vector3Int> l, int x, int y, int z)
        {
            if (x >= 0 && x < Nx && y >= 0 && y < Ny && z >= 0 && z < Nz)
                l.Add(new Vector3Int(x, y, z));
        }

        // ── Case 计算 ────────────────────────────────────────────────────────

        public int GetCaseIndex(int vx, int vy, int vz)
        {
            int m = 0;
            // X 组：YZ 平面面（x = vx 处 4 个象限）
            if (XF(vx, vy,     vz    )) m |= 1;     // X0 +Y+Z
            if (XF(vx, vy - 1, vz    )) m |= 2;     // X1 -Y+Z
            if (XF(vx, vy - 1, vz - 1)) m |= 4;     // X2 -Y-Z
            if (XF(vx, vy,     vz - 1)) m |= 8;     // X3 +Y-Z
            // Y 组：XZ 平面面（y = vy 处 4 个象限）
            if (YF(vx,     vy, vz    )) m |= 16;    // Y0 +X+Z
            if (YF(vx - 1, vy, vz    )) m |= 32;    // Y1 -X+Z
            if (YF(vx - 1, vy, vz - 1)) m |= 64;    // Y2 -X-Z
            if (YF(vx,     vy, vz - 1)) m |= 128;   // Y3 +X-Z
            // Z 组：XY 平面面（z = vz 处 4 个象限）
            if (ZF(vx,     vy,     vz)) m |= 256;   // Z0 +X+Y
            if (ZF(vx - 1, vy,     vz)) m |= 512;   // Z1 -X+Y
            if (ZF(vx - 1, vy - 1, vz)) m |= 1024;  // Z2 -X-Y
            if (ZF(vx,     vy - 1, vz)) m |= 2048;  // Z3 +X-Y
            return m;
        }

        public (int canonIdx, Quaternion rot) GetCanonical(int vx, int vy, int vz)
        {
            int c = GetCaseIndex(vx, vy, vz);
            return (s_canonIdx[c], s_canonRot[c]);
        }

        bool XF(int vx, int j,  int k)  => InBoundsX(vx, j,  k)  && _xFaces[vx, j,  k];
        bool YF(int i,  int vy, int k)  => InBoundsY(i,  vy, k)  && _yFaces[i,  vy, k];
        bool ZF(int i,  int j,  int vz) => InBoundsZ(i,  j,  vz) && _zFaces[i,  j,  vz];

        // ── canonical 查表构建 ───────────────────────────────────────────────

        static void EnsureLookup()
        {
            if (s_canonIdx != null) return;

            var maskToMin = new int[4096];
            var maskToRot = new int[4096];
            var canonSet  = new SortedSet<int>();

            for (int m = 0; m < 4096; m++)
            {
                int minM = m, bestR = 0, cur = m;
                for (int r = 1; r < 4; r++)
                {
                    cur = Rotate90CW(cur);
                    if (cur < minM) { minM = cur; bestR = r; }
                }
                maskToMin[m] = minM;
                maskToRot[m] = bestR;
                canonSet.Add(minM);
            }

            var canonList = canonSet.ToArray();
            CanonicalCount = canonList.Length; // 应为 1044
            var idxOf = new Dictionary<int, int>(canonList.Length);
            for (int i = 0; i < canonList.Length; i++) idxOf[canonList[i]] = i;

            s_canonIdx = new int[4096];
            s_canonRot = new Quaternion[4096];
            for (int m = 0; m < 4096; m++)
            {
                s_canonIdx[m] = idxOf[maskToMin[m]];
                s_canonRot[m] = Quaternion.Euler(0, maskToRot[m] * 90f, 0);
            }
        }

        /// <summary>绕 Y 轴 90° CW 旋转的 12-bit 置换（从+Y向下看，顺时针）</summary>
        static int Rotate90CW(int mask)
        {
            int r = 0;
            if ((mask & 1)    != 0) r |= 1 << 8;   // X0 → Z0
            if ((mask & 2)    != 0) r |= 1 << 11;  // X1 → Z3
            if ((mask & 4)    != 0) r |= 1 << 10;  // X2 → Z2
            if ((mask & 8)    != 0) r |= 1 << 9;   // X3 → Z1
            if ((mask & 16)   != 0) r |= 1 << 7;   // Y0 → Y3
            if ((mask & 32)   != 0) r |= 1 << 4;   // Y1 → Y0
            if ((mask & 64)   != 0) r |= 1 << 5;   // Y2 → Y1
            if ((mask & 128)  != 0) r |= 1 << 6;   // Y3 → Y2
            if ((mask & 256)  != 0) r |= 1 << 3;   // Z0 → X3
            if ((mask & 512)  != 0) r |= 1 << 0;   // Z1 → X0
            if ((mask & 1024) != 0) r |= 1 << 1;   // Z2 → X1
            if ((mask & 2048) != 0) r |= 1 << 2;   // Z3 → X2
            return r;
        }
    }
}

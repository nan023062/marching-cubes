using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MarchingSquares 地板砖查表（类比 MarchingSquareTerrain 的 TileTable）。
    ///
    /// 角点编号（unit cell，XZ 平面），与 TileTable 完全对应：
    ///   V3(TL) ─── V2(TR)
    ///     │               │
    ///   V0(BL) ─── V1(BR)
    ///
    /// 每个格子 (cx, cz) 看其 4 个角点（顶点）是否在地板区域内：
    ///   V0 BL = vertex (cx,   cz  )
    ///   V1 BR = vertex (cx+1, cz  )
    ///   V2 TR = vertex (cx+1, cz+1)
    ///   V3 TL = vertex (cx,   cz+1)
    ///
    /// GetMeshCase(b0,b1,b2,b3) → 4-bit case index (0-15)，
    /// D4 水平对称归约 → 6 canonical：
    ///   0  (0b0000) 空      V3(TL)───V2(TR)
    ///   1  (0b0001) 单角         │         │
    ///   3  (0b0011) 邻边   V0(BL)───V1(BR)
    ///   5  (0b0101) 对角
    ///   7  (0b0111) 三角
    ///   15 (0b1111) 满格
    /// </summary>
    public static class SquareTable
    {
        public const int CornerCount  = 4;
        public const int CaseCount    = 16;  // 2^4

        // ── 角点坐标（与 TileTable.Corners 完全对应）────────────────────────

        /// <summary>四个角点的 (x, z) 坐标（unit cell [0,1]×[0,1]）。</summary>
        public static readonly (int x, int z)[] Corners =
        {
            (0, 0),  // V0 BL
            (1, 0),  // V1 BR
            (1, 1),  // V2 TR
            (0, 1),  // V3 TL
        };

        // ── Mesh 组合映射（binary 编码）──────────────────────────────────────

        /// <summary>
        /// 根据四角顶点是否在地板区域内，计算该格的 case index（0-15）。
        /// b0=V0(BL), b1=V1(BR), b2=V2(TR), b3=V3(TL)。
        /// 类比 TileTable.GetMeshCase，此处为纯 binary（0/1）而非 base-3 高度。
        /// </summary>
        public static int GetMeshCase(bool b0, bool b1, bool b2, bool b3)
        {
            int r = 0;
            if (b0) r |= 1;   // V0 BL → bit 0
            if (b1) r |= 2;   // V1 BR → bit 1
            if (b2) r |= 4;   // V2 TR → bit 2
            if (b3) r |= 8;   // V3 TL → bit 3
            return r;
        }

        /// <summary>
        /// 给定格子网格（cells），返回格子 (cx,cz) 的 4-bit case。
        /// 顶点状态：vertex (vx,vz) 在地板内 = 其周围至少有一个激活格子。
        /// </summary>
        public static int GetCellCase(bool[,] cells, int cx, int cz)
        {
            return GetMeshCase(
                IsVertexInside(cells, cx,     cz    ),   // V0 BL
                IsVertexInside(cells, cx + 1, cz    ),   // V1 BR
                IsVertexInside(cells, cx + 1, cz + 1),  // V2 TR
                IsVertexInside(cells, cx,     cz + 1)   // V3 TL
            );
        }

        /// <summary>顶点 (vx,vz) 是否在地板区域内：周围至少一个格子激活。</summary>
        public static bool IsVertexInside(bool[,] cells, int vx, int vz)
        {
            int W = cells.GetLength(0), D = cells.GetLength(1);
            return (vx > 0 && vz > 0     && cells[vx - 1, vz - 1])   // SW cell
                || (vx < W && vz > 0     && cells[vx,     vz - 1])   // SE cell
                || (vx > 0 && vz < D     && cells[vx - 1, vz    ])   // NW cell
                || (vx < W && vz < D     && cells[vx,     vz    ]);  // NE cell
        }

        // ── Canonical 归约（D4 水平，8 变换 → 6 canonical）────────────────────

        static int[]        s_canonIdx;
        static Quaternion[] s_canonRot;
        static bool[]       s_canonFlip;
        static int[]        s_canonList;

        public static int CanonicalCount { get; private set; } // = 6

        public static void EnsureLookup()
        {
            if (s_canonIdx != null) return;

            var maskToMin  = new int[16];
            var maskToRot  = new int[16];
            var maskToFlip = new bool[16];
            var canonSet   = new SortedSet<int>();

            for (int m = 0; m < 16; m++)
            {
                int  minM     = m;
                int  bestR    = 0;
                bool bestFlip = false;

                int cur = m;
                for (int r = 1; r < 4; r++)
                {
                    cur = Rot90(cur);
                    if (cur < minM) { minM = cur; bestR = r; bestFlip = false; }
                }
                cur = FlipX(m);
                for (int r = 0; r < 4; r++)
                {
                    if (r > 0) cur = Rot90(cur);
                    if (cur < minM) { minM = cur; bestR = r; bestFlip = true; }
                }

                maskToMin[m]  = minM;
                maskToRot[m]  = bestR;
                maskToFlip[m] = bestFlip;
                canonSet.Add(minM);
            }

            var canonList = canonSet.ToArray(); // {0,1,3,5,7,15}
            CanonicalCount = canonList.Length;
            s_canonList    = canonList;
            var idxOf = new Dictionary<int, int>(canonList.Length);
            for (int i = 0; i < canonList.Length; i++) idxOf[canonList[i]] = i;

            s_canonIdx  = new int[16];
            s_canonRot  = new Quaternion[16];
            s_canonFlip = new bool[16];
            for (int m = 0; m < 16; m++)
            {
                s_canonIdx[m]  = idxOf[maskToMin[m]];
                s_canonRot[m]  = Quaternion.Euler(0, maskToRot[m] * 90f, 0);
                s_canonFlip[m] = maskToFlip[m];
            }
        }

        /// <summary>4-bit case → (canonical 序号 0-5, Y轴旋转, 是否X镜像)。</summary>
        public static (int canonIdx, Quaternion rot, bool flip) GetCanonical(int mask)
        {
            EnsureLookup();
            return (s_canonIdx[mask & 0xF], s_canonRot[mask & 0xF], s_canonFlip[mask & 0xF]);
        }

        /// <summary>canonical 序号 → 代表 mask（{0,1,3,5,7,15} 之一）。</summary>
        public static int GetCanonicalMask(int canonIdx)
        {
            EnsureLookup();
            return canonIdx >= 0 && canonIdx < s_canonList.Length ? s_canonList[canonIdx] : 0;
        }

        // ── 4-bit 角点置换（BL→BR→TR→TL→BL = N→E→S→W→N 同构）───────────────

        /// <summary>绕 Y 轴 90° CW 旋转角点（BL→BR→TR→TL→BL）。</summary>
        public static int Rot90(int mask)
        {
            int r = 0;
            if ((mask & 1) != 0) r |= 2;  // BL → BR
            if ((mask & 2) != 0) r |= 4;  // BR → TR
            if ((mask & 4) != 0) r |= 8;  // TR → TL
            if ((mask & 8) != 0) r |= 1;  // TL → BL
            return r;
        }

        /// <summary>左右镜像（BL↔BR, TL↔TR）。</summary>
        public static int FlipX(int mask)
        {
            int r = 0;
            if ((mask & 1) != 0) r |= 2;  // BL → BR
            if ((mask & 2) != 0) r |= 1;  // BR → BL
            if ((mask & 4) != 0) r |= 8;  // TR → TL
            if ((mask & 8) != 0) r |= 4;  // TL → TR
            return r;
        }
    }
}

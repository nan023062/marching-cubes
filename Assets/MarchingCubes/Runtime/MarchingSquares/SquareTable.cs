using System.Collections.Generic;
using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MarchingSquares 地板砖查表（per-cell 邻居系统）。
    ///
    /// 4-bit mask：bit0=N, bit1=E, bit2=S, bit3=W（该格的4个基本方向邻居是否激活）。
    /// 16 种原始配置，D4 水平对称（4旋转 + X镜像 = 8变换）→ 6 canonical。
    ///
    /// 6 个 canonical 地板砖形态：
    ///   0  (0b0000) 孤立砖  —— 四面暴露（独立地板）
    ///   1  (0b0001) 末端砖  —— 三面暴露（走廊尽头）
    ///   3  (0b0011) 转角砖  —— 两面相邻暴露（L形角落）
    ///   5  (0b0101) 通道砖  —— 两面对向暴露（直线走廊）
    ///   7  (0b0111) T形砖   —— 一面暴露（T字交叉）
    ///   15 (0b1111) 内部砖  —— 全部连接（房间内部）
    ///
    /// 对称群：D4 水平（4旋转 + X镜像，含 FlipX∘Rot180=FlipZ）。
    /// 不含 FlipY（地板上/下表面不对称）。
    /// </summary>
    public static class SquareTable
    {
        // ── Canonical 查表 ────────────────────────────────────────────────────

        static int[]        s_canonIdx;   // [16]: mask → canonical index (0-5)
        static Quaternion[] s_canonRot;   // [16]: mask → Y轴旋转
        static bool[]       s_canonFlip;  // [16]: mask → 是否需要 X 镜像
        static int[]        s_canonList;  // [6]:  canonical index → canonical mask

        public static int CanonicalCount { get; private set; } // = 6

        /// <summary>强制初始化查表（通常由 GetCanonical 自动触发）。</summary>
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

                // 4 旋转
                int cur = m;
                for (int r = 1; r < 4; r++)
                {
                    cur = Rot90(cur);
                    if (cur < minM) { minM = cur; bestR = r; bestFlip = false; }
                }

                // FlipX 后再 4 旋转（FlipX∘Rot180 = FlipZ，覆盖所有水平镜像轴）
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

            // 应为 {0, 1, 3, 5, 7, 15} = 6 canonical
            var canonList = canonSet.ToArray();
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

        /// <summary>4-bit 邻居 mask → (canonical 序号 0-5, Y轴旋转, 是否X镜像)。</summary>
        public static (int canonIdx, Quaternion rot, bool flip) GetCanonical(int mask)
        {
            EnsureLookup();
            mask &= 0xF;
            return (s_canonIdx[mask], s_canonRot[mask], s_canonFlip[mask]);
        }

        /// <summary>canonical 序号 → 对应的代表 4-bit mask。</summary>
        public static int GetCanonicalMask(int canonIdx)
        {
            EnsureLookup();
            return canonIdx >= 0 && canonIdx < s_canonList.Length ? s_canonList[canonIdx] : 0;
        }

        /// <summary>计算某格的 4-bit 邻居 mask（N/E/S/W 邻格是否激活）。</summary>
        public static int GetNeighborMask(bool[,] grid, int x, int z)
        {
            int W = grid.GetLength(0), D = grid.GetLength(1);
            int mask = 0;
            if (z + 1 < D && grid[x,     z + 1]) mask |= 1; // North
            if (x + 1 < W && grid[x + 1, z    ]) mask |= 2; // East
            if (z - 1 >= 0 && grid[x,     z - 1]) mask |= 4; // South
            if (x - 1 >= 0 && grid[x - 1, z    ]) mask |= 8; // West
            return mask;
        }

        // ── 4-bit 置换 ────────────────────────────────────────────────────────

        /// <summary>绕 Y 轴 90° CW 旋转（N→E→S→W→N）。</summary>
        public static int Rot90(int mask)
        {
            int r = 0;
            if ((mask & 1) != 0) r |= 2;  // N → E
            if ((mask & 2) != 0) r |= 4;  // E → S
            if ((mask & 4) != 0) r |= 8;  // S → W
            if ((mask & 8) != 0) r |= 1;  // W → N
            return r;
        }

        /// <summary>X→−X 水平镜像（East↔West，N/S 不变）。</summary>
        public static int FlipX(int mask)
        {
            int r = 0;
            if ((mask & 1) != 0) r |= 1;  // N unchanged
            if ((mask & 2) != 0) r |= 8;  // E → W
            if ((mask & 4) != 0) r |= 4;  // S unchanged
            if ((mask & 8) != 0) r |= 2;  // W → E
            return r;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MarchingQuads
{
    /// <summary>
    /// MarchingQuads 围墙/围栏查表（per-vertex 面板连接系统）。
    ///
    /// 4-bit mask：bit0=N, bit1=E, bit2=S, bit3=W（该顶点处 4 条相邻面板是否激活）。
    /// 16 种原始配置，D4 水平对称（4旋转 + X镜像 = 8变换）→ 6 canonical。
    ///
    /// 6 个 canonical 顶点连接形态：
    ///   0  (0b0000) 孤立  —— 无面板，无连接件
    ///   1  (0b0001) 末端  —— 单面板端头盖
    ///   3  (0b0011) 转角  —— L 形竖条（两相邻面板 90°）
    ///   5  (0b0101) 通道  —— 直通竖条（两对向面板 180°）
    ///   7  (0b0111) T 形  —— T 字连接竖条
    ///   15 (0b1111) 十字  —— + 字连接竖条
    ///
    /// 数据：xPanels[vx, cz]（东西隔断）+ zPanels[cx, vz]（南北隔断）。
    /// 对称群：D4 水平（同 MarchingSquares），不含 FlipY（面板上/下端造型不对称）。
    /// </summary>
    public static class QuadTable
    {
        // ── Canonical 查表 ────────────────────────────────────────────────────

        static int[]        s_canonIdx;
        static Quaternion[] s_canonRot;
        static bool[]       s_canonFlip;
        static int[]        s_canonList;

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

            var canonList = canonSet.ToArray(); // {0, 1, 3, 5, 7, 15}
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

        /// <summary>4-bit 面板 mask → (canonical 序号 0-5, Y轴旋转, 是否X镜像)。</summary>
        public static (int canonIdx, Quaternion rot, bool flip) GetCanonical(int mask)
        {
            EnsureLookup();
            mask &= 0xF;
            return (s_canonIdx[mask], s_canonRot[mask], s_canonFlip[mask]);
        }

        /// <summary>canonical 序号 → 代表 mask。</summary>
        public static int GetCanonicalMask(int canonIdx)
        {
            EnsureLookup();
            return canonIdx >= 0 && canonIdx < s_canonList.Length ? s_canonList[canonIdx] : 0;
        }

        /// <summary>
        /// 计算顶点 (vx, vz) 处的 4-bit 面板激活 mask。
        /// xPanels[vx, cz]：在 x=vx 处跨越 cz→cz+1 的东西向面板。
        /// zPanels[cx, vz]：在 z=vz 处跨越 cx→cx+1 的南北向面板。
        /// </summary>
        public static int GetVertexMask(bool[,] xPanels, bool[,] zPanels, int vx, int vz)
        {
            int Xw = xPanels.GetLength(0), Xd = xPanels.GetLength(1); // [W+1][D]
            int Zw = zPanels.GetLength(0), Zd = zPanels.GetLength(1); // [W][D+1]

            int mask = 0;
            if (vz     < Xd && vx >= 0 && vx < Xw && xPanels[vx,   vz    ]) mask |= 1; // North
            if (vx     < Zw && vz >= 0 && vz < Zd && zPanels[vx,   vz    ]) mask |= 2; // East
            if (vz - 1 >= 0 && vx >= 0 && vx < Xw && xPanels[vx,   vz - 1]) mask |= 4; // South
            if (vx - 1 >= 0 && vz >= 0 && vz < Zd && zPanels[vx - 1, vz  ]) mask |= 8; // West
            return mask;
        }

        // ── 4-bit 置换（与 MarchingSquares 完全一致）────────────────────────

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

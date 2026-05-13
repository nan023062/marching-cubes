using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MarchingQuads
{
    /// <summary>
    /// MarchingQuads 围墙/围栏查表（per-vertex 面板连接系统，含高差支持）。
    ///
    /// Case 编码（10-bit，0~1022）：
    ///   bits 0-1  r_V  顶点自身高于 base 的量，∈ {0,1,2}
    ///   bits 2-3  N    North 方向：0=absent, 1=r0, 2=r1, 3=r2
    ///   bits 4-5  E    East 方向
    ///   bits 6-7  S    South 方向
    ///   bits 8-9  W    West 方向
    ///
    /// base = min(h_V, 所有激活邻居高度)，r_X = h_X - base ∈ {0,1,2}。
    /// 约束：相邻顶点高差不超过 2（调用方保证）。
    ///
    /// D4 水平对称（4旋转 + X镜像）→ 123 canonical：
    ///   r_V=0: 55 canonical（含 6 个 flat 原型 {0,4,20,68,84,340}）
    ///   r_V=1: 34 canonical
    ///   r_V=2: 34 canonical
    /// </summary>
    public static class QuadTable
    {
        public const int CodeCount = 1023; // 10-bit: max valid code = 2|(3<<2)|(3<<4)|(3<<6)|(3<<8) = 1022

        // ── Canonical 查表 ────────────────────────────────────────────────────

        static int[]        s_canonIdx;
        static Quaternion[] s_canonRot;
        static bool[]       s_canonFlip;
        static int[]        s_canonList;

        public static int CanonicalCount { get; private set; } // = 123

        public static void EnsureLookup()
        {
            if (s_canonIdx != null) return;

            var maskToMin  = new int[CodeCount];
            var maskToRot  = new int[CodeCount];
            var maskToFlip = new bool[CodeCount];
            var canonSet   = new SortedSet<int>();

            for (int m = 0; m < CodeCount; m++)
            {
                if (!IsValidCase(m)) continue;

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

            var canonList = canonSet.ToArray();
            CanonicalCount = canonList.Length;
            s_canonList    = canonList;
            var idxOf = new Dictionary<int, int>(canonList.Length);
            for (int i = 0; i < canonList.Length; i++) idxOf[canonList[i]] = i;

            s_canonIdx  = new int[CodeCount];
            s_canonRot  = new Quaternion[CodeCount];
            s_canonFlip = new bool[CodeCount];
            for (int m = 0; m < CodeCount; m++)
            {
                if (!IsValidCase(m)) continue;
                s_canonIdx[m]  = idxOf[maskToMin[m]];
                s_canonRot[m]  = Quaternion.Euler(0, maskToRot[m] * 90f, 0);
                s_canonFlip[m] = maskToFlip[m];
            }
        }

        /// <summary>caseCode → (canonical 序号 0-122, Y轴旋转, 是否X镜像)。</summary>
        public static (int canonIdx, Quaternion rot, bool flip) GetCanonical(int code)
        {
            EnsureLookup();
            return (s_canonIdx[code], s_canonRot[code], s_canonFlip[code]);
        }

        /// <summary>canonical 序号 → 代表 caseCode。</summary>
        public static int GetCanonicalCode(int canonIdx)
        {
            EnsureLookup();
            return canonIdx >= 0 && canonIdx < s_canonList.Length ? s_canonList[canonIdx] : 0;
        }

        // ── Case 计算 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 计算顶点 (vx,vz) 处的 caseCode（含高差）。
        /// heights[vx,vz]：整数顶点高度，尺寸 (W+1)×(D+1)，与面板数组同顶点坐标系。
        /// xPanels[vx,cz]：在 x=vx 处跨越 cz→cz+1 的面板（连接顶点 vz 与 vz+1）。
        /// zPanels[cx,vz]：在 z=vz 处跨越 cx→cx+1 的面板（连接顶点 vx 与 vx+1）。
        /// </summary>
        public static int GetVertexCase(bool[,] xPanels, bool[,] zPanels, int[,] heights, int vx, int vz)
        {
            int Xw = xPanels.GetLength(0), Xd = xPanels.GetLength(1);
            int Zw = zPanels.GetLength(0), Zd = zPanels.GetLength(1);

            bool hasN = vz     < Xd && vx >= 0 && vx < Xw && xPanels[vx,      vz    ];
            bool hasE = vx     < Zw && vz >= 0 && vz < Zd && zPanels[vx,      vz    ];
            bool hasS = vz - 1 >= 0 && vx >= 0 && vx < Xw && xPanels[vx,      vz - 1];
            bool hasW = vx - 1 >= 0 && vz >= 0 && vz < Zd && zPanels[vx - 1,  vz    ];

            int hV = heights[vx, vz];
            int hN = hasN ? heights[vx,     vz + 1] : int.MaxValue;
            int hE = hasE ? heights[vx + 1, vz    ] : int.MaxValue;
            int hS = hasS ? heights[vx,     vz - 1] : int.MaxValue;
            int hW = hasW ? heights[vx - 1, vz    ] : int.MaxValue;

            int baseH = hV;
            if (hasN && hN < baseH) baseH = hN;
            if (hasE && hE < baseH) baseH = hE;
            if (hasS && hS < baseH) baseH = hS;
            if (hasW && hW < baseH) baseH = hW;

            return Encode(
                hV - baseH,
                hasN ? hN - baseH : -1,
                hasE ? hE - baseH : -1,
                hasS ? hS - baseH : -1,
                hasW ? hW - baseH : -1);
        }

        // ── 编码 / 解码 ──────────────────────────────────────────────────────

        /// <summary>
        /// 编码 (r_V, r_N, r_E, r_S, r_W) → caseCode。
        /// r_X ∈ {0,1,2}；absent 传 -1（编码为 field=0；激活 r=0 编码为 field=1）。
        /// </summary>
        public static int Encode(int rV, int rN, int rE, int rS, int rW)
            => rV
            | (DirField(rN) << 2)
            | (DirField(rE) << 4)
            | (DirField(rS) << 6)
            | (DirField(rW) << 8);

        /// <summary>caseCode 是否为有效配置：r_V∈{0,1,2} 且 min(r_V, 激活方向r) == 0。</summary>
        public static bool IsValidCase(int code)
        {
            if (code < 0 || code >= CodeCount) return false;
            int rV = code & 3;
            if (rV == 3) return false;

            int rN = DirR((code >> 2) & 3);
            int rE = DirR((code >> 4) & 3);
            int rS = DirR((code >> 6) & 3);
            int rW = DirR((code >> 8) & 3);

            int minR = rV;
            if (rN >= 0 && rN < minR) minR = rN;
            if (rE >= 0 && rE < minR) minR = rE;
            if (rS >= 0 && rS < minR) minR = rS;
            if (rW >= 0 && rW < minR) minR = rW;
            return minR == 0;
        }

        /// <summary>从 caseCode 解码各方向 r 值（-1=absent）。</summary>
        public static (int rV, int rN, int rE, int rS, int rW) Decode(int code)
            => (code & 3,
                DirR((code >> 2) & 3),
                DirR((code >> 4) & 3),
                DirR((code >> 6) & 3),
                DirR((code >> 8) & 3));

        // ── D4 置换 ──────────────────────────────────────────────────────────

        /// <summary>绕 Y 轴 90° CW 旋转（N→E→S→W→N），r_V 不变。</summary>
        public static int Rot90(int code)
        {
            int rV = code & 3;
            int rN = (code >> 2) & 3;
            int rE = (code >> 4) & 3;
            int rS = (code >> 6) & 3;
            int rW = (code >> 8) & 3;
            return rV | (rW << 2) | (rN << 4) | (rE << 6) | (rS << 8);
        }

        /// <summary>X→−X 水平镜像（East↔West），r_V/N/S 不变。</summary>
        public static int FlipX(int code)
        {
            int rV = code & 3;
            int rN = (code >> 2) & 3;
            int rE = (code >> 4) & 3;
            int rS = (code >> 6) & 3;
            int rW = (code >> 8) & 3;
            return rV | (rN << 2) | (rW << 4) | (rS << 6) | (rE << 8);
        }

        // ── 内部辅助 ─────────────────────────────────────────────────────────

        static int DirField(int r) => r < 0 ? 0 : r + 1;   // absent→0, r0→1, r1→2, r2→3
        static int DirR(int field) => field == 0 ? -1 : field - 1; // 0→-1(absent), 1→0, 2→1, 3→2
    }
}

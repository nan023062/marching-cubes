using System.Collections.Generic;
using UnityEngine;

namespace MarchingEdges
{
    /// <summary>
    /// Marching Edges 几何表与 canonical 归约查表。
    ///
    /// 坐标约定：cube 中心在原点，棱长 = 1，half-extent = 0.5。
    /// 12 个面槽对应 cube 3 条中心平面与 12 条棱中点的交点，
    /// 每槽为一个 0.5×0.5 的薄面（从原点延伸到棱中点）。
    ///
    /// 面槽 bit 编码：
    ///   bits 0-3  X组（YZ平面 x=0）：X0=+Y+Z, X1=-Y+Z, X2=-Y-Z, X3=+Y-Z
    ///   bits 4-7  Y组（XZ平面 y=0）：Y0=+X+Z, Y1=-X+Z, Y2=-X-Z, Y3=+X-Z
    ///   bits 8-11 Z组（XY平面 z=0）：Z0=+X+Y, Z1=-X+Y, Z2=-X-Y, Z3=+X-Y
    ///
    /// 对称群：D4 水平（4旋转 + X镜像 = 8变换）→ 618 canonical
    ///   X组 ≡ Z组（Rot90 互换），FlipX∘Rot180 = FlipZ，上下不对称故不含 FlipY。
    /// </summary>
    public static class EdgeTable
    {
        // ── 面槽几何（cube-local，中心=原点，每槽4顶点，v0 始终为原点）────────

        /// <summary>12 个面槽各自的 4 个顶点坐标（顺序：v0=原点，逆时针正面向外）。</summary>
        public static readonly Vector3[][] SlotVerts = new Vector3[12][]
        {
            // ── X 组（YZ 平面, x=0, 法线=+X）────────────────────────────────
            new[] { V(0,0,0), V(0,.5f,0), V(0,.5f,.5f), V(0,0,.5f) },       // X0 bit0 +Y+Z
            new[] { V(0,-.5f,0), V(0,0,0), V(0,0,.5f), V(0,-.5f,.5f) },     // X1 bit1 -Y+Z
            new[] { V(0,-.5f,-.5f), V(0,0,-.5f), V(0,0,0), V(0,-.5f,0) },   // X2 bit2 -Y-Z
            new[] { V(0,0,-.5f), V(0,.5f,-.5f), V(0,.5f,0), V(0,0,0) },     // X3 bit3 +Y-Z

            // ── Y 组（XZ 平面, y=0, 法线=+Y）────────────────────────────────
            new[] { V(0,0,0), V(.5f,0,0), V(.5f,0,.5f), V(0,0,.5f) },       // Y0 bit4 +X+Z
            new[] { V(-.5f,0,0), V(0,0,0), V(0,0,.5f), V(-.5f,0,.5f) },     // Y1 bit5 -X+Z
            new[] { V(-.5f,0,-.5f), V(0,0,-.5f), V(0,0,0), V(-.5f,0,0) },   // Y2 bit6 -X-Z
            new[] { V(0,0,-.5f), V(.5f,0,-.5f), V(.5f,0,0), V(0,0,0) },     // Y3 bit7 +X-Z

            // ── Z 组（XY 平面, z=0, 法线=+Z）────────────────────────────────
            new[] { V(0,0,0), V(.5f,0,0), V(.5f,.5f,0), V(0,.5f,0) },       // Z0 bit8  +X+Y
            new[] { V(-.5f,0,0), V(0,0,0), V(0,.5f,0), V(-.5f,.5f,0) },     // Z1 bit9  -X+Y
            new[] { V(-.5f,-.5f,0), V(0,-.5f,0), V(0,0,0), V(-.5f,0,0) },   // Z2 bit10 -X-Y
            new[] { V(0,-.5f,0), V(.5f,-.5f,0), V(.5f,0,0), V(0,0,0) },     // Z3 bit11 +X-Y
        };

        /// <summary>各面槽的正面法线（X组=right，Y组=up，Z组=forward）。</summary>
        public static readonly Vector3[] SlotNormals =
        {
            Vector3.right, Vector3.right, Vector3.right, Vector3.right,     // X 组
            Vector3.up,    Vector3.up,    Vector3.up,    Vector3.up,         // Y 组
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, // Z 组
        };

        // ── Canonical 归约查表（D4 水平，8 变换）──────────────────────────────

        static int[]        s_canonIdx;
        static Quaternion[] s_canonRot;
        static bool[]       s_canonFlip;
        static int[]        s_canonList;

        public static int CanonicalCount { get; private set; }

        /// <summary>强制初始化查表（通常由 GetCanonical 自动触发）。</summary>
        public static void EnsureLookup()
        {
            if (s_canonIdx != null) return;

            var maskToMin  = new int[4096];
            var maskToRot  = new int[4096];
            var maskToFlip = new bool[4096];
            var canonSet   = new SortedSet<int>();

            for (int m = 0; m < 4096; m++)
            {
                int  minM     = m;
                int  bestR    = 0;
                bool bestFlip = false;

                // 4 旋转（无翻转）
                int cur = m;
                for (int r = 1; r < 4; r++)
                {
                    cur = Rotate90CW(cur);
                    if (cur < minM) { minM = cur; bestR = r; bestFlip = false; }
                }

                // FlipX 后再 4 旋转（水平镜像 × 旋转；FlipX∘Rot180 = FlipZ）
                cur = FlipX(m);
                for (int r = 0; r < 4; r++)
                {
                    if (r > 0) cur = Rotate90CW(cur);
                    if (cur < minM) { minM = cur; bestR = r; bestFlip = true; }
                }

                maskToMin[m]  = minM;
                maskToRot[m]  = bestR;
                maskToFlip[m] = bestFlip;
                canonSet.Add(minM);
            }

            var canonList = canonSet.ToArray();
            CanonicalCount = canonList.Length; // 618（D4 水平对称）
            s_canonList    = canonList;
            var idxOf = new Dictionary<int, int>(canonList.Length);
            for (int i = 0; i < canonList.Length; i++) idxOf[canonList[i]] = i;

            s_canonIdx  = new int[4096];
            s_canonRot  = new Quaternion[4096];
            s_canonFlip = new bool[4096];
            for (int m = 0; m < 4096; m++)
            {
                s_canonIdx[m]  = idxOf[maskToMin[m]];
                s_canonRot[m]  = Quaternion.Euler(0, maskToRot[m] * 90f, 0);
                s_canonFlip[m] = maskToFlip[m];
            }
        }

        /// <summary>任意 12-bit mask → (canonical 序号, Y轴旋转, 是否X镜像)。</summary>
        public static (int canonIdx, Quaternion rot, bool flip) GetCanonical(int mask)
        {
            EnsureLookup();
            mask &= 0xFFF;
            return (s_canonIdx[mask], s_canonRot[mask], s_canonFlip[mask]);
        }

        /// <summary>canonical 序号 → 对应的原始 12-bit mask。</summary>
        public static int GetCanonicalMask(int canonIdx)
        {
            EnsureLookup();
            return canonIdx >= 0 && canonIdx < s_canonList.Length ? s_canonList[canonIdx] : 0;
        }

        // ── 12-bit 置换 ───────────────────────────────────────────────────────

        /// <summary>绕 Y 轴 90° CW 旋转（从+Y向下看顺时针）的 12-bit 置换。</summary>
        public static int Rotate90CW(int mask)
        {
            int r = 0;
            if ((mask &    1) != 0) r |= 1 << 8;   // X0 → Z0
            if ((mask &    2) != 0) r |= 1 << 11;  // X1 → Z3
            if ((mask &    4) != 0) r |= 1 << 10;  // X2 → Z2
            if ((mask &    8) != 0) r |= 1 << 9;   // X3 → Z1
            if ((mask &   16) != 0) r |= 1 << 7;   // Y0 → Y3
            if ((mask &   32) != 0) r |= 1 << 4;   // Y1 → Y0
            if ((mask &   64) != 0) r |= 1 << 5;   // Y2 → Y1
            if ((mask &  128) != 0) r |= 1 << 6;   // Y3 → Y2
            if ((mask &  256) != 0) r |= 1 << 3;   // Z0 → X3
            if ((mask &  512) != 0) r |= 1 << 0;   // Z1 → X0
            if ((mask & 1024) != 0) r |= 1 << 1;   // Z2 → X1
            if ((mask & 2048) != 0) r |= 1 << 2;   // Z3 → X2
            return r;
        }

        /// <summary>X→−X 水平镜像（X组不变，Y/Z组左右象限互换）。
        /// FlipX∘Rot180 = FlipZ；FlipX∘Rot90/270 为对角镜像。</summary>
        public static int FlipX(int mask)
        {
            int r = 0;
            r |= (mask & 0xF);                                      // X 组不变
            if ((mask &   16) != 0) r |= 32;    // Y0 → Y1
            if ((mask &   32) != 0) r |= 16;    // Y1 → Y0
            if ((mask &   64) != 0) r |= 128;   // Y2 → Y3
            if ((mask &  128) != 0) r |= 64;    // Y3 → Y2
            if ((mask &  256) != 0) r |= 512;   // Z0 → Z1
            if ((mask &  512) != 0) r |= 256;   // Z1 → Z0
            if ((mask & 1024) != 0) r |= 2048;  // Z2 → Z3
            if ((mask & 2048) != 0) r |= 1024;  // Z3 → Z2
            return r;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}

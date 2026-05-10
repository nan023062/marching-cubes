
namespace MarchingSquares
{
    /// <summary>
    /// Marching Squares 全局静态映射表（类比 CubeTable）。
    ///
    /// 两类组合映射：
    ///   1. Mesh 组合映射 — 四角高差（高/低）→ 16 种几何 case
    ///   2. 纹理组合映射 — 四角 terrainType → 16 种 overlay 混合 case
    ///
    /// 角点编号（unit quad，XZ 平面）：
    ///   V3(TL) ─── V2(TR)       bit mask：bit_i = 1 表示 Vi 高于 base（Mesh）
    ///     │               │                         或 Vi 属于 overlay 类型（纹理）
    ///   V0(BL) ─── V1(BR)
    /// </summary>
    public static class TileTable
    {
        public const int CornerCount = 4;
        public const int CaseCount   = 16;

        // ── 悬崖查表 ─────────────────────────────────────────────────────────

        public const int CliffCaseCount = 16;

        /// <summary>
        /// D4 旋转映射：每个悬崖 case → (规范 case, 旋转次数)。
        /// 旋转单位 = 90° CW（Unity Euler(0, 90*n, 0)）。
        /// 规范 case 集合：{1, 3, 5, 7, 15}，只需这 5 个 FBX。
        /// Mesh 以格子 XZ 中心为原点，旋转后自动覆盖全部 16 种情形。
        /// </summary>
        public static readonly (int canonical, int rotCount)[] CliffD4Map =
        {
            (0,  0),  // 0:  无悬崖
            (1,  0),  // 1:  南墙
            (1,  1),  // 2:  东墙  = canonical 1 旋转 1 次
            (3,  0),  // 3:  南+东
            (1,  2),  // 4:  北墙  = canonical 1 旋转 2 次
            (5,  0),  // 5:  南+北（对穿）
            (3,  1),  // 6:  东+北 = canonical 3 旋转 1 次
            (7,  0),  // 7:  南+东+北
            (1,  3),  // 8:  西墙  = canonical 1 旋转 3 次
            (3,  3),  // 9:  南+西 = canonical 3 旋转 3 次
            (5,  1),  // 10: 东+西 = canonical 5 旋转 1 次
            (7,  3),  // 11: 南+东+西 = canonical 7 旋转 3 次
            (3,  2),  // 12: 北+西 = canonical 3 旋转 2 次
            (7,  2),  // 13: 南+北+西 = canonical 7 旋转 2 次
            (7,  1),  // 14: 东+北+西 = canonical 7 旋转 1 次
            (15, 0),  // 15: 四面（孤岛）
        };

        /// <summary>5 个规范 case，对应 mq_cliff_1/3/5/7/15.fbx。</summary>
        public static readonly int[] CliffCanonicalCases = { 1, 3, 5, 7, 15 };

        // ── 角点坐标 ─────────────────────────────────────────────────────────

        /// <summary>四个角点的 (x, z) 坐标（unit quad [0,1]×[0,1]）。</summary>
        public static readonly (int x, int z)[] Corners =
        {
            (0, 0),  // V0 BL
            (1, 0),  // V1 BR
            (1, 1),  // V2 TR
            (0, 1),  // V3 TL
        };

        // ── Mesh 组合映射 ─────────────────────────────────────────────────────

        /// <summary>
        /// 根据四角高度计算 Mesh case index 和 base 高度。
        /// h0=V0(BL), h1=V1(BR), h2=V2(TR), h3=V3(TL)。
        /// base = 四角最小高度，bit_i=1 表示该角点高于 base。
        /// </summary>
        /// <summary>
        /// 返回值 0-14：标准 case（同格四角高差 ≤ 1）。
        /// 返回值 15-18：对角高差 == 2 的特殊 case，需要独立 mesh：
        ///   15 = V0 最高(+2)，V2 为 base；16 = V1 最高，V3 为 base；
        ///   17 = V2 最高，V0 为 base；  18 = V3 最高，V1 为 base。
        /// </summary>
        public static int GetMeshCase(int h0, int h1, int h2, int h3, out int baseH)
        {
            baseH = h0;
            if (h1 < baseH) baseH = h1;
            if (h2 < baseH) baseH = h2;
            if (h3 < baseH) baseH = h3;

            int r0 = h0 - baseH, r1 = h1 - baseH, r2 = h2 - baseH, r3 = h3 - baseH;

            // 对角点高差 == 2：4 方向约束允许此情况，需要专用 mesh
            if (r0 == 2) return 15;
            if (r1 == 2) return 16;
            if (r2 == 2) return 17;
            if (r3 == 2) return 18;

            return (r0 > 0 ? 1 : 0) | (r1 > 0 ? 2 : 0) | (r2 > 0 ? 4 : 0) | (r3 > 0 ? 8 : 0);
        }

        // ── 纹理组合映射 ──────────────────────────────────────────────────────

        /// <summary>
        /// 根据四角 terrainType 计算纹理 overlay case index。
        /// overlayType = 本格需要混合的 overlay 地形类型（大于 baseType 的类型）。
        /// bit_i=1 表示 Vi 的 terrainType >= overlayType（参与混合）。
        /// 结果用于查询纹理 atlas（4×4 共 16 种混合图案）。
        /// </summary>
        public static int GetTextureCase(byte t0, byte t1, byte t2, byte t3, byte overlayType)
        {
            int ci = 0;
            if (t0 >= overlayType) ci |= 1;
            if (t1 >= overlayType) ci |= 2;
            if (t2 >= overlayType) ci |= 4;
            if (t3 >= overlayType) ci |= 8;
            return ci;
        }

        /// <summary>
        /// 从四角 terrainType 提取 baseType（最小值）和最多 3 层 overlayType。
        /// 返回 overlay 层数（0~3）。
        /// </summary>
        public static int GetTerrainLayers(byte t0, byte t1, byte t2, byte t3,
                                            out byte baseType,
                                            out byte overlay1, out byte overlay2, out byte overlay3)
        {
            baseType = t0;
            if (t1 < baseType) baseType = t1;
            if (t2 < baseType) baseType = t2;
            if (t3 < baseType) baseType = t3;

            overlay1 = overlay2 = overlay3 = 0;
            int count = 0;
            for (byte t = (byte)(baseType + 1); t < 5; t++)
            {
                if (t == t0 || t == t1 || t == t2 || t == t3)
                {
                    if      (count == 0) overlay1 = t;
                    else if (count == 1) overlay2 = t;
                    else                 overlay3 = t;
                    count++;
                }
            }
            return count;
        }

        // ── Atlas 美术约定（terrain_overlay.asset 4×4 子格排布）────────────────
        //
        // atlas 子格按 TileTable 标准编码排布（与角点 V0~V3 序号直接对应）：
        //
        //   bit 0 = V0(BL) → mask 贡献 1
        //   bit 1 = V1(BR) → mask 贡献 2
        //   bit 2 = V2(TR) → mask 贡献 4
        //   bit 3 = V3(TL) → mask 贡献 8
        //
        //   atlas_idx = Σ (Vi 标记 ? 1<<i : 0)            ← 4 个点 mask 直接相加
        //   子格位置: col = atlas_idx % 4, row = atlas_idx / 4 (Unity UV，row=0 在底)
        //
        // 例：V0+V2 标记 → idx = 1 + 4 = 5 → 子格 (col=1, row=1)
        //
        // 任何端（C# runtime / Python 离线 / shader）的「4 角 → atlas idx」一律
        // 直接位运算，无须查表无须旋转。

        /// <summary>4 角 mask + 单 bit type → atlas case_idx (0~15)，TileTable 标准编码。</summary>
        public static int GetAtlasCase(byte mBL, byte mBR, byte mTR, byte mTL, int bit)
            =>  ((mBL >> bit) & 1)
            | (((mBR >> bit) & 1) << 1)
            | (((mTR >> bit) & 1) << 2)
            | (((mTL >> bit) & 1) << 3);

        /// <summary>4 角是否参与 (true/false) → atlas case_idx (0~15)。离线工具友好。</summary>
        public static int GetAtlasCase(bool BL, bool BR, bool TR, bool TL)
            =>  (BL ? 1 : 0)
            |   (BR ? 1 : 0) << 1
            |   (TR ? 1 : 0) << 2
            |   (TL ? 1 : 0) << 3;

        /// <summary>atlas case_idx (0~15) → 子格 (col, row)（Unity UV，row=0 在底）。</summary>
        public static (int col, int row) GetAtlasCell(int atlasCase)
            => (atlasCase & 3, atlasCase >> 2);
    }
}

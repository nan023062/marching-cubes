
namespace MarchingSquareTerrain
{
    /// <summary>
    /// Marching Squares 全局静态映射表（类比 CubeTable）。
    ///
    /// 两类组合映射：
    ///   1. Mesh 组合映射 — 四角高度 → base-3 编码 case_idx ∈ [0,80]（65 真实几何 + 16 死槽）
    ///   2. 纹理组合映射 — 四角 terrainType / mask → 16 种 atlas overlay case
    ///
    /// 角点编号（unit quad，XZ 平面）：
    ///   V3(TL) ─── V2(TR)       Mesh: r_i = h_i - min(h0..h3) ∈ {0,1,2}
    ///     │               │       Atlas: bit_i = 该角是否参与本 layer
    ///   V0(BL) ─── V1(BR)
    /// </summary>
    public static class TileTable
    {
        public const int CornerCount = 4;

        /// <summary>Atlas overlay 的 4-bit mask case 数（与 mesh case 完全解耦）。</summary>
        public const int CaseCount   = 16;

        /// <summary>Mesh case 数组容量（base-3 编码：65 真实几何 + 16 死槽）。</summary>
        public const int BaseCaseCount = 81;

        // ── 角点坐标 ─────────────────────────────────────────────────────────
        /// <summary>四个角点的 (x, z) 坐标（unit quad [0,1]×[0,1]）。</summary>
        public static readonly (int x, int z)[] Corners =
        {
            (0, 0),  // V0 BL
            (1, 0),  // V1 BR
            (1, 1),  // V2 TR
            (0, 1),  // V3 TL
        };

        // ── Mesh 组合映射（base-3 编码）───────────────────────────────────────

        /// <summary>
        /// 根据四角高度计算 Mesh case index 和 base 高度。
        /// h0=V0(BL), h1=V1(BR), h2=V2(TR), h3=V3(TL)。
        /// base = 四角最小高度，r_i = h_i - base ∈ {0,1,2}（同格 4 角高差 ≤ 2 硬约束）。
        /// 返回 case_idx = r0 + r1*3 + r2*9 + r3*27 ∈ [0, 80]。
        /// 65 个真实几何 case（min(r) == 0）+ 16 个死槽（min(r) > 0，永远不会产出）。
        /// </summary>
        public static int GetMeshCase(int h0, int h1, int h2, int h3, out int baseH)
        {
            baseH = h0;
            if (h1 < baseH) baseH = h1;
            if (h2 < baseH) baseH = h2;
            if (h3 < baseH) baseH = h3;

            int r0 = h0 - baseH, r1 = h1 - baseH, r2 = h2 - baseH, r3 = h3 - baseH;
            return r0 + r1 * 3 + r2 * 9 + r3 * 27;
        }

        /// <summary>
        /// 判定 case_idx 是否为有效（真实几何）case：min(r0..r3) == 0。
        /// 死槽 case_idx（min(r) > 0）不会被 GetMeshCase 产出，运行时永不访问；
        /// 编辑器扫描 / 批量构建时跳过。
        /// </summary>
        public static bool IsValidCase(int caseIdx)
        {
            int r0 = caseIdx % 3;
            int r1 = (caseIdx / 3) % 3;
            int r2 = (caseIdx / 9) % 3;
            int r3 = (caseIdx / 27) % 3;
            int m  = r0;
            if (r1 < m) m = r1;
            if (r2 < m) m = r2;
            if (r3 < m) m = r3;
            return m == 0;
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
    }
}

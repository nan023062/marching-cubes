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
    public static class MqTable
    {
        public const int CornerCount = 4;
        public const int CaseCount   = 16;

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
        public static int GetMeshCase(int h0, int h1, int h2, int h3, out int baseH)
        {
            baseH = h0;
            if (h1 < baseH) baseH = h1;
            if (h2 < baseH) baseH = h2;
            if (h3 < baseH) baseH = h3;

            int ci = 0;
            if (h0 > baseH) ci |= 1;
            if (h1 > baseH) ci |= 2;
            if (h2 > baseH) ci |= 4;
            if (h3 > baseH) ci |= 8;
            return ci;
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
    }
}

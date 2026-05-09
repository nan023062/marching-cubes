namespace MarchingSquares
{
    /// <summary>
    /// Marching Squares 全局静态配置表（类比 CubeTable）。
    /// 集中定义角点、D4 对称置换、canonical case 归约表，供所有 MQ 组件共用。
    ///
    /// 角点编号（unit quad，XZ 平面，Y = 高度）：
    ///   V3(0,1) ─── V2(1,1)       bit mask：bit_i = 1 表示 Vi 高于 base
    ///     │               │
    ///   V0(0,0) ─── V1(1,0)
    ///
    /// 16 cases → D4 归约 → 5 canonical：{0, 1, 3, 5, 7}
    /// Case 15（全高）与 case 0（全低）几何相同，均为平 quad。
    /// </summary>
    public static class MqTable
    {
        // ── 角点 ─────────────────────────────────────────────────────────────

        public const int CornerCount = 4;
        public const int CaseCount   = 16;

        /// <summary>四个角点的 (x, z) 坐标（unit quad [0,1]×[0,1]）。</summary>
        public static readonly (int x, int z)[] Corners =
        {
            (0, 0),  // V0 BL
            (1, 0),  // V1 BR
            (1, 1),  // V2 TR
            (0, 1),  // V3 TL
        };

        // ── Canonical cases ───────────────────────────────────────────────────

        /// <summary>D4 归约后的 5 个 canonical case。</summary>
        public static readonly int[] CanonicalCases = { 0, 1, 3, 5, 7 };

        // ── D4 置换表 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 8 个 D4 变换的角点置换：perm[t][i] = 变换 t 后，角点 i 移动到的新位置。
        /// 顺序：e / r(270°CW) / r²(180°) / r³(90°CW) / s(flipX) / sr / sr² / sr³
        /// rotY 对应：0 / 270 / 180 / 90 / 0 / 270 / 180 / 90（flip=true 时加镜像）
        /// </summary>
        public static readonly int[][] D4Perms =
        {
            new[] { 0, 1, 2, 3 },  // e      rotY=0
            new[] { 3, 0, 1, 2 },  // r      rotY=270 (90° CW from above)
            new[] { 2, 3, 0, 1 },  // r²     rotY=180
            new[] { 1, 2, 3, 0 },  // r³     rotY=90
            new[] { 1, 0, 3, 2 },  // s      rotY=0,   flip
            new[] { 2, 1, 0, 3 },  // sr     rotY=270, flip
            new[] { 3, 2, 1, 0 },  // sr²    rotY=180, flip
            new[] { 0, 3, 2, 1 },  // sr³    rotY=90,  flip
        };

        public static readonly float[] D4RotY  = { 0f, 270f, 180f, 90f, 0f, 270f, 180f, 90f };
        public static readonly bool[]  D4Flipped = { false, false, false, false, true, true, true, true };

        // ── 预计算 canonical 归约表 ───────────────────────────────────────────

        /// <summary>
        /// CanonicalIndex[ci]：ci 在 D4 群下的最小等价 case（canonical form）。
        /// 特例：case 15（全高）→ 归约为 0（全低），两者几何相同。
        /// </summary>
        public static readonly int[] CanonicalIndex;

        static MqTable()
        {
            CanonicalIndex = new int[CaseCount];
            for (int ci = 0; ci < CaseCount; ci++)
            {
                int best = ci;
                for (int t = 0; t < D4Perms.Length; t++)
                {
                    int mapped = 0;
                    for (int i = 0; i < CornerCount; i++)
                        if ((ci & (1 << i)) != 0)
                            mapped |= 1 << D4Perms[t][i];
                    if (mapped < best) best = mapped;
                }
                CanonicalIndex[ci] = best;
            }
            // case 15（全高）几何与 case 0（全低）相同，强制归约
            CanonicalIndex[15] = 0;
        }

        // ── 工具方法 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 根据四角高度计算 case index 和 base 高度。
        /// h0=BL, h1=BR, h2=TR, h3=TL（与 Corners[] 顺序一致）。
        /// </summary>
        public static int GetCaseIndex(int h0, int h1, int h2, int h3, out int baseH)
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

        public static bool IsCanonical(int ci) => CanonicalIndex[ci] == ci;
    }
}

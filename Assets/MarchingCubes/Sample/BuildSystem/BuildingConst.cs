namespace MarchingCubes.Sample
{
    /// <summary>
    /// 建造系统共享常量。
    /// MSQTerrain 和 MCBuilding 的格点单位必须保持一致，统一在此定义。
    /// </summary>
    public static class BuildingConst
    {
        /// <summary>
        /// 格点单位大小（world units per grid cell）。
        /// MSQTerrain: pow = Unit  →  terrain.unit = 1f / Unit
        /// MCBuilding: unit = Unit
        /// </summary>
        public const int Unit = 1;

        /// <summary>
        /// 相邻格点高差硬约束（单位：Unit）。
        /// 65 case base-3 编码统一覆盖「同格 4 角高差 ≤ 2」全部组合，
        /// BFS 高差传播保证「相邻格点高差 ≤ 2」与之匹配。
        /// </summary>
        public const int TerrainMaxHeightDiff = 2;
    }
}

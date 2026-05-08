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
        /// 地形相邻格点允许的最大高差（terrain 本地网格单位）。
        /// 超过此值时自动向周围传播调整，保证不出现 mesh 变体所需高差以外的情况。
        /// </summary>
        public const int TerrainMaxHeightDiff = 1;
    }
}

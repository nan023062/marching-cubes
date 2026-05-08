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
        /// 地形相邻格点允许的最大高差，以 Unit 为基准（= 1 个世界单位）。
        /// Unit 变化时阈值等比例调整，保证始终是"1 个格点单位"的高差限制。
        /// </summary>
        public const int TerrainMaxHeightDiff = Unit;
    }
}

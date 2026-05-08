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
        /// 地形相邻格点允许的最大高差（单位：Unit）。
        /// 值为 1 表示相邻格点最多差 1 个 Unit 的世界高度。
        /// 后期美术提供多高差 mesh 变体后可调大此值以接入更丰富的地形变化。
        /// </summary>
        public const int TerrainMaxHeightDiff = 1;
    }
}

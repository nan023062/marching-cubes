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
    }
}

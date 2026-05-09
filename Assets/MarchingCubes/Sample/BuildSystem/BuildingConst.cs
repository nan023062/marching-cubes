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
        /// 悬崖最大高度（单位：Unit）。调大此值可刷出更高的悬崖墙面。
        /// 悬崖 Tile Mesh（固定 1 unit 高）在运行时按实际高差自动缩放 Y，无需新增美术 Case。
        ///
        /// 注意：坡面 Tile 的相邻格点高差始终强制 ≤ 1（19-case 坡面系统的硬约束，不受此值影响）。
        /// 相邻格 base 高差 > 1 时，坡面 tile 以最近似的 case 显示，垂直部分由悬崖 tile 补足。
        /// </summary>
        public const int TerrainMaxHeightDiff = 1;
    }
}

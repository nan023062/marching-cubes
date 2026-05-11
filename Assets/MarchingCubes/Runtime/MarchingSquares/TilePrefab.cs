using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MQ tile prefab 调试组件，仅持有 caseIndex / baseHeight 数据供 Inspector 查看。
    /// caseIndex 为 base-3 编码 case_idx ∈ [0, 80]，TileTable.GetMeshCase 产出。
    /// 网格可视化（点阵 grid）由 TerrainBuilder.DrawGizmos 在 Terrain 层统一绘制。
    /// </summary>
    [ExecuteAlways]
    public class TilePrefab : MonoBehaviour
    {
        [Header("Tile")]
        public int caseIndex;   // 0~80（base-3 编码 r0+r1*3+r2*9+r3*27，65 有效 + 16 死槽）
        public int baseHeight;  // runtime 由 TerrainBuilder 设置
    }
}

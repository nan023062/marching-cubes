using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// Case mesh 生成算法的可替换插槽。
    /// 子类化此 ScriptableObject 并实现 Build，即可在 CaseMeshProceduralGenerator
    /// 中切换任意算法，无需修改生成器本身。
    ///
    /// 三条硬约束（子类必须保证）：
    ///   1. 接缝精度  — 开放面顶点精确落在 x/y/z ∈ {0,1}；
    ///                  crossing edge 上的顶点精确取 CubeTable.EdgeMidpoints[e]。
    ///   2. 拓扑正确  — 开放面集合与 CubeTable.EdgeTable[ci] 的 bit mask 对应。
    ///   3. D4 一致   — 算法由顶点位置驱动，不 per-case 硬编码；
    ///                  对称性对任意 D4 变换自动成立。
    /// </summary>
    public abstract class CaseMeshBuilderAsset : ScriptableObject
    {
        // ── 约束参考（子类直接使用 CubeTable 静态数据）────────────────────────
        //
        //   CubeTable.Vertices[0..7]       — 8 顶点坐标（{0,1}³ 角点）
        //   CubeTable.EdgeMidpoints[0..11] — 12 棱中点（接缝顶点吸附目标）
        //   CubeTable.EdgeTable[ci]        — bit mask：哪些棱被等值面穿越
        //   CubeTable.TriTable[ci]         — 等值面三角面索引（拓扑参考）

        /// <summary>
        /// 生成 caseIndex 对应的 Unity Mesh。
        /// 顶点坐标空间：[0,1]³ Unity 坐标系（Y-up）。
        /// caseIndex 全空(0)/全满(255) 时返回 null。
        /// </summary>
        public abstract Mesh Build(int caseIndex);

        // ── 便捷方法（子类调用）────────────────────────────────────────────────

        /// <summary>返回 caseIndex 中 active 顶点的下标（0–7）列表。</summary>
        protected static List<int> ActiveVertices(int caseIndex)
        {
            var list = new List<int>(8);
            for (int i = 0; i < 8; i++)
                if ((caseIndex & (1 << i)) != 0) list.Add(i);
            return list;
        }

        /// <summary>返回 caseIndex 中 active 顶点的 Unity 坐标列表（与 ActiveVertices 同序）。</summary>
        protected static List<Vector3> ActiveVertexPositions(int caseIndex)
        {
            var list = new List<Vector3>(8);
            for (int i = 0; i < 8; i++)
                if ((caseIndex & (1 << i)) != 0)
                { var v = CubeTable.Vertices[i]; list.Add(new Vector3(v.x, v.y, v.z)); }
            return list;
        }

        /// <summary>caseIndex 中哪些棱被等值面穿越（EdgeTable bit mask）。</summary>
        protected static int CrossingEdgeMask(int caseIndex)
            => CubeTable.EdgeTable[caseIndex];

        /// <summary>
        /// 返回所有被穿越的棱的中点坐标（即接缝顶点必须吸附的精确位置）。
        /// </summary>
        protected static List<Vector3> CrossingEdgeMidpoints(int caseIndex)
        {
            int mask = CubeTable.EdgeTable[caseIndex];
            var list = new List<Vector3>(12);
            for (int e = 0; e < 12; e++)
                if ((mask & (1 << e)) != 0)
                { var m = CubeTable.EdgeMidpoints[e]; list.Add(new Vector3(m.x, m.y, m.z)); }
            return list;
        }
    }
}

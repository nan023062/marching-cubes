using System;
using UnityEngine;

namespace MarchingQuads
{
    /***  面板与顶点约定  *************************************
     *
     *  数据层：两组面板数组（垂直放置于 Y=0 平面上方）
     *
     *    xPanels[vx, cz]：垂直于 X 轴的面板（东西向隔断）
     *      位于 x=vx 处，跨越 z ∈ [cz, cz+1]
     *      隔开格子 (vx-1, cz) 与格子 (vx, cz)
     *
     *    zPanels[cx, vz]：垂直于 Z 轴的面板（南北向隔断）
     *      位于 z=vz 处，跨越 x ∈ [cx, cx+1]
     *      隔开格子 (cx, vz-1) 与格子 (cx, vz)
     *
     *  顶点 (vx, vz) 处的 4 条相邻面板（形成 QuadVertexMask）：
     *
     *        zPanel[vx-1][vz]        zPanel[vx][vz]
     *       ←── West(3) ──┤ (vx,vz) ├── East(1) ──→
     *                xPanel[vx][vz-1] ↓ South(2)
     *                xPanel[vx][vz]   ↑ North(0)
     *
     *  6 canonical 顶点连接形态（与 MarchingSquares 对应）：
     *    0  孤立（无面板）   → 无顶点连接件
     *    1  末端（单向面板） → 端头盖
     *    3  转角 L           → L 形竖条
     *    5  通道 ——          → 直通竖条（可省略）
     *    7  T 形             → T 形竖条
     *    15 十字 +           → 十字竖条
     *
     ****************************************************/

    /// <summary>顶点处 4 条相邻面板的激活掩码（N/E/S/W）。</summary>
    [Flags]
    public enum QuadVertexMask
    {
        None  = 0,
        North = 1 << 0,  // xPanels[vx,   vz  ] 向 +Z 延伸
        East  = 1 << 1,  // zPanels[vx,   vz  ] 向 +X 延伸
        South = 1 << 2,  // xPanels[vx,   vz-1] 向 -Z 延伸
        West  = 1 << 3,  // zPanels[vx-1, vz  ] 向 -X 延伸
        All   = North | East | South | West,
    }

    /// <summary>面板轴向（决定面板在空间中的朝向）。</summary>
    public enum PanelAxis
    {
        X,  // 垂直于 X 轴（面板法线朝 ±X，东西走向隔断）
        Z,  // 垂直于 Z 轴（面板法线朝 ±Z，南北走向隔断）
    }

    /// <summary>MarchingQuads 重建回调接口。</summary>
    public interface IMqReceiver
    {
        void OnRebuildCompleted();
    }
}

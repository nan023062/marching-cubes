using System;

namespace MarchingSquares
{
/************ MQ 悬崖边的索引约定 ****************************

  与地形 tile 的边约定完全对应：

  V3(TL) ───E2─── V2(TR)       bit mask：bit_i=1 表示该边有悬崖墙面
     |                 |          （当前格高于该边方向的相邻格）
    E3                E1
     |                 |
  V0(BL) ───E0─── V1(BR)

  E0 = 南边（Unity -Z）   E1 = 东边（Unity +X）
  E2 = 北边（Unity +Z）   E3 = 西边（Unity -X）

  悬崖 Mesh 以格子 XZ 中心为原点，Y ∈ [0,1]（1 unit 高）。

************************************************************/

    // ── 悬崖边 ───────────────────────────────────────────────────────────────

    public enum CliffEdge
    {
        E0 = 0,  // 南边 -Z
        E1 = 1,  // 东边 +X
        E2 = 2,  // 北边 +Z
        E3 = 3,  // 西边 -X
    }

    [Flags]
    public enum CliffEdgeMask
    {
        None = 0x00,
        E0   = 0x01,  // 南
        E1   = 0x02,  // 东
        E2   = 0x04,  // 北
        E3   = 0x08,  // 西
        All  = 0x0F,
    }
}

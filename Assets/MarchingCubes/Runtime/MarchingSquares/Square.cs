using System;
using UnityEngine;

namespace MarchingSquares
{
    /***  格子邻居约定（per-cell, 4 基本方向）  *****
     *
     *   ┌──────┬──────┐
     *   │  NW  │  NE  │
     *   │   North(0)  │
     *   ├──────┼──────┤
     *   │W(3)  │  E(1)│
     *   │   South(2)  │
     *   ├──────┬──────┤
     *   │  SW  │  SE  │
     *   └──────┴──────┘
     *
     *  CellNeighborMask：bit0=N, bit1=E, bit2=S, bit3=W
     *  SquareVertexMask：bit0=BL, bit1=BR, bit2=TR, bit3=TL
     *    （顶点系统：以顶点为中心，看周围4个格子是否激活）
     *
     ****************************************************/

    /// <summary>4-bit per-cell 邻居激活掩码（North/East/South/West）。</summary>
    [Flags]
    public enum CellNeighborMask
    {
        None  = 0,
        North = 1 << 0,
        East  = 1 << 1,
        South = 1 << 2,
        West  = 1 << 3,
        All   = North | East | South | West,
    }

    /// <summary>4-bit per-vertex 格子激活掩码（BL/BR/TR/TL 四角格子）。</summary>
    [Flags]
    public enum SquareVertexMask
    {
        None = 0,
        BL   = 1 << 0,
        BR   = 1 << 1,
        TR   = 1 << 2,
        TL   = 1 << 3,
        All  = BL | BR | TR | TL,
    }

    /// <summary>格点数据：记录该位置是否有地板格子。</summary>
    public struct SquarePoint
    {
        public readonly sbyte x, z;
        public bool active;

        public SquarePoint(int x, int z, bool active = false)
        {
            this.x      = (sbyte)x;
            this.z      = (sbyte)z;
            this.active = active;
        }

        public Vector3 position => new Vector3(x, 0f, z);
    }

    /// <summary>MarchingSquares 重建回调接口。</summary>
    public interface IMsReceiver
    {
        void OnRebuildCompleted();
    }
}

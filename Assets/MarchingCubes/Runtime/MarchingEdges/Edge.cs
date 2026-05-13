using System;
using UnityEngine;

namespace MarchingEdges
{
    /*****  面槽索引与平面约定  *****************************
     *
     *  3 个正交中心面把 cube 分成 12 个面槽（各 0.5×0.5）：
     *
     *        X组（YZ平面, x=0）    Y组（XZ平面, y=0）    Z组（XY平面, z=0）
     *        法线 = +X             法线 = +Y             法线 = +Z
     *
     *         +Y                                          +Y
     *     X3  |  X0                                   Z1  |  Z0
     *   ──────O──────  +Z    ──────O──────  +X   ─────────O──────  +X
     *     X2  |  X1       Y2  |  Y3  |  Y0  |  Y1     Z2  |  Z3
     *                                +Z
     *
     *  Bit 编码：bits 0-3 = X组，bits 4-7 = Y组，bits 8-11 = Z组
     *
     *******************************************************/

    /// <summary>12 个面槽的枚举，值等于对应的 bit 位。</summary>
    public enum EdgeSlot
    {
        X0 =  0, X1 =  1, X2 =  2, X3 =  3,  // YZ 平面 (+Y+Z, -Y+Z, -Y-Z, +Y-Z)
        Y0 =  4, Y1 =  5, Y2 =  6, Y3 =  7,  // XZ 平面 (+X+Z, -X+Z, -X-Z, +X-Z)
        Z0 =  8, Z1 =  9, Z2 = 10, Z3 = 11,  // XY 平面 (+X+Y, -X+Y, -X-Y, +X-Y)
    }

    /// <summary>12 个面槽的位掩码标志，可组合使用。</summary>
    [Flags]
    public enum EdgeSlotMask
    {
        None = 0,

        // X 组（YZ 平面）
        X0 = 1 << 0,  X1 = 1 << 1,  X2 = 1 << 2,  X3 = 1 << 3,
        // Y 组（XZ 平面）
        Y0 = 1 << 4,  Y1 = 1 << 5,  Y2 = 1 << 6,  Y3 = 1 << 7,
        // Z 组（XY 平面）
        Z0 = 1 << 8,  Z1 = 1 << 9,  Z2 = 1 << 10, Z3 = 1 << 11,

        XGroup = X0 | X1 | X2 | X3,
        YGroup = Y0 | Y1 | Y2 | Y3,
        ZGroup = Z0 | Z1 | Z2 | Z3,
        WallsOnly  = XGroup | ZGroup,   // 仅垂直面（围栏/栅栏子集）
        FloorOnly  = YGroup,            // 仅水平面子集
        All = XGroup | YGroup | ZGroup, // 0xFFF
    }

    /// <summary>3 个正交中心面的分组。</summary>
    public enum EdgeGroup : byte
    {
        X = 0,  // YZ 平面（X 方向法线）
        Y = 1,  // XZ 平面（Y 方向法线，水平面）
        Z = 2,  // XY 平面（Z 方向法线）
    }

    /// <summary>
    /// 格点数据：记录该顶点位置及其 12-bit 面槽激活状态。
    /// 类比 MarchingCubes 的 Point（iso → mask）。
    /// </summary>
    public struct EdgePoint
    {
        public readonly sbyte x, y, z;

        /// <summary>12-bit 面槽掩码（对应 EdgeSlotMask）。</summary>
        public int mask;

        public Vector3 position => new Vector3(x, y, z);

        public EdgePoint(int x, int y, int z)
        {
            this.x    = (sbyte)x;
            this.y    = (sbyte)y;
            this.z    = (sbyte)z;
            this.mask = 0;
        }

        public bool HasSlot(EdgeSlot slot)              => (mask & (1 << (int)slot)) != 0;
        public void SetSlot(EdgeSlot slot, bool active) =>
            mask = active ? mask | (1 << (int)slot) : mask & ~(1 << (int)slot);

        /// <summary>该顶点是否没有任何激活面槽。</summary>
        public bool IsEmpty => mask == 0;

        /// <summary>获取面槽所属分组（X/Y/Z）。</summary>
        public static EdgeGroup GroupOf(EdgeSlot slot) => (EdgeGroup)((int)slot >> 2);
    }

    /// <summary>面槽网格顶点（用于平滑网格共享顶点缓存）。</summary>
    public struct EdgeVertex
    {
        public int    index;     // 在顶点列表中的索引
        public Vector3 position;
        public Vector3 normal;
    }

    /// <summary>面槽三角形（存储顶点索引，供平滑网格使用）。</summary>
    public struct EdgeTriangle
    {
        public int v0, v1, v2;
    }

    /// <summary>MarchingEdges 重建回调接口。</summary>
    public interface IMarchingEdgeReceiver
    {
        /// <summary>网格重建完成后调用。</summary>
        void OnRebuildCompleted();
    }
}

using System;
using UnityEngine;

namespace MarchingSquares
{
    /************ 顶点和边的索引约定 ****************

      ⓪-③: Vertex Index（角点）
      0-3 : Edge Index（边）

      V3(TL) ───E2─── V2(TR)
         |                 |
        E3                E1
         |                 |
      V0(BL) ───E0─── V1(BR)

      Bit mask：bit_i=1 表示 Vi 高于 base 高度（高位角点）

    ****************************************************/

    // ── 角点 ─────────────────────────────────────────────────────────────────

    public enum TileVertex
    {
        V0 = 0,  // BL (0,0)
        V1 = 1,  // BR (1,0)
        V2 = 2,  // TR (1,1)
        V3 = 3,  // TL (0,1)
    }

    [Flags]
    public enum TileVertexMask
    {
        None = 0x00,
        V0   = 0x01,  // BL
        V1   = 0x02,  // BR
        V2   = 0x04,  // TR
        V3   = 0x08,  // TL
        All  = 0x0F,
    }

    // ── 边 ───────────────────────────────────────────────────────────────────

    public enum TileEdge
    {
        E0 = 0,  // Bottom  V0–V1
        E1 = 1,  // Right   V1–V2
        E2 = 2,  // Top     V2–V3
        E3 = 3,  // Left    V3–V0
    }

    // ── 格点数据 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// MQ 地形格点（类比 MC 的 Point）。
    /// 存储格点坐标、高度值（离散整数）、地形类型。
    /// </summary>
    public struct TilePoint
    {
        public readonly sbyte x, z;  // 格点坐标
        public byte  textureType;     // 纹理类型
        
        public TilePoint(int x, int z)
        {
            this.x           = (sbyte)x;
            this.z           = (sbyte)z;
            this.textureType = 0;
        }
    }

    // ── 接收器接口 ───────────────────────────────────────────────────────────

    /// <summary>
    /// TileTerrain mesh 生成完成通知接口（类比 IMarchingCubeReceiver）。
    /// </summary>
    public interface ISquareReceiver
    {
        /// <summary>Rebuild() 完成后回调，用于同步 MeshFilter / MeshCollider。</summary>
        void OnRebuildCompleted(Mesh mesh);
    }
}

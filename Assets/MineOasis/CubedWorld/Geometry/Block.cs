//****************************************************************************
// File: Block.cs
// Author: Li Nan
// Date: 2024-03-07 12:00
// Version: 1.0
//****************************************************************************

using Unity.Entities;
using Unity.Mathematics;

namespace MineOasis
{
    /// <summary>
    /// A 3D block that can be built on.
    /// Block is a struct that contains the position, rotation, and size
    /// </summary>
    [System.Serializable]
    public struct Block : IComponentData
    {
        public float3 position;
        public quaternion rotation;
        public float3 size;
    }
    
    /// <summary>
    /// 组合类建材
    /// </summary>
    [System.Serializable]
    public struct CompositeBlock : IComponentData
    {
        /// <summary>
        /// 尺寸单位是Unit
        /// 指大于0的轴 就是可以拉伸的方向和 精度就是值*unit
        /// </summary>
        public byte cellX, cellY, cellZ;
        
        public float3 position;
        public quaternion rotation;
        public byte sizeX, sizeY, sizeZ;
    }
    
    /// <summary>
    /// 单独建材元素
    /// </summary>
    [System.Serializable]
    public struct SingleBlock : IComponentData
    {
        
    }
}
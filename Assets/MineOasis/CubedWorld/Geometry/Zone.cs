//****************************************************************************
// File: Zone.cs
// Author: Li Nan
// Date: 2024-03-07 12:00
// Version: 1.0
//****************************************************************************

using Unity.Entities;
using Unity.Mathematics;

namespace MineOasis
{
    /// <summary>
    /// A zone is a 3D space in the world that can be built on.
    /// </summary>
    public struct Zone : IComponentData
    {
        public float3 position;
        public quaternion rotation;
        
        /// <summary>
        /// size count of unit
        /// </summary>
        public int3 size;
    }
}
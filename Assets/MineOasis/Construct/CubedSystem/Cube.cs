using Unity.Entities;
using Unity.Mathematics;

namespace MineOasis
{
    public struct Cube : IComponentData
    {
        public float3 Center;
        public float3 Size;
        public quaternion Rotation;
    }
}
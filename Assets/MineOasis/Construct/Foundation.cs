using Unity.Mathematics;

namespace MineOasis
{
    [System.Serializable]
    public struct Foundation
    {
        public int id;
        public float3 position;
        public quaternion rotation;
        public float3 size;
        
        // parent structure
        public int parent;
    }
}
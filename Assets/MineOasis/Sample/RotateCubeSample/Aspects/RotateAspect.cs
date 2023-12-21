using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MineOasis.Sample.RotateCubeSample
{
    readonly partial struct RotateAspect : IAspect
    {
        readonly RefRW<LocalTransform> transform;
        readonly RefRW<RotateSpeed> rotateSpeed;
        
        public readonly void Set(float3 position, float speed)
        {
            transform.ValueRW.Position = position;
            rotateSpeed.ValueRW.rotateSpeed = speed;
        }
    }
}
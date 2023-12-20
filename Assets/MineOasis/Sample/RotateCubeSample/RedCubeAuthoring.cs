using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    struct RedCubeTag : IComponentData
    {
        
    }
    
    public class RedCubeAuthoring : MonoBehaviour 
    {
        public class Baker : Baker<RedCubeAuthoring>
        {
            public override void Bake(RedCubeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RedCubeTag());
            }
        }
    }
    
    [BurstCompile]
    [UpdateInGroup( typeof( CubeRotateSystemGroup ) )]
    public partial struct RedCubeRotateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach ((RefRW<LocalTransform> transform, RefRO<RotateSpeed> rotateSpeed, RefRO<RedCubeTag> tag) in 
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotateSpeed>, RefRO<RedCubeTag>>())
            {
                transform.ValueRW = transform.ValueRW.RotateY(rotateSpeed.ValueRO.rotateSpeed * deltaTime);
            }
        }
    }
}
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    struct BlueCubeTag : IComponentData
    {
        
    }
    
    public class BlueCubeAuthoring : MonoBehaviour 
    {
        public class Baker : Baker<BlueCubeAuthoring>
        {
            public override void Bake(BlueCubeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new BlueCubeTag());
            }
        }
    }
    
    [BurstCompile]
    [UpdateInGroup( typeof( CubeRotateSystemGroup ) )]
    public partial struct BlueCubeRotateSystem : ISystem
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
            
            foreach ((RefRW<LocalTransform> transform, RefRO<RotateSpeed> rotateSpeed, RefRO<BlueCubeTag> tag) in 
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotateSpeed>, RefRO<BlueCubeTag>>())
            {
                transform.ValueRW = transform.ValueRW.RotateY(rotateSpeed.ValueRO.rotateSpeed * deltaTime);
            }
        }
    }
}
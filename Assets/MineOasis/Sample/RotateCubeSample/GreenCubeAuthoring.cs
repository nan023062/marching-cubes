using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    struct GreenCubeTag : IComponentData
    {
        
    }
    
    public class GreenCubeAuthoring : MonoBehaviour 
    {
        public class Baker : Baker<GreenCubeAuthoring>
        {
            public override void Bake(GreenCubeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GreenCubeTag());
            }
        }
    }
    
    [BurstCompile]
    [UpdateInGroup( typeof( CubeRotateSystemGroup ) )]
    public partial struct GreenCubeRotateSystem : ISystem
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
            
            foreach ((RefRW<LocalTransform> transform, RefRO<RotateSpeed> rotateSpeed, RefRO<GreenCubeTag> tag) in 
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotateSpeed>, RefRO<GreenCubeTag>>())
            {
                transform.ValueRW = transform.ValueRW.RotateY(rotateSpeed.ValueRO.rotateSpeed * deltaTime);
            }
        }
    }
}
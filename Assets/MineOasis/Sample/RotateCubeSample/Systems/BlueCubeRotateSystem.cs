//****************************************************************************
// File: BlueCubeRotateSystem.cs
// Author: Li Nan
// Date: 2023-12-20 12:00
// Version: 1.0
//****************************************************************************

using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace MineOasis.Sample.RotateCubeSample
{
    
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
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
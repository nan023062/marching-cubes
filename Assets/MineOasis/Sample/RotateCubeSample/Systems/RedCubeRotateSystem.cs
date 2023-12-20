//****************************************************************************
// File: RedCubeRotateSystem.cs
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
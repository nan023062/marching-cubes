//****************************************************************************
// File: BlockSystem.cs
// Author: Li Nan
// Date: 2024-03-07 12:00
// Version: 1.0
//****************************************************************************
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace MineOasis
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    public partial struct BlockSystem : ISystem
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
            foreach ((RefRW<LocalTransform> transform, RefRO<Block> block) in 
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<Block>>())
            {
                
                
            }
        }
    }
}
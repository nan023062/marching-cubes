using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MineOasis
{    
    [BurstCompile]
    [UpdateInGroup(typeof(MineOasisSystemGroup))]
    public partial struct CubeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            //state.RequireForUpdate<Cube>();
            //state.RequireForUpdate<Renderer>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = state.WorldUnmanaged.Time.DeltaTime;
            
            foreach ((RefRW<Cube> cube, RefRO<Renderer> renderer) in SystemAPI.Query<RefRW<Cube>, RefRO<Renderer>>())
            {
                cube.ValueRW.Center += new float3(0.05f, 0.05f, 0.05f) * deltaTime;
            }
            
            Debug.Log($"CubeSystem.OnUpdate({deltaTime})");
        }
    }
}
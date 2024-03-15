using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace MineOasis
{
    [BurstCompile]
    [UpdateInGroup(typeof(MineOasisSystemGroup))]
    public partial struct RenderingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            //state.RequireForUpdate<Cube>();
            state.RequireForUpdate<Renderer>();
            //Debug.Log($"RenderingSystem.OnCreate()");
        }

        public void OnDestroy(ref SystemState state)
        {
            Debug.Log($"RenderingSystem.OnDestroy()");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = state.WorldUnmanaged.Time.DeltaTime;
            //Debug.Log($"RenderingSystem.OnUpdate({deltaTime})");
        }
    }
}
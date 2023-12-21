using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Jobs;

namespace MineOasis.Sample.RotateCubeSample
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(MineOasisSystemGroup))]
    public partial struct CubeGeneratorByPrefabSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CubeGeneratorByPrefab>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var generator = SystemAPI.GetSingleton<CubeGeneratorByPrefab>();
            using var cubesArray = CollectionHelper.CreateNativeArray<Entity>(generator.count, Allocator.Temp);
            state.EntityManager.Instantiate(generator.cubePrefab, cubesArray);
            
            int index = 0;
            foreach (var entity in cubesArray)
            {
                var position = new float3( index * 1.1f, 0, 0 );
                float rotateSpeed = 360f * (index + 1);
                var rotateAspect = SystemAPI.GetAspect<RotateAspect>(entity);
                rotateAspect.Set(position, rotateSpeed);
                /*
                var transform = state.EntityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = position;
                state.EntityManager.SetComponentData(entity, transform);
                
                var speed = state.EntityManager.GetComponentData<RotateSpeed>(entity);
                speed.rotateSpeed = rotateSpeed;
                state.EntityManager.SetComponentData(entity, speed);*/
                
                index++;
            }
            // system is only run once
            state.Enabled = false;
        }
    }
}
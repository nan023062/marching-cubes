//****************************************************************************
// File: AlphaTweenCubeWithJobChunkSystem.cs
// Author: Li Nan
// Date: 2023-12-20 12:00
// Version: 1.0
//****************************************************************************

using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MineOasis.Sample.RotateCubeSample
{
    [BurstCompile]
    [UpdateInGroup( typeof( CubeRotateSystemGroup ))]
    struct AlphaTweenCubeWithJobChunk : IJobChunk
    {
        public float time;
        public ComponentTypeHandle<LocalTransform> transformTypeHandle;
        
        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var chunkTransforms = chunk.GetNativeArray(ref transformTypeHandle);

            var e = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (e.NextEntityIndex(out var i))
            {
                var transform = chunkTransforms[i];
                transform.Scale = 1f + 0.5f * math.sin(time + transform.Position.x);
                chunkTransforms[i] = transform;
            }
        }
    }
    
    [BurstCompile]
    [UpdateInGroup( typeof( CubeRotateSystemGroup ) )]
    public partial struct AlphaTweenCubeWithJobChunkSystem : ISystem
    {
        EntityQuery m_Query;
        ComponentTypeHandle<LocalTransform> m_LocalTransformTypeHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<RotateSpeed, LocalTransform>();
            m_Query = state.GetEntityQuery(queryBuilder);
            m_LocalTransformTypeHandle = state.GetComponentTypeHandle<LocalTransform>();
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_LocalTransformTypeHandle.Update(ref state);
          
            var job = new AlphaTweenCubeWithJobChunk
            {
                time = (float)SystemAPI.Time.ElapsedTime,
                transformTypeHandle = m_LocalTransformTypeHandle,
            };
            state.Dependency = job.ScheduleParallel(m_Query, state.Dependency);;
        }
    }
}
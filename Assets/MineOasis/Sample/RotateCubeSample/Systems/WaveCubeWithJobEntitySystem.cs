//****************************************************************************
// File: RotateCubeWithJobEntitySystem.cs
// Author: Li Nan
// Date: 2023-12-20 12:00
// Version: 1.0
//****************************************************************************

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    partial struct WaveCubeWithJobEntity : IJobEntity
    {
        [ReadOnly] public float time;
        
        void Execute(ref LocalTransform transform, ref RotateSpeed rotateSpeed)
        {
            float3 position = transform.Position;
            float2 yz = new float2(position.y, position.z);
            float distance = math.distance(yz, float2.zero);
            rotateSpeed.rotateSpeed = 360f * distance;
            position.x = 2 * math.sin(time * 3f + distance * 0.2f);
            transform.Position = position;
        }
    }

    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup( typeof( CubeRotateSystemGroup ) )]
    public partial struct WaveCubeWithJobEntitySystem : ISystem
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
            var job = new WaveCubeWithJobEntity { time = (float)SystemAPI.Time.ElapsedTime };
            job.ScheduleParallel();
        }
    }
}
//****************************************************************************
// File: CubeRotateSystem.cs
// Author: Li Nan
// Date: 2023-12-19 12:00
// Version: 1.0
//****************************************************************************

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    [BurstCompile]
    [UpdateInGroup( typeof( CubeRotateSystemGroup ) )]
    public partial struct CubeRotateSystem : ISystem
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
            
            foreach ((RefRW<LocalTransform> transform, RefRO<RotateSpeed> rotateSpeed) in 
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotateSpeed>>())
            {
                //transform.ValueRW.Position.y =  math.sin(2 * (float)SystemAPI.Time.ElapsedTime);
                transform.ValueRW = transform.ValueRW.RotateY(rotateSpeed.ValueRO.rotateSpeed * deltaTime);
                //transform.ValueRW.RotateY(rotateSpeed.ValueRO.rotateSpeed * deltaTime);
            }
            //Debug.Log($"CubeRotateSystem.OnUpdate({deltaTime})");
        }
    }
}
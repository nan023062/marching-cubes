//****************************************************************************
// File: RotateCubeAuthoring.cs
// Author: Li Nan
// Date: 2023-12-19 12:00
// Version: 1.0
//****************************************************************************

using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    struct RotateSpeed : IComponentData
    {
        public float rotateSpeed;
    }
    
    
    public class RotateCubeAuthoring : MonoBehaviour
    {
        [Range(0, 360)] public float rotateSpeed = 360F;
        
        public class Baker : Baker<RotateCubeAuthoring>
        {
            public override void Bake(RotateCubeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RotateSpeed()
                {
                    rotateSpeed = math.radians(authoring.rotateSpeed) 
                });
            }
        }
    }
}
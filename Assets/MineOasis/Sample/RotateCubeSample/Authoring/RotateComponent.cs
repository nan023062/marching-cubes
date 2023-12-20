//****************************************************************************
// File: RotateComponent.cs
// Author: Li Nan
// Date: 2023-12-20 12:00
// Version: 1.0
//****************************************************************************

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    public class RotateComponent : MonoBehaviour
    {
        [Range(0, 360)] public float rotateSpeed = 360F;
        
        public class Baker : Baker<RotateComponent>
        {
            public override void Bake(RotateComponent authoring)
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
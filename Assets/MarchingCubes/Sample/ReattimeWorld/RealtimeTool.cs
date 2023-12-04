//****************************************************************************
// File: RealtimeTool.cs
// Author: Li Nan
// Date: 2023-12-03 12:00
// Version: 1.0
//****************************************************************************

using System;
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class RealtimeTool : MonoBehaviour
    {
        [SerializeField, Range( 0.001f, 2f)]
        private float _radius = 0.6f;
        
        public void Update()
        {
            var world = RealtimeWorld.Instance;
            if (world != null)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    world.SetBlock(transform.position, _radius);
                }
            }
        }
    }
}
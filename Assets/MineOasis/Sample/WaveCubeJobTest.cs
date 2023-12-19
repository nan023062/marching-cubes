//****************************************************************************
// File: WaveCubeJobTest.cs
// Author: Li Nan
// Date: 2023-12-19 12:00
// Version: 1.0
//****************************************************************************

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;

namespace MineOasis.Sample
{
    [BurstCompile]
    struct WaveCubeJob : IJobParallelForTransform
    {
        [ReadOnly] public float time;
        
        public void Execute(int index, TransformAccess transform)
        {
            Vector3 position = transform.position;
            Vector2 xz = new Vector2(position.x, position.z);
            float distance = Vector2.Distance(xz, Vector2.zero);
            position.y = 10 * math.sin(time * 3 + distance * 0.2f);
            transform.position = position;
        }
    }


    public class WaveCubeJobTest : MonoBehaviour
    {
        private static ProfilerMarker _profilerMarker = new ("WaveCubeJobTest.Update");
        
        public GameObject prefab;
        [Range(10,100)] public int xHalfCount = 40;
        [Range(10,100)] public int zHalfCount = 40;
        
        private TransformAccessArray _transformsAccessArray;
        
        private void Start()
        {
            _transformsAccessArray = new TransformAccessArray(4 * xHalfCount * zHalfCount);
            for (int x = -xHalfCount; x < xHalfCount; x++)
            {
                for (int z = -zHalfCount; z < zHalfCount; z++)
                {
                    GameObject go = Instantiate(prefab, new Vector3(x, 0, z), Quaternion.identity);
                    _transformsAccessArray.Add(go.transform);
                }
            }
        }
        
        private void Update()
        {
            using (_profilerMarker.Auto())
            {
                var job = new WaveCubeJob() { time = Time.realtimeSinceStartup, };
                job.Schedule(_transformsAccessArray).Complete();
            }
        }

        private void OnDestroy()
        {
            _transformsAccessArray.Dispose();
        }
    }
}
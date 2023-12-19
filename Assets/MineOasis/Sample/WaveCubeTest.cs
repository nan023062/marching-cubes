//****************************************************************************
// File: WaveCubeTest.cs
// Author: Li Nan
// Date: 2023-12-19 12:00
// Version: 1.0
//****************************************************************************

using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;

namespace MineOasis.Sample
{
    public class WaveCubeTest : MonoBehaviour
    {
        private static ProfilerMarker _profilerMarker = new ("WaveCubeTest.Update");
        public GameObject prefab;
        [Range(10,100)] public int xHalfCount = 40;
        [Range(10,100)] public int zHalfCount = 40;
        private List<Transform> _transformsArray;
        private void Start()
        {
            _transformsArray = new List<Transform>(4 * xHalfCount * zHalfCount);
            for (int x = -xHalfCount; x < xHalfCount; x++)
            {
                for (int z = -zHalfCount; z < zHalfCount; z++)
                {
                    GameObject go = Instantiate(prefab, new Vector3(x, 0, z), Quaternion.identity);
                    _transformsArray.Add(go.transform);
                }
            }
        }
        
        private void Update()
        {
            using (_profilerMarker.Auto())
            {
                float time = Time.realtimeSinceStartup;
                foreach (var t in _transformsArray)
                {
                    Vector3 position = t.position;
                    Vector2 xz = new Vector2(position.x, position.z);
                    float distance = Vector2.Distance(xz, Vector2.zero);
                    position.y = 10 * math.sin(time * 3 + distance * 0.2f);
                    t.position = position;
                }
            }
        }

        private void OnDestroy()
        {
            _transformsArray.Clear();
        }
    }
}
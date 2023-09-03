//****************************************************************************
// File: MarchingCubesSample.cs
// Author: Li Nan
// Date: 2023-09-02 12:00
// Version: 1.0
//****************************************************************************

using System;
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class MarchingCubesSample : MonoBehaviour
    {
        public int x, y, z;
        public uint unit = 1;
        public sbyte value = 5;

        private MarchingCubes _marchingCubes;
        [SerializeField] private Brush _brush;
        private MeshFilter _meshFilter;
        
        private void Awake()
        {
            Vector3 scale = Vector3.one * (1f / unit);
            Matrix4x4 matrix4X4 = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _marchingCubes = new MarchingCubes(x, y, z, matrix4X4);
            _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.sharedMesh = _marchingCubes.mesh;
            transform.localScale = scale;
        }

        private void OnDrawGizmos()
        {
            _marchingCubes?.DrawGizmos();
        }
        
        private void Update()
        {
            Vector3 pos = Input.mousePosition;
            Transform t = _brush.transform;
            Ray ray = Camera.main.ScreenPointToRay(pos);
            int layerMask = 1 << LayerMask.NameToLayer("MarchingCubes");
            if (Physics.Raycast(ray, out var hit, 1000, layerMask))
            {
                var h = hit.collider;
                t.position = hit.point;
                t.rotation = _marchingCubes.localToWorld.rotation;
            }
            else
            {
                t.position = ray.origin + ray.direction * 4;
            }

            if (Input.GetMouseButtonUp(1))
            {
                _marchingCubes.Brush(_brush, false);
                _meshFilter.sharedMesh = _marchingCubes.mesh;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _marchingCubes.Brush(_brush, true);
                _meshFilter.sharedMesh = _marchingCubes.mesh;
            }
        }
    }
}
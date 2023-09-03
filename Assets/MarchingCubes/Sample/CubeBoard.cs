//****************************************************************************
// File: CubeBoard.cs
// Author: Li Nan
// Date: 2023-09-03 12:00
// Version: 1.0
//****************************************************************************

using System;
using UnityEngine;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class CubeBoard : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private BoxCollider _collider;
        public MarchingCubesSample cubesSample;
        
        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<BoxCollider>();
        }
        
        public void OnMouseEnter()
        {
            _renderer.enabled = true;
        }

        public void OnMouseUpAsButton()
        {
            Vector3 pos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(pos);
            if (_collider.Raycast(ray, out var hit, 1000))
            {
                Vector3 scale = transform.localScale;
                Vector3 center = _collider.center;
                Vector3 half = _collider.size * 0.5f;
                Vector3 min = center - half;
                pos = transform.InverseTransformPoint(hit.point);
                Vector3 offset = pos - min;
                int x = Mathf.FloorToInt(offset.x * scale.x);
                int z = Mathf.FloorToInt(offset.z * scale.z);
                cubesSample.CreateCube(x, 0, z);
            }
        }

        public void OnMouseExit()
        {
            _renderer.enabled = false;
        }
    }
}
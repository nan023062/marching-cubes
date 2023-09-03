//****************************************************************************
// File: PointCube.cs
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
    public class PointCube : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private BoxCollider _collider;
        public int x, y, z;
        
        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<BoxCollider>();
        }

        public void OnMouseEnter()
        {
            _renderer.enabled = true;
        }
        
        public void OnMouseExit()
        {
            _renderer.enabled = false;
        }
    }
}
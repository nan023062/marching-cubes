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
    public class PointCube : PointElement
    {
        public int x, y, z;

        [SerializeField] bool _showGizmos = false;

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }
    }
}
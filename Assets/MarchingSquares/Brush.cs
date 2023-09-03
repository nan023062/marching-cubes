//****************************************************************************
// File: Brush.cs
// Author: Li Nan
// Date: 2023-08-30 12:00
// Version: 1.0
//****************************************************************************

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MarchingSquares
{
    public class Brush : MonoBehaviour
    {
        [Range(1, 5),SerializeField, Header("刷子尺寸")] 
        private int size;
        [SerializeField, Header("开启纹理刷")] 
        public bool colorBrush;
        
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Mesh _mesh;
        private int _size;
        
        public int Size => _size;
        
        public void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _filter = GetComponent<MeshFilter>();
            _size = 0;
            UpdateMesh();
        }

        private void OnDestroy()
        {
            _filter = null;
            _renderer = null;
        }

        private void Update()
        {
            _filter.sharedMesh = _mesh;
        }

        private void OnValidate()
        {
            UpdateMesh();
        }

        private void UpdateMesh()
        {
            if (size != _size && null != _filter)
            {
                _size = size;
                int segment = 16;
                float segAngle = 360f / segment;
                Vector3 axis = new Vector3(0,0.01f,0.5f * _size);
                Vector3[] vertices = new Vector3[segment + 1];
                int[] triangles = new int[ segment * 3];
                vertices[segment] = new Vector3(0, 0.01f, 0);
                for (int i = 0; i < segment; i++)
                {
                    vertices[i] = Quaternion.AngleAxis(segAngle * i, Vector3.up) * axis;
                    int index = i * 3;
                    triangles[index + 0] = segment;
                    triangles[index + 1] = i;
                    triangles[index + 2] = (i + 1) % segment;
                }

                _mesh = new Mesh
                {
                    vertices = vertices,
                    triangles = triangles,
                };
                _mesh.RecalculateNormals();
            }
        }
    }
}
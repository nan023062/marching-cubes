//****************************************************************************
// File: DualContouringSample.cs
// Author: Li Nan
// Date: 2026-05-09
// Version: 1.0
//
// 对标 GenerationSphereSample.cs：用同一份球形 iso 场驱动 DualContouringMesh
//****************************************************************************

using UnityEngine;

namespace DualContouring
{
    public class DualContouringSample : MonoBehaviour
    {
        public int x = 10, y = 10, z = 10;
        public float radius = 4f;

        private DualContouringMesh _dcMesh;
        private MeshFilter _meshFilter;
        private Vector3 _center;

        void Start()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _dcMesh = new DualContouringMesh(x, y, z);
            Populate();
        }

        private void OnValidate()
        {
            if (_dcMesh == null) return;
            Populate();
        }

        private void Populate()
        {
            _center = new Vector3(_dcMesh.X, _dcMesh.Y, _dcMesh.Z) * 0.5f;
            float maxDis = Vector3.Distance(_center, Vector3.zero);
            _dcMesh.IsoLevel = maxDis - radius;

            for (int i = 0; i <= _dcMesh.X; i++)
            for (int j = 0; j <= _dcMesh.Y; j++)
            for (int k = 0; k <= _dcMesh.Z; k++)
            {
                float iso = maxDis - Vector3.Distance(_center, new Vector3(i, j, k));
                _dcMesh.SetPointISO(i, j, k, iso);
            }

            _dcMesh.Rebuild();

            if (_meshFilter != null)
                _meshFilter.sharedMesh = _dcMesh.mesh;
        }
    }
}

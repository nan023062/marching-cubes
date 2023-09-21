using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class GenerationSphereSample : MonoBehaviour
    {
        public int x, y, z;
        private CubeMesh _cubeMesh;

        public float radius = 1f;
        
        // Start is called before the first frame update
        void Start()
        {
            _cubeMesh = new CubeMesh(x, y, z, transform.localToWorldMatrix);
            OnValidate();
        }

        private void OnDestroy()
        {
            _cubeMesh = null;
        }

        private void OnValidate()
        {
            if (null != _cubeMesh)
            {
                Vector3 center = new Vector3(_cubeMesh.X, _cubeMesh.Y, _cubeMesh.Z) * 0.5f;
                float maxDis = Vector3.Distance(center, Vector3.zero);
                
                for (int i = 0; i <= _cubeMesh.X; i++)
                {
                    for (int j = 0; j <= _cubeMesh.Y; j++)
                    {
                        for (int k = 0; k <= _cubeMesh.Z; k++)
                        {
                            float iso = maxDis - Vector3.Distance(center, new Vector3(i, j, k));
                            _cubeMesh.SetPointISO(i, j , k, iso);
                        }
                    }
                }
                
                _cubeMesh.isoLevel = maxDis - radius;
                _cubeMesh.Rebuild();
            }
        }

        private void OnDrawGizmos()
        {
            if (null != _cubeMesh)
            {
                Gizmos.matrix = _cubeMesh.localToWorld;
                Gizmos.DrawMesh(_cubeMesh.mesh);
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireMesh(_cubeMesh.mesh);
                
                Gizmos.color = Color.white;
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
using UnityEngine;
using UnityEngine.Profiling;

namespace MarchingCubes.Sample
{
    public class SmoothSphereSample : MonoBehaviour, IMarchingCubeReceiver
    {
        public int x, y, z;
        private CubeMeshSmooth _cubeMesh;
        private MeshFilter _meshFilter;
        private float _isoLevel, _maxDis;
        private Vector3 _center;
        
        public float radius = 1f;
  
        void Start()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _cubeMesh = new CubeMeshSmooth(x, y, z, this);
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
                _isoLevel = maxDis - radius;
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
                
                Profiler.BeginSample( "Rebuild");
                _cubeMesh.Rebuild();
                Profiler.EndSample();
                if(null != _meshFilter)
                    _meshFilter.sharedMesh = _cubeMesh.mesh;
            }
        }

        private void OnDrawGizmos()
        {
            if (null == _meshFilter && null != _cubeMesh)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawMesh(_cubeMesh.mesh);
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireMesh(_cubeMesh.mesh);
                
                Gizmos.color = Color.white;
                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        public float GetIsoLevel() => _isoLevel;
        
        public bool IsoPass(float iso) => iso > _isoLevel;
        
        public void OnRebuildCompleted()
        {
            
        }
    }
}
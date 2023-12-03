//****************************************************************************
// File: RealtimeWorldChunk.cs
// Author: Li Nan
// Date: 2023-12-03 12:00
// Version: 1.0
//****************************************************************************

using UnityEngine;
using UnityEngine.Profiling;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class RealtimeWorldChunk : MonoBehaviour, IMarchingCubeReceiver
    {
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private CubeMeshSmooth _cubeMesh;
        private float _isoLevel = 1f;

        [SerializeField] private int chunkX;

        [SerializeField] private int chunkY;

        [SerializeField] private int chunkZ;

        float IMarchingCubeReceiver.GetIsoLevel() => _isoLevel;

        bool IMarchingCubeReceiver.IsoPass(float iso) => iso >= _isoLevel;
        
        void IMarchingCubeReceiver.OnRebuildCompleted()
        {
            _meshFilter.sharedMesh = _cubeMesh.mesh;
            _meshCollider.sharedMesh = _cubeMesh.mesh;
        }

        public void Initialize(int chunkX, int chunkY, int chunkZ, bool closed)
        {
            this.chunkX = chunkX;
            this.chunkY = chunkY;
            this.chunkZ = chunkZ;
            
            int chunkCell = RealtimeWorld.ChunkCell;
            float cellSize = RealtimeWorld.CellSize;
            float chunkSize = chunkCell * cellSize;
            Transform chunkTransform = transform;
            Vector3 localPosition = new Vector3(chunkX, chunkY, chunkZ) * chunkSize;
            chunkTransform.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
            chunkTransform.localScale = new Vector3(cellSize, cellSize, cellSize);

            _meshCollider = GetComponent<MeshCollider>();
            _meshFilter = GetComponent<MeshFilter>();
            _cubeMesh = new CubeMeshSmooth(chunkCell, chunkCell, chunkCell, this);

            float value = closed ? float.MaxValue : 0f;
            for (int i = 1; i < chunkCell; i++)
            {
                for (int j = 1; j < chunkCell; j++)
                {
                    for (int k = 1; k < chunkCell; k++)
                    {
                        _cubeMesh.SetPointISO(i, j, k, value);
                    }
                }
            }
        }

        public bool SetBlock(in Vector3 position, float radius)
        {
            _isoLevel = radius;
            float localScale = RealtimeWorld.CellSize;
            Vector3 center = transform.InverseTransformPoint(position);
            Vector3 radius3 = new Vector3(radius, radius, radius) / localScale;
            Vector3 localMin = center - radius3;
            Vector3 localMax = center + radius3;
            
            int x0 = Mathf.Clamp(Mathf.FloorToInt(localMin.x), 0, _cubeMesh.X);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(localMin.y), 0, _cubeMesh.Y);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(localMin.z), 0, _cubeMesh.Z);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(localMax.x), 0, _cubeMesh.X);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(localMax.y), 0, _cubeMesh.Y);
            int z1 = Mathf.Clamp(Mathf.CeilToInt(localMax.z), 0, _cubeMesh.Z);
            
            bool changed = false;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int z = z0; z <= z1; z++)
                    {
                        Vector3 p = new Vector3(x, y, z);
                        float iso = Vector3.Distance(p, center) * localScale;
                        float oldIso = _cubeMesh.GetPointISO(x, y, z);
                        if( iso < oldIso)
                        {
                            changed = true;
                            _cubeMesh.SetPointISO(x, y, z, iso);
                        }
                    }
                }
            }

            return changed;
        }

        public void RebuildTerrain()
        {
            Profiler.BeginSample("Rebuild");
            _cubeMesh.Rebuild();
            Profiler.EndSample();
        }
    }
}
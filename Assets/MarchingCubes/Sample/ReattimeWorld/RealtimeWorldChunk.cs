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
        private RealtimeWorld _world;
        private float _isoLevel = 1f;
        private Coord _coord;

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

        public void Initialize(RealtimeWorld world, int chunkX, int chunkY, int chunkZ, bool closed)
        {
            this._world = world;
            this.chunkX = chunkX;
            this.chunkY = chunkY;
            this.chunkZ = chunkZ;
            
            int chunkCellNum = RealtimeWorld.ChunkCellNum;
            float size = RealtimeWorld.Size;
            float chunkSize = chunkCellNum * size;
            
            Transform chunkTransform = transform;
            Vector3 localPosition = new Vector3(chunkX, chunkY, chunkZ) * chunkSize;
            localPosition += RealtimeWorld.PosOffset;
            chunkTransform.localPosition = localPosition;
            chunkTransform.localRotation = Quaternion.identity;
            chunkTransform.localScale = new Vector3(size, size, size);
            
            _meshCollider = GetComponent<MeshCollider>();
            _meshFilter = GetComponent<MeshFilter>();
            int chunkMaxCell = RealtimeWorld.ChunkMaxCellNum;
            _cubeMesh = new CubeMeshSmooth(chunkMaxCell, chunkMaxCell, chunkMaxCell, this);
            
            _coord.x = chunkX * chunkCellNum - RealtimeWorld.CellOffset;
            _coord.y = chunkY * chunkCellNum - RealtimeWorld.CellOffset;
            _coord.z = chunkZ * chunkCellNum - RealtimeWorld.CellOffset;
            for (int i = 0; i <= chunkMaxCell; i++)
            {
                for (int j = 0; j <= chunkMaxCell; j++)
                {
                    for (int k = 0; k <= chunkMaxCell; k++)
                    {
                        float iso = _world.GetPointIso(_coord.x + i, _coord.y + j, _coord.z + k);
                        _cubeMesh.SetPointISO(i, j, k, iso);
                    }
                }
            }
        }

        public bool SetBlock(in Coord min, in Coord max, float radius)
        {
            _isoLevel = radius;
            
            // 转换到本地空间坐标范围
            int x0 = Mathf.Clamp(min.x - _coord.x, 0, _cubeMesh.X);
            int y0 = Mathf.Clamp(min.y - _coord.y, 0, _cubeMesh.Y);
            int z0 = Mathf.Clamp(min.z - _coord.z, 0, _cubeMesh.Z);
            int x1 = Mathf.Clamp(max.x - _coord.x, 0, _cubeMesh.X);
            int y1 = Mathf.Clamp(max.y - _coord.y, 0, _cubeMesh.Y);
            int z1 = Mathf.Clamp(max.z - _coord.z, 0, _cubeMesh.Z);
            
            bool changed = false;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int z = z0; z <= z1; z++)
                    {
                        float iso = _world.GetPointIso(x + _coord.x, y + _coord.y, z + _coord.z);
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
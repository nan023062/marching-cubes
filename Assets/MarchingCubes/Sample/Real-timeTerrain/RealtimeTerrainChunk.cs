using UnityEngine;
using UnityEngine.Profiling;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class RealtimeTerrainChunk : MonoBehaviour, IMarchingCubeReceiver
    {
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private CubeMeshSmooth _cubeMesh;
        
        [SerializeField]
        private int chunkX;
        
        [SerializeField]
        private int chunkZ;
        
        [Range( 0.001f, 1f),SerializeField]
        private float perlinNoiseScale = 1f / 7;
        
        float IMarchingCubeReceiver.GetIsoLevel() => RealtimeTerrain.ChunkHeight + 0.1F;
        
        bool IMarchingCubeReceiver.IsoPass(float iso) => iso < RealtimeTerrain.ChunkHeight + 0.1F;
        
        void IMarchingCubeReceiver.OnRebuildCompleted()
        {
            _meshFilter.sharedMesh = _cubeMesh.mesh;
            _meshCollider.sharedMesh = _cubeMesh.mesh;
        }
        
        public void Initialize()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _meshFilter = GetComponent<MeshFilter>();
            _cubeMesh = new CubeMeshSmooth(
                RealtimeTerrain.ChunkCell, 
                RealtimeTerrain.ChunkHeight, 
                RealtimeTerrain.ChunkCell, this);
        }
        
        public void RebuildTerrain(int chunkX, int chunkZ)
        {
            this.chunkX = chunkX;
            this.chunkZ = chunkZ;
            int chunkCell = RealtimeTerrain.ChunkCell;
            float cellSize = RealtimeTerrain.CellSize;
            float chunkSize = chunkCell * cellSize;
            Transform chunkTransform = transform;
            Vector3 localPosition = new Vector3(chunkX, 0F, chunkZ) * chunkSize;
            chunkTransform.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
            chunkTransform.localScale = new Vector3(cellSize, cellSize, cellSize);

            for (int i = 0; i <= chunkCell; i++)
            {
                for (int k = 0; k <= chunkCell; k++)
                {
                    float x = (chunkX * chunkCell + i) * perlinNoiseScale;
                    float z = (chunkZ * chunkCell + k) * perlinNoiseScale;
                    
                    float height = Mathf.PerlinNoise(x, z) * RealtimeTerrain.ChunkHeight;
                    for (int j = 0; j <= RealtimeTerrain.ChunkHeight; j++)
                    {
                        _cubeMesh.SetPointISO(i, j, k, height + j);  
                    }
                }
            }
            
            Profiler.BeginSample("Rebuild");
            _cubeMesh.Rebuild();
            Profiler.EndSample();
        }
    }
}
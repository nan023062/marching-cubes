using UnityEngine;

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
        
        public RealtimeTerrain Terrain;
        
        float IMarchingCubeReceiver.GetIsoLevel()
        {
            return RealtimeTerrain.ChunkHeight;
        }
        
        bool IMarchingCubeReceiver.IsoPass(float iso) => iso < RealtimeTerrain.ChunkHeight;
        
        void IMarchingCubeReceiver.OnRebuildCompleted()
        {
            _meshFilter.sharedMesh = _cubeMesh.mesh;
            _meshCollider.sharedMesh = _cubeMesh.mesh;
        }
        
        private void Awake()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _meshFilter = GetComponent<MeshFilter>();
            int cellWidth = RealtimeTerrain.ChunkSize;
            int cellHeight = RealtimeTerrain.ChunkHeight;
            _cubeMesh = new CubeMeshSmooth(cellWidth, cellHeight, cellWidth, this);
        }
        
        private void OnDestroy()
        {
            _cubeMesh = null;
        }
        
        public void RebuildTerrain()
        {
            // TODO: random terrain
            // 当前设置
            int cellWidth = RealtimeTerrain.ChunkSize;
            int cellHeight = RealtimeTerrain.ChunkHeight;
            for (int i = 0; i <= cellWidth; i++)
            {
                for (int j = 0; j <= cellHeight; j++)
                {
                    for (int k = 0; k <= cellWidth; k++)
                    {
                        Vector3 pos = new Vector3(i, j, k);
                        pos = transform.TransformPoint(pos);
                        float height = Terrain.GetTerrianHeight(pos);
                        _cubeMesh.SetPointISO(i, j, k, height + j);  
                    }
                }
            }
            
            _cubeMesh.Rebuild();
        }
    }
}
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MarchingCubes.Sample
{
    public class RealtimeTerrain : MonoBehaviour
    {
        public const int ChunkSize = 32;
        public const int ChunkHeight = 16;
        public const int ChunkHeightMapSize = ChunkSize * 8;
        
        [SerializeField, Header( "Chunk Prefab")]
        private RealtimeTerrainChunk _chunkPrefab;
        
        private int _gridX, _gridZ;
        private bool _initialized;
        private RealtimeTerrainChunk _centerChunk;
        private float[,] _heightMap;
        
        [UnityEngine.Range( 0f, 256f)]
        public float scale = 1f;
        public Texture2D _heightMapTexture;
        private void Awake()
        {
            _heightMapTexture = new Texture2D(ChunkHeightMapSize + 1, ChunkHeightMapSize + 1, TextureFormat.R8, false);
            _heightMap = new float[ChunkHeightMapSize + 1, ChunkHeightMapSize + 1];
            ResetHeightMap();
            GetComponent<Renderer>().sharedMaterial.mainTexture = _heightMapTexture;
        }
        
        [ContextMenu("Reset Height Map")]
        private void ResetHeightMap()
        {
            for (int i = 0; i <= ChunkHeightMapSize; i++)
            {
                for (int j = 0; j <= ChunkHeightMapSize; j++)
                {
                    float x = i / (float)ChunkHeightMapSize * scale;
                    float z = j / (float)ChunkHeightMapSize * scale;
                    float noise = Mathf.PerlinNoise(x, z);
                    _heightMapTexture.SetPixel(i, j, new Color(noise, noise, noise));
                    float height = noise;
                    _heightMap[i, j] = height * ChunkHeight;
                }
            }
            _heightMapTexture.Apply();
        }
        
        public void Update()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 spot = mainCamera.transform.position;
                int gridX = Mathf.FloorToInt(spot.x / ChunkSize);
                int gridZ = Mathf.FloorToInt(spot.z / ChunkSize);
                if (_initialized && (gridX != _gridX || gridZ != _gridZ))
                {
                    UpdateChunks(gridX, gridZ);
                }
                else if (!_initialized)
                {
                    UpdateChunks(gridX, gridZ);
                }
            }
        }
        
        private void UpdateChunks(int gridX, int gridZ)
        {
            _gridX = gridX;
            _gridZ = gridZ;

            if (_centerChunk == null)
            {
                GameObject prefab = Object.Instantiate(_chunkPrefab.gameObject);
                prefab.SetActive( true);
                _centerChunk = prefab.GetComponent<RealtimeTerrainChunk>();
                _centerChunk.Terrain = this;
            }
            
            Transform chunkTransform = _centerChunk.transform;
            chunkTransform.rotation = Quaternion.identity;
            chunkTransform.position = new Vector3(gridX * ChunkSize, 0F, gridZ * ChunkSize);
            _centerChunk.RebuildTerrain();
        }
        
        public float GetTerrianHeight(in Vector3 worldPos)
        {
            // 采样_heightMap
            int x = Mathf.RoundToInt(worldPos.x);
            int z = Mathf.FloorToInt(worldPos.z);
            int halfSize = ChunkHeightMapSize / 2 ;
            x = Mathf.Clamp( x + halfSize, 0, ChunkHeightMapSize);
            z = Mathf.Clamp( z + halfSize, 0, ChunkHeightMapSize);
            return _heightMap[x, z];
        }
    }
}
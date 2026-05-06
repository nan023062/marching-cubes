using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MarchingCubes.Sample
{
    public class RealtimeTerrain : MonoBehaviour
    {
        public const int ChunkCell = 32;
        public const int ChunkHeight = 16;
        public const float CellSize = 0.5f;
        public const int Offset = 5;
        public const int ViewSize = Offset * 2 + 1;

        [SerializeField, Header("Spot")] private Transform _spot;

        [SerializeField, Header("Chunk Prefab")]
        private RealtimeTerrainChunk _chunkPrefab;
        
        private bool _initialized;
        private ViewPort _viewPort;
        private readonly Dictionary<long, Chunk> _chunks = new Dictionary<long, Chunk>();
        private readonly LinkedList<long> _dirtyChunks = new LinkedList<long>();

        public void Update()
        {
            if (_spot != null)
            {
                float chunkSize = ChunkCell * CellSize;
                Vector3 spot = _spot.position;
                int chunkX = Mathf.FloorToInt(spot.x / chunkSize);
                int chunkZ = Mathf.FloorToInt(spot.z / chunkSize);
                ViewPort viewPort = new ViewPort(new ChunkId(chunkX, chunkZ));
                if (_initialized && _viewPort != viewPort)
                {
                    UpdateViewChunks(viewPort);
                }
                else if (!_initialized)
                {
                    _initialized = true;
                    UpdateViewChunks(viewPort);
                }


                TickDirtyChunks();
            }
        }

        // 实现 以 _spot 为中心，每次移动超过一个 ChunkCell 就更新周围 5 * 5 个 Chunk
        private void UpdateViewChunks(in ViewPort viewPort)
        {
            ViewPort oldViewPort = _viewPort;
            _viewPort = viewPort;
            
            int minX = Math.Min(viewPort.x0, oldViewPort.x0);
            int maxX = Math.Max(viewPort.x1, oldViewPort.x1);
            int minZ = Math.Min(viewPort.z0, oldViewPort.z0);
            int maxZ = Math.Max(viewPort.z1, oldViewPort.z1);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    ChunkId id = new ChunkId(x, z);
                    if (_viewPort.Contains(x, z))
                    {
                        if (!_chunks.TryGetValue(id, out Chunk chunk))
                        {
                            chunk = new Chunk { used = true, };
                            _chunks.Add(id, chunk);
                        }
                        else
                        {
                            chunk.used = true;
                        }
                    }   
                    else if (_chunks.TryGetValue(id, out Chunk chunk))
                    {
                        chunk.used = false;
                    } 
                }
            }
            
            // 3. 移除不再使用的 Chunk 生成新的 Chunk
            foreach (KeyValuePair<long, Chunk> pair in _chunks)
            {
                Chunk chunk = pair.Value;
                bool hasGen = null != chunk.obj;
                if (chunk.used != hasGen)
                {
                    _dirtyChunks.AddLast(pair.Key);
                }
            }
        }
        
        private void TickDirtyChunks()
        {
            while (_dirtyChunks.Count > 0)
            {
                ChunkId id = _dirtyChunks.First.Value;
                _dirtyChunks.RemoveFirst();
                if (_chunks.TryGetValue(id, out Chunk chunk))
                {
                    if (!chunk.used)
                    {
                        _chunks.Remove(id);
                        if (null != chunk.obj)
                        {
                            Object.Destroy(chunk.obj.gameObject);
                            break;
                        }
                    }
                    else
                    {
                        if (chunk.obj == null)
                        {
                            GameObject go = Object.Instantiate(_chunkPrefab.gameObject, transform);
                            go.name = $"Chunk_{id.x}_{id.z}";
                            go.SetActive( true );
                            chunk.obj = go.GetComponent<RealtimeTerrainChunk>();
                            chunk.obj.Initialize();
                            chunk.obj.RebuildTerrain(id.x, id.z);
                            break;
                        }  
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        struct ChunkId
        {
            [FieldOffset(0)] private long _id;
            [FieldOffset(0)] public int x;
            [FieldOffset(4)] public int z;
            
            public ChunkId(int x, int z)
            {
                _id = 0;
                this.x = x;
                this.z = z;
            }
            
            public static implicit operator long(ChunkId id)
            {
                return id._id;
            }
            
            public static implicit operator ChunkId(long id)
            {
                return new ChunkId {_id = id};
            }
            
            public override int GetHashCode()
            {
                return x ^ z;
            }
            
            public static bool operator ==(ChunkId a, ChunkId b)
            {
                return a._id == b._id;
            }
            
            public static bool operator !=(ChunkId a, ChunkId b)
            {
                return !(a == b);
            }

            public override bool Equals(object obj)
            {
                return obj is ChunkId other && other._id == _id;
            }
        }
        
        class Chunk
        {
            public bool used;
            public RealtimeTerrainChunk obj;
        }

        readonly struct ViewPort : IEquatable<ViewPort>
        {
            public readonly int x0;
            public readonly int z0;
            public readonly int x1;
            public readonly int z1;
            
            public ViewPort(ChunkId id)
            {
                x0 = id.x - Offset;
                z0 = id.z - Offset;
                x1 = id.x + Offset;
                z1 = id.z + Offset;
            }
            
            public bool Contains(int x, int z)
            {
                return x0 <= x && x <= x1 && z0 <= z && z <= z1;
            }
            
            public static bool operator ==(ViewPort a, ViewPort b)
            {
                return a.x0 == b.x0 && a.z0 == b.z0 && a.x1 == b.x1 && a.z1 == b.z1;
            }
            
            public static bool operator !=(ViewPort a, ViewPort b)
            {
                return !(a == b);
            }

            public bool Equals(ViewPort other)
            {
                return x0 == other.x0 && z0 == other.z0 && x1 == other.x1 && z1 == other.z1;
            }
            
            public override bool Equals(object obj)
            {
                return obj is ViewPort other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = x0;
                    hash = (hash * 397) ^ z0;
                    hash = (hash * 397) ^ x1;
                    hash = (hash * 397) ^ z1;
                    return hash;
                }
            }
        }
    }
}
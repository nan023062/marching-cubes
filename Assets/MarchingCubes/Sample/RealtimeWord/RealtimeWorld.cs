//****************************************************************************
// File: RealtimeWorld.cs
// Author: Li Nan
// Date: 2023-12-03 12:00
// Version: 1.0
//****************************************************************************

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MarchingCubes.Sample
{
    public class RealtimeWorld : MonoBehaviour
    {
        public const int ChunkCell = 32;
        public const float CellSize = 0.25f;
        public const int Offset = 1;
        public const int ViewSize = Offset * 2 + 1;

        public static RealtimeWorld Instance { private set; get; }

        [SerializeField, Header("Spot")] private Transform _spot;

        [SerializeField, Header("Chunk Prefab")]
        private RealtimeWorldChunk _chunkPrefab;
        
        private bool _initialized;
        private ViewPort _viewPort;
        private readonly Dictionary<long, Chunk> _chunks = new();
        private readonly LinkedList<long> _dirtyChunks = new();

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (_spot != null)
            {
                float chunkSize = ChunkCell * CellSize;
                Vector3 spot = _spot.position;
                short chunkX = (short)Mathf.FloorToInt(spot.x / chunkSize);
                short chunkY = (short)Mathf.FloorToInt(spot.y / chunkSize);
                short chunkZ = (short)Mathf.FloorToInt(spot.z / chunkSize);
                ViewPort viewPort = new ViewPort(new ChunkId(chunkX, chunkY, chunkZ));
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
        
        private void OnDestroy()
        {
            Instance = null;
        }

        // 实现 以 _spot 为中心，每次移动超过一个 ChunkCell 就更新周围 5 * 5 个 Chunk
        private void UpdateViewChunks(in ViewPort viewPort)
        {
            ViewPort oldViewPort = _viewPort;
            _viewPort = viewPort;
            
            short minX = Math.Min(viewPort.x0, oldViewPort.x0);
            short maxX = Math.Max(viewPort.x1, oldViewPort.x1);
            short minY = Math.Min(viewPort.y0, oldViewPort.y0);
            short maxY = Math.Max(viewPort.y1, oldViewPort.y1);
            short minZ = Math.Min(viewPort.z0, oldViewPort.z0);
            short maxZ = Math.Max(viewPort.z1, oldViewPort.z1);
            
            for (short x = minX; x <= maxX; x++)
            {
                for (short y = minY; y <= maxY; y++)
                {
                    for (short z = minZ; z <= maxZ; z++)
                    {
                        ChunkId id = new ChunkId(x, y, z);
                        if (_viewPort.Contains(id))
                        {
                            if (!_chunks.TryGetValue(id, out Chunk chunk))
                            {
                                chunk = new Chunk { used = true, };
                                _chunks.Add(id, chunk);
                                _dirtyChunks.AddLast(id);
                            }
                            else
                            {
                                chunk.used = true;
                                if (chunk.obj == null || !chunk.obj.gameObject.activeSelf)
                                {
                                    _dirtyChunks.AddLast(id);
                                }
                            }
                        }
                        else if (_chunks.TryGetValue(id, out Chunk chunk))
                        {
                            chunk.used = false;
                            if (chunk.obj != null)
                            {
                                _dirtyChunks.AddLast(id);
                            }
                        }
                    }
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
                        //_chunks.Remove(id);
                        if (null != chunk.obj)
                        {
                            chunk.obj.gameObject.SetActive(false);
                            break;
                        }
                    }
                    else
                    {
                        if (chunk.obj == null)
                        {
                            GameObject go = Object.Instantiate(_chunkPrefab.gameObject, transform);
                            go.name = $"Chunk<{id.x}_{id.y}_{id.z}>";
                            go.SetActive(true);
                            chunk.obj = go.GetComponent<RealtimeWorldChunk>();
                            chunk.obj.Initialize( id.x, id.y, id.z, id != 0);
                        }
                        else if (!chunk.obj.gameObject.activeSelf)
                        {
                            chunk.obj.gameObject.SetActive(true);
                        }
                        chunk.obj.RebuildTerrain();
                        break;
                    }
                }
            }
        }
        
        public void SetBlock(Vector3 position, float radius)
        {
            Vector3 min = position - Vector3.one * radius;
            ChunkId minId = PositionToChunkId(min);
            Vector3 max = position + Vector3.one * radius;
            ChunkId maxId = PositionToChunkId(max);
            
            for (short x = minId.x; x <= maxId.x; x++)
            {
                for (short y = minId.y; y <= maxId.y; y++)
                {
                    for (short z = minId.z; z <= maxId.z; z++)
                    {
                        ChunkId id = new ChunkId(x, y, z);
                        if (_chunks.TryGetValue(id, out Chunk chunk))
                        {
                            // ReSharper disable once Unity.PerformanceCriticalCodeNullComparison
                            if (null != chunk.obj)
                            {
                                bool dirty = chunk.obj.SetBlock(position, radius);
                                if( dirty )
                                    _dirtyChunks.AddLast(id);
                            }
                        }
                    }
                }
            }
        }
        
        private ChunkId PositionToChunkId(Vector3 position)
        {
            float chunkSize = ChunkCell * CellSize;
            short chunkX = (short)Mathf.FloorToInt(position.x / chunkSize);
            short chunkY = (short)Mathf.FloorToInt(position.y / chunkSize);
            short chunkZ = (short)Mathf.FloorToInt(position.z / chunkSize);
            return new ChunkId(chunkX, chunkY, chunkZ);
        }


        [StructLayout(LayoutKind.Explicit)]
        struct ChunkId
        {
            [FieldOffset(0)] private long _id;
            [FieldOffset(0)] public short x;
            [FieldOffset(2)] public short y;
            [FieldOffset(4)] public short z;
            
            public ChunkId(short x, short y, short z)
            {
                _id = 0;
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public static implicit operator long(ChunkId id)
            {
                return id._id;
            }

            public static implicit operator ChunkId(long id)
            {
                return new ChunkId { _id = id };
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
            public RealtimeWorldChunk obj;
        }

        readonly struct ViewPort : IEquatable<ViewPort>
        {
            public readonly short x0;
            public readonly short z0;
            public readonly short y0;
            public readonly short y1;
            public readonly short x1;
            public readonly short z1;

            public ViewPort(ChunkId id)
            {
                x0 = (short)(id.x - (short)Offset);
                z0 = (short)(id.z - (short)Offset);
                x1 = (short)(id.x + (short)Offset);
                z1 = (short)(id.z + (short)Offset);
                y0 = (short)(id.y - (short)Offset);
                y1 = (short)(id.y + (short)Offset);
            }

            public bool Contains(in ChunkId id)
            {
                return x0 <= id.x && id.x <= x1 && z0 <= id.z && id.z <= z1 && y0 <= id.y && id.y <= y1;
            }

            public static bool operator ==(ViewPort a, ViewPort b)
            {
                return a.x0 == b.x0 && a.z0 == b.z0 && a.x1 == b.x1 && a.z1 == b.z1 && a.y0 == b.y0 && a.y1 == b.y1;
            }

            public static bool operator !=(ViewPort a, ViewPort b)
            {
                return !(a == b);
            }

            public bool Equals(ViewPort other)
            {
                return this == other;
            }

            public override bool Equals(object obj)
            {
                return obj is ViewPort other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(x0, y0, z0, x1, y1, z1);
            }
        }
    }
}
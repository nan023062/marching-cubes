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
        public const int ChunkCellNum = 32;
        public const int CellOffset = 1;
        public const int ChunkMaxCellNum = ChunkCellNum + 2 * CellOffset;
        public static readonly Vector3 PosOffset = new Vector3(-CellOffset, -CellOffset, -CellOffset) * Size;
        public const float Size = 0.25f;
        public const int Offset = 2;
        public const int ViewSize = Offset * 2 + 1;
        
        public static RealtimeWorld Instance { private set; get; }

        [SerializeField, Header("Spot")] private Transform _spot;
        
        [SerializeField, Header("Chunk Prefab")]
        private RealtimeWorldChunk _chunkPrefab;
        
        [SerializeField, Header("World Capacity")]
        private short _halfCapacity = 64;
        
        private bool _initialized;
        private ViewPort _viewPort;
        private readonly Dictionary<long, Chunk> _chunks = new();
        private readonly LinkedList<long> _dirtyChunks = new();
        private float[,,] _isoValues;
        private ChunkId _min, _max;
        private int _pointLength, _halfPointLength;

        private void Awake()
        {
            Instance = this;
            _halfPointLength = _halfCapacity * ChunkCellNum;
            _pointLength = _halfPointLength * 2 + 1;
            _isoValues = new float[_pointLength, _pointLength, _pointLength];
            _min = new ChunkId((short)-_halfCapacity,(short)-_halfCapacity, (short)-_halfCapacity);
            _max = new ChunkId(_halfCapacity,_halfCapacity, _halfCapacity);
            for (int x = 0; x < _pointLength; x++)
            {
                for (int y = 0; y < _pointLength; y++)
                {
                    for (int z = 0; z < _pointLength; z++)
                        _isoValues[x, y, z] = float.MaxValue;
                }
            }
        }
        
        public float GetPointIso(int x, int y, int z)
        {
            x = Mathf.Clamp(x + _halfPointLength, 0, _pointLength - 1);
            y = Mathf.Clamp(y + _halfPointLength, 0, _pointLength - 1);
            z = Mathf.Clamp(z + _halfPointLength, 0, _pointLength - 1);
            return _isoValues[x, y, z];
        }

        private void Update()
        {
            if (_spot != null)
            {
                float chunkSize = ChunkCellNum * Size;
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
                            chunk.obj.Initialize(this, id.x, id.y, id.z, id != 0);
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
        
        public void SetBlock(in Vector3 position, float radius)
        {
            Vector3 center = position;
            Vector3 radius3 = new Vector3(radius, radius, radius);
            Vector3 minCorner = center - radius3;
            Vector3 maxCorner = center + radius3;
            
            // 1 更新iso 
            Vector3 min = minCorner / Size;
            Vector3 max = maxCorner / Size;
            Coord coord0 = new Coord(Mathf.FloorToInt(min.x), Mathf.FloorToInt(min.y), Mathf.FloorToInt(min.z));
            Coord coord1 = new Coord(Mathf.FloorToInt(max.x), Mathf.FloorToInt(max.y), Mathf.FloorToInt(max.z));

            bool changed = false;

            for (int x = coord0.x; x <= coord1.x; x++)
            {
                for (int y = coord0.y; y <= coord1.y; y++)
                {
                    for (int z = coord0.z; z <= coord1.z; z++)
                    {
                        Vector3 p = new Vector3(x, y, z) * Size;
                        float iso = Vector3.Distance(p, center);
                        int x0 = Mathf.Clamp(x + _halfPointLength, 0, _pointLength - 1);
                        int y0 = Mathf.Clamp(y + _halfPointLength, 0, _pointLength - 1);
                        int z0 = Mathf.Clamp(z + _halfPointLength, 0, _pointLength - 1);
                        float oldIso = _isoValues[x0, y0, z0];
                        if( iso < oldIso)
                        {
                            changed = true;
                            _isoValues[x0, y0, z0] = iso;
                        }
                    }
                }
            }

            // 2 更新影响的chunk
            if (changed)
            {
                ChunkId minId = PositionToChunkId(minCorner);
                ChunkId maxId = PositionToChunkId(maxCorner);
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
                                    bool dirty = chunk.obj.SetBlock(coord0, coord1, radius);
                                    if( dirty )
                                        _dirtyChunks.AddLast(id);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private ChunkId PositionToChunkId(Vector3 position)
        {
            float chunkSize = ChunkCellNum * Size;
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
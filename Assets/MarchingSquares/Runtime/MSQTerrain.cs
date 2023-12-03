//****************************************************************************
// File: Terrain.cs
// Author: Li Nan
// Date: 2023-08-30 12:00
// Version: 1.0
//****************************************************************************

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Color = UnityEngine.Color;

namespace MarchingSquares
{
    public partial class MSQTerrain
    {
        public readonly int width, length, height;
        public readonly float unit;
        private readonly Point[,] _points;
        public readonly Matrix4x4 localToWorld;
        public readonly Matrix4x4 worldToLocal;
        public readonly ITextureLoader loader;
        public readonly Mesh mesh;
        private readonly Vector3[] _vertices;
        private readonly Vector2[] _uvs;
        private readonly Queue<(int, int)> _queue = new(64);

        public MSQTerrain(int width, int length, int height, float unit, Vector3 position, ITextureLoader textureLoader)
        {
            this.width = width;
            this.length = length;
            this.height = height;
            this.unit = unit;
            loader = textureLoader;
            localToWorld = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * unit);
            worldToLocal = localToWorld.inverse;
            _points = new Point[length + 1, width + 1];
            for (int i = 0; i <= length; i++)
            {
                for (int j = 0; j <= width; j++)
                    _points[i, j] = new Point();
            }

            // generate mesh
            int totalTriangle = length * width * 2;
            int totalVertex = totalTriangle * 3;
            mesh = new Mesh();
            _vertices = new Vector3[totalVertex];
            int[] triangles = new int[totalVertex];
            _uvs = new Vector2[totalVertex];
      
            // filter indices and uvs
            for (int i = 0; i < totalVertex; i++)
            {
                triangles[i] = i;
                _uvs[i] =Vector2.zero;
            }

            // filter vertices
            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < length; x++)
                {
                    Vector3 p0 = new Vector3(x, 0, z);
                    Vector3 p1 = new Vector3(x, 0, z + 1);
                    Vector3 p2 = new Vector3(x + 1, 0, z + 1);
                    Vector3 p3 = new Vector3(x + 1, 0, z);

                    int index = (x + length * z) * 6;
                    _vertices[index++] = p0;
                    _vertices[index++] = p1;
                    _vertices[index++] = p3;
                    _vertices[index++] = p3;
                    _vertices[index++] = p1;
                    _vertices[index] = p2;
                }
            }

            mesh.vertices = _vertices;
            mesh.triangles = triangles;
            
            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < length; x++)
                {
                    PaintChunkTexture(x, z, 0, true);
                    PaintChunkTexture(x, z, 0, false);
                }
            }
            mesh.uv = _uvs;
            mesh.RecalculateNormals();
        }

        public bool PaintTexture(Brush brush, int layer, bool add)
        {
            layer = Math.Clamp(layer, 0, 3);
            (Vector3 center, float radiusSqr) = CalculateArea(brush, out int minX,
                out int minZ, out int maxX, out int maxZ);
            
            bool dirty = false;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector2 d = new Vector2(x - center.x, z - center.z);
                    if (d.sqrMagnitude <= radiusSqr)
                    {
                        if(PaintChunkTexture(x, z, layer, add))
                            dirty = true;
                    }
                }
            }

            if (dirty)
            {
                mesh.uv = _uvs;
            }
            
            return dirty;
        }

        public bool BrushMapHigh(Brush brush, int delta)
        {
            _queue.Clear();
            (Vector3 center, float radiusSqr) = CalculateArea(brush, out int minX,
                out int minZ, out int maxX, out int maxZ);
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector2 d = new Vector2(x - center.x, z - center.z);
                    if(d.sqrMagnitude <= radiusSqr)
                        DetectChunkUpdated(x, z, delta);
                }
            }
            
            bool dirty = _queue.Count > 0;

            // Breadth-first recursive height value to ensure that the difference
            // between adjacent chunks is not greater than 1
            while (_queue.Count > 0)
            {
                (int x, int z) = _queue.Dequeue();
                ref Point origin = ref _points[x, z];
                // 递归相邻4个chunk
                if (x > 0)
                {
                    ref readonly var chunk = ref _points[x - 1, z];
                    int d = chunk.high - origin.high;
                    if (d > 1) DetectChunkUpdated(x - 1, z, -1);
                    else if (d < -1) DetectChunkUpdated(x - 1, z, 1);
                }

                if (x < length)
                {
                    ref readonly var chunk = ref _points[x + 1, z];
                    int d = chunk.high - origin.high;
                    if (d > 1) DetectChunkUpdated(x + 1, z, -1);
                    else if (d < -1) DetectChunkUpdated(x + 1, z, 1);
                }

                if (z > 0)
                {
                    ref readonly var chunk = ref _points[x, z - 1];
                    int d = chunk.high - origin.high;
                    if (d > 1) DetectChunkUpdated(x, z - 1, -1);
                    else if (d < -1) DetectChunkUpdated(x, z - 1, 1);
                }

                if (z < width)
                {
                    ref readonly var chunk = ref _points[x, z + 1];
                    int d = chunk.high - origin.high;
                    if (d > 1) DetectChunkUpdated(x, z + 1, -1);
                    else if (d < -1) DetectChunkUpdated(x, z + 1, 1);
                }
            }

            // recalculate if dirty
            if (dirty)
            {
                mesh.vertices = _vertices;
                mesh.RecalculateNormals();
            }
            return dirty;
        }

        private (Vector3,float) CalculateArea(Brush brush, out int minX, out int minZ, out int maxX, out int maxZ)
        {
            float radius = brush.Size * 0.5f;
            Vector3 half = Vector3.one * radius;
            float radiusSqr = radius * radius;
            Vector3 center = worldToLocal.MultiplyPoint(brush.transform.position);
            Vector3 min = center - half;
            Vector3 max = center + half;
            minX = Mathf.Clamp(Mathf.CeilToInt(min.x), 0, length);
            minZ = Mathf.Clamp(Mathf.CeilToInt(min.z), 0, width);
            maxX = Mathf.Clamp(Mathf.FloorToInt(max.x), 0, length);
            maxZ = Mathf.Clamp(Mathf.FloorToInt(max.z), 0, width);
            return (center, radiusSqr);
        }

        private void DetectChunkUpdated(int x, int z, int d)
        {
            ref var chunk = ref _points[x, z];
            sbyte high = (sbyte)Math.Clamp(d + chunk.high, -64, 64);
            if (high != chunk.high)
            {
                chunk.high = high;
                Vector3 p = new Vector3(x, chunk.high, z);

                // 左下 - 5 号点
                if (x > 0 && z > 0)
                {
                    int gridLB = x - 1 + (z - 1) * length;
                    int index = gridLB * 6;
                    _vertices[index + 5] = p;
                }

                // 左上 - 2、3号点
                if (x > 0 && z < width)
                {
                    int gridLT = x - 1 + z * length;
                    int index = gridLT * 6;
                    _vertices[index + 2] = p;
                    _vertices[index + 3] = p;
                }

                // 右上 - 0号点
                if (x < length && z < width)
                {
                    int gridRT = x + z * length;
                    int index = gridRT * 6;
                    _vertices[index + 0] = p;
                }

                // 右下 - 1、4 号点
                if (x < length && z > 0)
                {
                    int gridRB = x + (z - 1) * length;
                    int index = gridRB * 6;
                    _vertices[index + 1] = p;
                    _vertices[index + 4] = p;
                }

                _queue.Enqueue((x, z));
            }
        }

        private bool PaintChunkTexture(int x, int z, int layer, bool add)
        {
            ref var chunk = ref _points[x, z];
            int bit = 1 << layer;
            
            int texLayer = chunk.texLayer;
            if (add) texLayer |= bit;
            else texLayer &= ~bit;
            
            if (texLayer != chunk.texLayer)
            {
                chunk.texLayer = (byte)texLayer;
                var texture = loader.GetTexture(layer);
                // 暂时只支持1层
                //ref Vector2[] uvs = ref _uvs[layer];
                var uvs = _uvs;
                
                // 左下 - 5 号点
                if (x > 0 && z > 0)
                {
                    UpdateGridUVs(ref uvs, texture,x - 1, z - 1, bit);
                }

                // 左上 - 2、3号点
                if (x > 0 && z < width)
                {
                    UpdateGridUVs(ref uvs, texture,x - 1, z, bit);
                }

                // 右上 - 0号点
                if (x < length && z < width)
                {
                    UpdateGridUVs(ref uvs, texture, x , z, bit);
                }

                // 右下 - 1、4 号点
                if (x < length && z > 0)
                {
                    UpdateGridUVs(ref uvs, texture, x, z - 1, bit);
                }
                
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateGridUVs(ref Vector2[] uvs, MSQTexture texture, int x, int z, int bit)
        {
            // 确定当前grid的纹理索引
            int index = 0;
            
            ref readonly var chunkLB = ref _points[x, z];
            if ((chunkLB.texLayer & bit) > 0)
                index |= 1 << 3;
            
            ref readonly var chunkLT = ref _points[x, z + 1];
            if ((chunkLT.texLayer & bit) > 0)
                index |= 1 << 2;
            
            ref readonly var chunkRT = ref _points[x + 1, z + 1];
            if ((chunkRT.texLayer & bit) > 0)
                index |= 1 << 1;
            
            ref readonly var chunkRB = ref _points[x + 1, z];
            if ((chunkRB.texLayer & bit) > 0)
                index |= 1 << 0;

            // 更新6个点的纹理uv
            int vIndex = (x + z * length) * 6;
            Coord coord = texture.coord[index];
            Vector2 offset = MSQTexture.offset;
            Vector2 min = new Vector2(offset.x * coord.x, offset.y * coord.y);
            
            // 0 号点
            uvs[vIndex + 0] = min;
            
            // 1 和 4 号点
            Vector2 uv14 = min + new Vector2(0, offset.y);
            uvs[vIndex + 1] = uvs[vIndex + 4] = uv14;
            
            // 2 和 3 号点
            Vector2 uv23 = min + new Vector2(offset.x, 0);
            uvs[vIndex + 2] = uvs[vIndex + 3] = uv23;
            
            // 5 号点
            uvs[vIndex + 5] = min + offset;
        }
        
        public void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            //Gizmos.DrawWireMesh(mesh);

            Gizmos.color = Color.red;
            for (int i = 0; i <= length; i++)
            {
                for (int j = 0; j <= width; j++)
                {
                    ref readonly var point1 = ref _points[i, j];
                    Vector3 p1 = new Vector3(i, point1.high, j);
                    Gizmos.DrawSphere(p1, 0.05F);
                }
            }
        }
    }


    public interface ITextureLoader
    {
        MSQTexture GetTexture(int layer);
    }
}
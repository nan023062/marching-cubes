//****************************************************************************
// File: BlockBuilding.cs
// Author: Li Nan
// Date: 2023-09-10 12:00
// Version: 1.0
//****************************************************************************

using UnityEngine;

namespace MarchingCubes.Sample
{
    public class BlockBuilding
    {
        public readonly int X, Y, Z;
        private readonly Point[,,] _points;
        private readonly Cube[,,] _cubes;
        private Matrix4x4 _localToWorld;
        public readonly IMeshStore MeshStore;
        
        public Matrix4x4 localToWorld
        {
            get => _localToWorld;
            set => _localToWorld = value;
        }

        struct Cube
        {
            public readonly sbyte x, y, z;
            private readonly BlockBuilding building;
            public GameObject mesh;

            public ref readonly Point this[int index]
            {
                get
                {
                    ref (int x, int y, int z) v = ref CubeTable.Vertices[index];
                    return ref building._points[x + v.x, y + v.y, z + v.z];
                }
            }

            public Cube(BlockBuilding building, int x, int y, int z)
            {
                this.building = building;
                this.x = (sbyte)x;
                this.y = (sbyte)y;
                this.z = (sbyte)z;
                mesh = null;
            }
        }

        public void DrawPoints()
        {
            Color color = Gizmos.color;
            Gizmos.matrix = localToWorld;

            foreach (var point in _points)
            {
                Gizmos.color = point.iso > 0 ? Color.red : Color.green;
                Gizmos.DrawSphere(point.position, 0.1f);
            }

            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.identity;
        }

        public BlockBuilding(int x, int y, int z, Matrix4x4 localToWorld, IMeshStore meshStore)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            MeshStore = meshStore;
            _localToWorld = localToWorld;
            _points = new Point[X + 1, Y + 1, Z + 1];
            for (int i = 0; i <= X; i++)
            {
                for (int j = 0; j <= Y; j++)
                {
                    for (int k = 0; k <= Z; k++)
                        _points[i, j, k] = new Point(i, j, k);
                }
            }

            _cubes = new Cube[X, Y, Z];
            for (int i = 0; i < X; i++)
            {
                for (int j = 0; j < Y; j++)
                {
                    for (int k = 0; k < Z; k++)
                        _cubes[i, j, k] = new Cube(this, i, j, k);
                }
            }
        }
        
        public void SetPointStatus(int x, int y, int z, bool active)
        {
            x = Mathf.Clamp(x, 0, X);
            y = Mathf.Clamp(y, 0, Y);
            z = Mathf.Clamp(z, 0, Z);
            _points[x, y, z].iso = active ? 1 : 0;
            
            int minX = Mathf.Clamp(x -1, 0, X - 1);
            int maxX = Mathf.Clamp(x, 0, X - 1);
            int minY = Mathf.Clamp(y - 1, 0, Y - 1);
            int maxY = Mathf.Clamp(y, 0, Y - 1);
            int minZ = Mathf.Clamp(z - 1, 0, Z - 1);
            int maxZ = Mathf.Clamp(z, 0, Z - 1);
            
            for (int i = minX; i <= maxX; i++)
            {
                for (int j = minY; j <= maxY; j++)
                {
                    for (int k = minZ; k <= maxZ; k++)
                    {
                        ref var cube = ref _cubes[i, j, k];
                        int cubeIndex = 0;
                        for (int v = 0; v < CubeTable.VertexCount; v++)
                        {
                            ref readonly var point = ref cube[v];
                            if (point.iso > 0.5f)
                                cubeIndex |= 1 << v;
                        }

                        var oldMesh = cube.mesh;
                        if (oldMesh != null)
                        {
                            cube.mesh = null;
                            Object.DestroyImmediate(oldMesh);
                        }
                        
                        cube.mesh = MeshStore.GetMesh(cubeIndex);
                        if (null != cube.mesh)
                        {
                            Transform transform = cube.mesh.transform;
                            Vector3 position = localToWorld.MultiplyPoint(new Vector3(i, j, k));
                            transform.SetPositionAndRotation(position, Quaternion.identity);
                            transform.localScale = Vector3.one;
                        }
                    }
                }
            }
        }
    }
}
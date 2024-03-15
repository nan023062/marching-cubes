//****************************************************************************
// File: MarchingCubeBuilding.cs
// Author: Li Nan
// Date: 2023-09-02 12:00
// Version: 1.0
//****************************************************************************
using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class MarchingCubeBuilding : MonoBehaviour, IMeshStore
    {
        public int x, y, z;
        public uint unit = 1;
        [SerializeField] GameObject pointCubePrefab;
        [SerializeField] GameObject pointQuadPrefab;
        [SerializeField] private bool showPoint;
        [SerializeField] Transform mesh256;
        
        private BlockBuilding _building;
        private PointCube[,,] _pointCubes;
        private readonly Dictionary<int, GameObject> _meshes = new ();
        
        private void Awake()
        {
            Vector3 scale = Vector3.one / unit;
            Matrix4x4 matrix4X4 = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _building = new BlockBuilding(x, y, z, matrix4X4, this);
            transform.localScale = scale;
            _pointCubes = new PointCube[x + 1, y + 1, z + 1];
            for (int i = 1; i < x; i++)
            {
                for (int j = 1; j < z; j++)
                {
                    GameObject go = Instantiate(pointQuadPrefab);
                    Transform t = go.transform;
                    t.SetParent(this.transform);
                    t.SetLocalPositionAndRotation(new Vector3(i, 0.5f, j), Quaternion.identity);
                    t.localScale = new Vector3(1, 0, 1);
                    var quad = go.GetComponent<PointQuad>();
                    quad.marchingCubes = this;
                    quad.x = i;
                    quad.z = j;
                }
            }
            _meshes.Clear();
            FetchMeshesRecursive(mesh256);
        }
        
        private void FetchMeshesRecursive(Transform transform)
        {
            if (transform == null)
                return;
            
            foreach (Transform child in transform)
            {
                if (int.TryParse(child.name, out int cubeIndex))
                    _meshes.Add(cubeIndex, child.gameObject);
                
                FetchMeshesRecursive(child);
            }
        }


        private void OnDestroy()
        {
            _pointCubes = null;
        }

        public void OnClicked(PointElement element, bool left, in Vector3 normal)
        {
            if (left)
            {
                if (element is PointQuad quad)
                {
                    CreateCube(quad.x, 1, quad.z);
                }
                else if (element is PointCube cube)
                {
                    Vector3 localNormal = cube.transform.InverseTransformVector(normal).normalized;
                    Vector3 coord = new Vector3(cube.x, cube.y, cube.z) + localNormal;
                    int x0 = Mathf.RoundToInt(coord.x);
                    int y0 = Mathf.RoundToInt(coord.y);
                    int z0 = Mathf.RoundToInt(coord.z);
                    CreateCube(x0, y0, z0);
                }
            }
            else if (Input.GetMouseButtonUp(1))
            {
                if (element is PointCube cube)
                {
                    DestroyCube(cube);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (showPoint)
            {
                _building?.DrawPoints();
            }
        }

        private void CreateCube(int x, int y, int z)
        {
            if (x <= 0 || y <= 0 || z <= 0)
                return;
            if (x >= _building.X || y >= _building.Y || z >= _building.Z)
                return;

            ref var cube = ref _pointCubes[x, y, z];
            if (null == cube)
            {
                GameObject go = Instantiate(pointCubePrefab);
                Transform t = go.transform;
                t.SetParent(this.transform);
                t.SetLocalPositionAndRotation(new Vector3(x, y, z), Quaternion.identity);
                t.localScale = Vector3.one;

                cube = go.GetComponent<PointCube>();
                cube.marchingCubes = this;
                cube.x = x;
                cube.y = y;
                cube.z = z;
                _pointCubes[x, y, z] = cube;
                _building.SetPointStatus(x, y, z, true);
            }
        }

        private void DestroyCube(PointCube cube)
        {
            _building.SetPointStatus(x, y, z, false);
            DestroyImmediate(cube.gameObject);
            _pointCubes[cube.x, cube.y, cube.z] = null;
        }

        GameObject IMeshStore.GetMesh(int cubeIndex)
        {
            if (_meshes.TryGetValue(cubeIndex, out var mesh))
            {
                return GameObject.Instantiate(mesh);
            }
            
            return null;
        }
    }
}
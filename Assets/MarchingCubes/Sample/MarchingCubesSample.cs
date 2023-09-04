//****************************************************************************
// File: MarchingCubesSample.cs
// Author: Li Nan
// Date: 2023-09-02 12:00
// Version: 1.0
//****************************************************************************

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MarchingCubes.Sample
{
    public class MarchingCubesSample : MonoBehaviour
    {
        public int x, y, z;
        public uint unit = 1;
        [SerializeField] GameObject pointCubePrefab;
        [SerializeField] GameObject pointQuadPrefab;
        [SerializeField] private bool showPoint;
        
        private MarchingCubes _marchingCubes;
        private MeshFilter _meshFilter;
        private PointCube[,,] _pointCubes;

        private void Awake()
        {
            Vector3 scale = Vector3.one / unit;
            Matrix4x4 matrix4X4 = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _marchingCubes = new MarchingCubes(x, y, z, matrix4X4);
            _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.sharedMesh = _marchingCubes.mesh;
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
                _marchingCubes?.DrawPoints();
            }
        }
        
        private void CreateCube(int x, int y, int z)
        {
            if(x <= 0 || y <= 0 || z <= 0) 
                return;
            if(x >= _marchingCubes.X || y >= _marchingCubes.Y || z >= _marchingCubes.Z) 
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
                _marchingCubes.MarkPoint(x,y,z, true);
                _marchingCubes.RebuildMesh();
                _meshFilter.sharedMesh = _marchingCubes.mesh;
            }
        }
        
        private void DestroyCube(PointCube cube)
        {
            _marchingCubes.MarkPoint(cube.x, cube.y, cube.z, false);
            _marchingCubes.RebuildMesh();
            _meshFilter.sharedMesh = _marchingCubes.mesh;
            DestroyImmediate(cube.gameObject);
            _pointCubes[cube.x, cube.y, cube.z] = null;
        }
    }
}
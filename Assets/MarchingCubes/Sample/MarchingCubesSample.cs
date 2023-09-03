//****************************************************************************
// File: MarchingCubesSample.cs
// Author: Li Nan
// Date: 2023-09-02 12:00
// Version: 1.0
//****************************************************************************
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class MarchingCubesSample : MonoBehaviour
    {
        public int x, y, z;
        public uint unit = 1;
        [SerializeField] GameObject pointCubePrefab;
        [SerializeField] private bool showPoint;
        [SerializeField] private CubeBoard _board;

        private MarchingCubes _marchingCubes;
        private MeshFilter _meshFilter;
        private PointCube[,,] _pointCubes;

        private void Awake()
        {
            Vector3 scale = Vector3.one * (1f / unit);
            Matrix4x4 matrix4X4 = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _marchingCubes = new MarchingCubes(x, y, z, matrix4X4);
            _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.sharedMesh = _marchingCubes.mesh;
            transform.localScale = scale;
            _pointCubes = new PointCube[x + 1, y + 1, z + 1];
            var transform1 = _board.transform;
            transform1.localPosition = new Vector3(x, 0, z) * 0.5f;
            transform1.localScale = new Vector3(x, 0, z);
            _board.cubesSample = this;
        }
        
        private void OnDestroy()
        {
            _pointCubes = null;
        }

        private void Update()
        {
            if (Input.GetMouseButtonUp(0))
            {
                if (RaycastCube(out var cube, out var hit))
                {
                    Vector3 normal = cube.transform.InverseTransformVector(hit.normal).normalized;
                    Vector3 coord = new Vector3(cube.x, cube.y, cube.z) + normal;
                    int x0 = Mathf.RoundToInt(coord.x);
                    int y0 = Mathf.RoundToInt(coord.y);
                    int z0 = Mathf.RoundToInt(coord.z);
                    CreateCube(x0, y0, z0);
                }
            }
            else if (Input.GetMouseButtonUp(1))
            {
                if (RaycastCube(out var cube, out var hit))
                {
                    DestroyCube(cube);
                }
            }
        }
        
        private bool RaycastCube(out PointCube cube, out RaycastHit hit)
        {
            cube = null;
            Vector3 pos = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(pos);
            int layerMask = 1 << LayerMask.NameToLayer("MarchingCubes");
            if (Physics.Raycast(ray, out hit, 1000, layerMask))
            {
                cube = hit.transform.GetComponent<PointCube>();
            }

            return null != cube;
        }

        private void OnDrawGizmos()
        {
            if (showPoint)
            {
                _marchingCubes?.DrawPoints();
            }
        }

        public void CreateCube(int x, int y, int z)
        {
            if(x < 0 || y < 0 || z < 0) 
                return;
            if(x > _marchingCubes.X || y > _marchingCubes.Y || z > _marchingCubes.Z) 
                return;
            
            ref var cube = ref _pointCubes[x, y, z];
            if (null == cube)
            {
                GameObject go = Instantiate(pointCubePrefab);
                Transform t = go.transform;
                t.SetParent(this.transform);
                t.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.identity);
                t.localScale = Vector3.one;

                cube = go.GetComponent<PointCube>();
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
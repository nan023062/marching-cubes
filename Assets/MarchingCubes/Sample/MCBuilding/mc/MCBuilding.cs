using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class MCBuilding : MonoBehaviour, IMeshStore
    {
        // 尺寸由 BuildingManager.Init 注入，不在 Inspector 单独配置
        public uint unit = BuildingConst.Unit;

        [SerializeField] private CasePrefabConfig[] _configs;
        [SerializeField] private int _currentConfigIndex = 0;

        [SerializeField] private GameObject pointCubePrefab;
        [SerializeField] private GameObject pointQuadPrefab;
        [SerializeField] private bool showPoint;

        private int _x, _y, _z;
        private BlockBuilding      _building;
        private PointCube[,,]      _pointCubes;
        private List<GameObject>   _pointQuads = new List<GameObject>();

        // ── Config 访问 ───────────────────────────────────────────────────────

        public int ConfigCount        => _configs != null ? _configs.Length : 0;
        public int CurrentConfigIndex => _currentConfigIndex;

        private CasePrefabConfig CurrentConfig
        {
            get
            {
                if (_configs == null || _configs.Length == 0) return null;
                int idx = Mathf.Clamp(_currentConfigIndex, 0, _configs.Length - 1);
                return _configs[idx];
            }
        }

        public void SwitchConfig(int index)
        {
            if (_configs == null || index < 0 || index >= _configs.Length) return;
            if (index == _currentConfigIndex) return;
            _currentConfigIndex = index;
            _building?.RefreshAllMeshes();
        }

        public string GetConfigName(int index) =>
            (_configs != null && index >= 0 && index < _configs.Length && _configs[index] != null)
                ? _configs[index].name : "";

        // ── 由 BuildingManager 驱动初始化 ────────────────────────────────────
        // renderWidth / buildHeight / renderDepth：渲染格数（顶点数 = 渲染格数 + 1，内部自动处理）

        public void Init(int renderWidth, int buildHeight, int renderDepth)
        {
            _x = renderWidth;
            _y = buildHeight;
            _z = renderDepth;

            Vector3 scale    = Vector3.one / unit;
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _building = new BlockBuilding(_x, _y, _z, matrix, this);
            transform.localScale = scale;

            _pointCubes = new PointCube[_x + 1, _y + 1, _z + 1];

            for (int i = 1; i < _x; i++)
            {
                for (int j = 1; j < _z; j++)
                {
                    var go = Instantiate(pointQuadPrefab);
                    var t  = go.transform;
                    t.SetParent(transform);
                    t.localPosition = new Vector3(i, 0.5f, j);
                    t.localRotation = Quaternion.identity;
                    t.localScale    = new Vector3(1f, 0f, 1f);

                    var quad = go.GetComponent<PointQuad>();
                    quad.mcs = this;
                    quad.x   = i;
                    quad.z   = j;

                    _pointQuads.Add(go);
                }
            }
        }

        private void OnDestroy()
        {
            _pointCubes = null;
            _pointQuads.Clear();
        }

        private void OnDrawGizmos()
        {
            if (showPoint) _building?.DrawPoints();
        }

        // ── 地形同步 ──────────────────────────────────────────────────────────

        public void SyncWithTerrain(MarchingSquares.MSQTerrain terrain)
        {
            int idx = 0;
            for (int i = 1; i < _x; i++)
            {
                for (int j = 1; j < _z; j++)
                {
                    if (idx < _pointQuads.Count && _pointQuads[idx] != null)
                    {
                        float h = Mathf.Max(
                            terrain.GetPointHeight(i,     j),
                            terrain.GetPointHeight(i + 1, j),
                            terrain.GetPointHeight(i,     j + 1),
                            terrain.GetPointHeight(i + 1, j + 1));
                        var pos = _pointQuads[idx].transform.localPosition;
                        pos.y = h + 0.5f;
                        _pointQuads[idx].transform.localPosition = pos;
                    }
                    idx++;
                }
            }

            for (int ci = 0; ci <= _x; ci++)
            for (int cj = 0; cj <= _y; cj++)
            for (int ck = 0; ck <= _z; ck++)
            {
                var cube = _pointCubes[ci, cj, ck];
                if (cube == null) continue;

                bool conflict =
                    terrain.GetPointHeight(ci,     ck)     > cj ||
                    terrain.GetPointHeight(ci + 1, ck)     > cj ||
                    terrain.GetPointHeight(ci,     ck + 1) > cj ||
                    terrain.GetPointHeight(ci + 1, ck + 1) > cj;

                if (conflict) DestroyCube(cube);
            }
        }

        // ── 交互开关 ─────────────────────────────────────────────────────────

        public void EnableInteraction(bool active)
        {
            foreach (var go in _pointQuads)
                if (go != null) go.SetActive(active);

            foreach (var cube in _pointCubes)
                if (cube != null) cube.gameObject.SetActive(active);
        }

        // ── 点击处理 ─────────────────────────────────────────────────────────

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
                    Vector3 local = cube.transform.InverseTransformVector(normal).normalized;
                    Vector3 coord = new Vector3(cube.x, cube.y, cube.z) + local;
                    CreateCube(
                        Mathf.RoundToInt(coord.x),
                        Mathf.RoundToInt(coord.y),
                        Mathf.RoundToInt(coord.z));
                }
            }
            else
            {
                if (element is PointCube cube)
                    DestroyCube(cube);
            }
        }

        public void SetPoint(int px, int py, int pz, bool active)
        {
            _building?.SetPointStatus(px, py, pz, active);
        }

        private void CreateCube(int cx, int cy, int cz)
        {
            if (cx <= 0 || cy <= 0 || cz <= 0) return;
            if (cx >= _building.X || cy >= _building.Y || cz >= _building.Z) return;

            ref var cube = ref _pointCubes[cx, cy, cz];
            if (cube != null) return;

            var go = Instantiate(pointCubePrefab);
            var t  = go.transform;
            t.SetParent(transform);
            t.localPosition = new Vector3(cx, cy, cz);
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            cube     = go.GetComponent<PointCube>();
            cube.mcs = this;
            cube.x   = cx; cube.y = cy; cube.z = cz;
            _building.SetPointStatus(cx, cy, cz, true);
        }

        private void DestroyCube(PointCube cube)
        {
            _building.SetPointStatus(cube.x, cube.y, cube.z, false);
            DestroyImmediate(cube.gameObject);
            _pointCubes[cube.x, cube.y, cube.z] = null;
        }

        // ── IMeshStore ────────────────────────────────────────────────────────

        GameObject IMeshStore.GetMesh(int cubeIndex)
        {
            var config = CurrentConfig;
            if (config == null) return null;
            var prefab = config.GetPrefab(cubeIndex);
            if (prefab == null) return null;
            return Object.Instantiate(prefab);
        }
    }
}

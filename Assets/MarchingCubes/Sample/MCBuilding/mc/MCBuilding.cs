using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class MCBuilding : MonoBehaviour, IMeshStore
    {
        public int x, y, z;
        public uint unit = BuildingConst.Unit;

        [SerializeField] private CasePrefabConfig[] _configs;
        [SerializeField] private int _currentConfigIndex = 0;

        [SerializeField] private GameObject pointCubePrefab;
        [SerializeField] private GameObject pointQuadPrefab;
        [SerializeField] private bool showPoint;
        [SerializeField] private bool debugCube;

        private BlockBuilding _building;
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

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Vector3 scale  = Vector3.one / unit;
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _building = new BlockBuilding(x, y, z, matrix, this);
            transform.localScale = scale;

            _pointCubes = new PointCube[x + 1, y + 1, z + 1];

            // PointQuad：地面格点交互体（hover 描边 + 点击建造）
            for (int i = 1; i < x; i++)
            {
                for (int j = 1; j < z; j++)
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

        /// <summary>
        /// 地形高度变化后调用。
        /// 1. PointQuad Y 跟随地形表面高度。
        /// 2. 与地形产生穿插的 Cube 删除（Cube 任意格角点低于地形则视为冲突）。
        /// 前提：terrain 与 MCBuilding 使用相同 unit 且世界原点对齐。
        /// </summary>
        public void SyncWithTerrain(MarchingSquares.MSQTerrain terrain)
        {
            // 1. PointQuad 高度跟随地形
            int idx = 0;
            for (int i = 1; i < x; i++)
            {
                for (int j = 1; j < z; j++)
                {
                    if (idx < _pointQuads.Count && _pointQuads[idx] != null)
                    {
                        // 取格子四角最高点，确保 Quad 始终在地形表面之上
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

            // 2. 删除与地形穿插的 Cube
            for (int ci = 0; ci <= x; ci++)
            for (int cj = 0; cj <= y; cj++)
            for (int ck = 0; ck <= z; ck++)
            {
                var cube = _pointCubes[ci, cj, ck];
                if (cube == null) continue;

                // cube 占据 (ci~ci+1, cj~cj+1, ck~ck+1) 的格子空间
                // 只要底面四角任一格点的地形高度 >= cj（cube 底部），即视为穿插
                bool conflict =
                    terrain.GetPointHeight(ci,     ck)     > cj ||
                    terrain.GetPointHeight(ci + 1, ck)     > cj ||
                    terrain.GetPointHeight(ci,     ck + 1) > cj ||
                    terrain.GetPointHeight(ci + 1, ck + 1) > cj;

                if (conflict) DestroyCube(cube);
            }
        }

        // ── 交互开关（BuildState.OnEnter/Exit 调用）──────────────────────────

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

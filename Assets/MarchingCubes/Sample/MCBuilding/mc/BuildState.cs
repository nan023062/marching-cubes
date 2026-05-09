using System.Collections.Generic;
using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public class BuildState : IBuildState
    {
        readonly MCBuilding _building;

        BlockBuilding    _blockBuilding;
        PointCube[,,]    _pointCubes;
        List<GameObject> _pointQuads;

        public BuildState(MCBuilding building)
        {
            _building = building;
            InitBuilding();
        }

        // ── 初始化建造结构 ────────────────────────────────────────────────────

        void InitBuilding()
        {
            int x = _building.RenderWidth;
            int y = _building.BuildHeight;
            int z = _building.RenderDepth;

            var matrix = Matrix4x4.TRS(_building.transform.position,
                Quaternion.identity, Vector3.one / BuildingConst.Unit);
            _blockBuilding = new BlockBuilding(x, y, z, matrix, _building);

            _pointCubes = new PointCube[x + 1, y + 1, z + 1];
            _pointQuads = new List<GameObject>((x - 1) * (z - 1));

            for (int i = 1; i < x; i++)
            {
                for (int j = 1; j < z; j++)
                {
                    var go = Object.Instantiate(_building.pointQuadPrefab);
                    var t  = go.transform;
                    t.SetParent(_building.transform);
                    t.localPosition = new Vector3(i, 0.5f, j);
                    t.localRotation = Quaternion.identity;
                    t.localScale    = new Vector3(1f, 0f, 1f);

                    var quad = go.GetComponent<PointQuad>();
                    quad.mcs = _building;
                    quad.x   = i;
                    quad.z   = j;

                    _pointQuads.Add(go);
                }
            }
        }

        // ── State lifecycle ───────────────────────────────────────────────────

        public void OnEnter()
        {
            _building.SetBuildHandlers(HandleClick, RefreshAllMeshes);
            SetInteraction(true);
        }

        public void OnExit()
        {
            _building.SetBuildHandlers(null, null);
            SetInteraction(false);
        }

        // ── GUI（Config 选择）────────────────────────────────────────────────

        public void OnGUI()
        {
            int count = _building.ConfigCount;
            if (count <= 1) return;

            const float btnW = 140f, btnH = 28f, pad = 8f;
            float totalW = count * (btnW + pad) + pad;
            GUI.Box(new Rect(pad, pad, totalW, btnH + pad * 2), GUIContent.none);

            for (int i = 0; i < count; i++)
            {
                string label = _building.GetConfigName(i);
                Rect r = new Rect(pad + i * (btnW + pad), pad + pad * 0.5f, btnW, btnH);
                if (i == _building.CurrentConfigIndex)
                    GUI.Box(r, label);
                else if (GUI.Button(r, label))
                    _building.SwitchConfig(i);
            }
        }

        public void OnUpdate() { }

        // ── 点击处理 ─────────────────────────────────────────────────────────

        void HandleClick(PointElement element, bool left, Vector3 normal)
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

        // ── 地形同步 ──────────────────────────────────────────────────────────

        public void SyncWithTerrain(MarchingSquares.MQTerrainBuilder terrain)
        {
            int idx = 0;
            int x = _building.RenderWidth;
            int z = _building.RenderDepth;

            for (int i = 1; i < x; i++)
            {
                for (int j = 1; j < z; j++)
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

            int xMax = _building.RenderWidth;
            int yMax = _building.BuildHeight;
            int zMax = _building.RenderDepth;

            for (int ci = 0; ci <= xMax; ci++)
            for (int cj = 0; cj <= yMax; cj++)
            for (int ck = 0; ck <= zMax; ck++)
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

        // ── 内部建造逻辑 ─────────────────────────────────────────────────────

        void CreateCube(int cx, int cy, int cz)
        {
            if (cx <= 0 || cy <= 0 || cz <= 0) return;
            if (cx >= _blockBuilding.X || cy >= _blockBuilding.Y || cz >= _blockBuilding.Z) return;

            ref var cube = ref _pointCubes[cx, cy, cz];
            if (cube != null) return;

            var go = Object.Instantiate(_building.pointCubePrefab);
            var t  = go.transform;
            t.SetParent(_building.transform);
            t.localPosition = new Vector3(cx, cy, cz);
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            cube     = go.GetComponent<PointCube>();
            cube.mcs = _building;
            cube.x   = cx; cube.y = cy; cube.z = cz;
            _blockBuilding.SetPointStatus(cx, cy, cz, true);
        }

        void DestroyCube(PointCube cube)
        {
            _blockBuilding.SetPointStatus(cube.x, cube.y, cube.z, false);
            Object.DestroyImmediate(cube.gameObject);
            _pointCubes[cube.x, cube.y, cube.z] = null;
        }

        void RefreshAllMeshes() => _blockBuilding.RefreshAllMeshes();

        void SetInteraction(bool active)
        {
            foreach (var go in _pointQuads)
                if (go != null) go.SetActive(active);
            foreach (var cube in _pointCubes)
                if (cube != null) cube.gameObject.SetActive(active);
        }
    }
}

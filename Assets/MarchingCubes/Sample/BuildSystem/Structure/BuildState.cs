using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public class BuildState : IBuildState
    {
        readonly Structure _structure;

        StructureBuilder _blockBuilding;
        PointCube[,,]    _pointCubes;
        GameObject[,]    _pointQuads;   // [RenderWidth, RenderDepth]，cell 索引；非平地 cell 槽位 = null
        bool             _interactionActive;

        public BuildState(Structure structure)
        {
            _structure = structure;
            InitBuilding();
        }

        // ── 初始化建造结构 ────────────────────────────────────────────────────

        void InitBuilding()
        {
            int x = _structure.RenderWidth;
            int y = _structure.BuildHeight;
            int z = _structure.RenderDepth;

            var matrix = Matrix4x4.TRS(_structure.transform.position,
                Quaternion.identity, Vector3.one / BuildingConst.Unit);
            _blockBuilding = new StructureBuilder(x, y, z, matrix, _structure);

            _pointCubes = new PointCube[x + 1, y + 1, z + 1];
            _pointQuads = new GameObject[_structure.RenderWidth, _structure.RenderDepth];
        }

        // ── State lifecycle ───────────────────────────────────────────────────

        public void OnEnter()
        {
            _structure.SetBuildHandlers(HandleClick, RefreshAllMeshes);
            SetInteraction(true);
        }

        public void OnExit()
        {
            _structure.SetBuildHandlers(null, null);
            SetInteraction(false);
        }

        // ── GUI（Config 选择）────────────────────────────────────────────────

        public void OnGUI()
        {
            int count = _structure.ConfigCount;
            if (count <= 1) return;

            const float btnW = 140f, btnH = 28f, pad = 8f;
            float totalW = count * (btnW + pad) + pad;
            GUI.Box(new Rect(pad, pad, totalW, btnH + pad * 2), GUIContent.none);

            for (int i = 0; i < count; i++)
            {
                string label = _structure.GetConfigName(i);
                Rect r = new Rect(pad + i * (btnW + pad), pad + pad * 0.5f, btnW, btnH);
                if (i == _structure.CurrentConfigIndex)
                    GUI.Box(r, label);
                else if (GUI.Button(r, label))
                    _structure.SwitchConfig(i);
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
                    CreateCube(quad.cx, 1, quad.cz);
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

        public void SyncWithTerrain(MarchingSquares.TerrainBuilder terrain)
        {
            int xCells = _structure.RenderWidth;
            int zCells = _structure.RenderDepth;

            // 段一：扫所有 cell，按 IsCellFlat 增/删/移位 PointQuad
            for (int cx = 0; cx < xCells; cx++)
            for (int cz = 0; cz < zCells; cz++)
            {
                bool flat = terrain.IsCellFlat(cx, cz, out int baseH);
                var current = _pointQuads[cx, cz];

                if (flat && current == null)
                {
                    var go = Object.Instantiate(_structure.PointQuadPrefab);
                    var t  = go.transform;
                    t.SetParent(_structure.transform);
                    t.localPosition = new Vector3(cx + 0.5f, baseH, cz + 0.5f);
                    t.localRotation = Quaternion.identity;
                    t.localScale    = new Vector3(1f, 0f, 1f);
                    go.SetActive(_interactionActive);

                    var quad = go.GetComponent<PointQuad>();
                    quad.mcs = _structure;
                    quad.cx  = cx;
                    quad.cz  = cz;

                    _pointQuads[cx, cz] = go;
                }
                else if (!flat && current != null)
                {
                    Object.Destroy(current);
                    _pointQuads[cx, cz] = null;
                }
                else if (flat && current != null)
                {
                    var pos = current.transform.localPosition;
                    pos.y = baseH;
                    current.transform.localPosition = pos;
                }
            }

            // 段二：PointCube 冲突销毁（沿用旧逻辑）
            int xMax = _structure.RenderWidth;
            int yMax = _structure.BuildHeight;
            int zMax = _structure.RenderDepth;
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

            var go = Object.Instantiate(_structure.PointCubePrefab);
            var t  = go.transform;
            t.SetParent(_structure.transform);
            t.localPosition = new Vector3(cx, cy, cz);
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            cube     = go.GetComponent<PointCube>();
            cube.mcs = _structure;
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
            _interactionActive = active;
            foreach (var go in _pointQuads)
                if (go != null) go.SetActive(active);
            foreach (var cube in _pointCubes)
                if (cube != null) cube.gameObject.SetActive(active);
        }
    }
}

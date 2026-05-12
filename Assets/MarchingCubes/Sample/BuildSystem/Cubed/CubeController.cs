using System.Collections.Generic;
using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public class CubeController : BuildController
    {
        public uint unit = BuildingConst.Unit;

        [SerializeField] private CasePrefabConfig[] _configs;
        [SerializeField] private int _currentConfigIndex = 1;

        public CubeBuilder Builder { get; private set; }

        public int RenderWidth { get; private set; }
        public int BuildHeight { get; private set; }
        public int RenderDepth { get; private set; }

        CubeBuilder   _blockBuilding;
        GameObject[,,]     _cubeObjects;
        bool[,]            _cellActive;
        int[,]             _cellBaseH;
        TileBuilder     _terrain;

        // ── Config ───────────────────────────────────────────────────────────

        public int ConfigCount        => _configs != null ? _configs.Length : 0;
        public int CurrentConfigIndex => _currentConfigIndex;

        public string GetConfigName(int index) =>
            (_configs != null && index >= 0 && index < _configs.Length && _configs[index] != null)
                ? _configs[index].name : "";

        public void SwitchConfig(int index)
        {
            if (_configs == null || index < 0 || index >= _configs.Length) return;
            if (index == _currentConfigIndex) return;
            _currentConfigIndex = index;
            RefreshAllCubes();
        }

        // ── 初始化 ───────────────────────────────────────────────────────────

        public void Init(int renderWidth, int buildHeight, int renderDepth, CubeBuilder cubes, TileBuilder tiles)
        {
            RenderWidth = renderWidth;
            BuildHeight = buildHeight;
            RenderDepth = renderDepth;
            transform.localScale = Vector3.one / unit;

            _blockBuilding = cubes;
            Builder        = cubes;
            _terrain       = tiles;

            _cubeObjects = new GameObject[cubes.X, cubes.Y, cubes.Z];
            _cellActive  = new bool[RenderWidth, RenderDepth];
            _cellBaseH   = new int[RenderWidth, RenderDepth];

            InitGridMeshes("BuildGridVisual", "BuildGridCollider");
        }

        // ── IBuildState ───────────────────────────────────────────────────────

        public override void OnEnter()
        {
            SetActive(true);
            SetInteraction(true);
            if (_terrain != null) SyncWithTerrain();
        }

        public override void OnExit()
        {
            SetActive(false);
            SetInteraction(false);
        }

        public override void OnUpdate() { }

        static readonly string[] ConfigNames = { "等值面", "圆角Cube" };

        public override void DrawGUI()
        {
            const float pad = 8f, btnH = 28f;
            GUI.Label(new Rect(pad, Screen.height - 132f, 320f, 20f),
                "左键 放置方块  右键 移除方块");

            if (GUI.Button(new Rect(pad, Screen.height - 168f, 120f, btnH),
                    Previewing ? "[ 隐藏预览 ]" : "  预览Cases  "))
                TogglePreview(GetCurrentConfigPrefabs());

            int count = ConfigCount;
            if (count <= 1) return;
            const float btnW = 140f, gap = 4f;
            float y = Screen.height - 204f;
            for (int i = 0; i < count; i++)
            {
                string label = i < ConfigNames.Length ? ConfigNames[i] : GetConfigName(i);
                Rect r = new Rect(pad, y - i * (btnH + gap), btnW, btnH);
                if (i == CurrentConfigIndex) GUI.Box(r, label);
                else if (GUI.Button(r, label)) SwitchConfig(i);
            }
        }

        GameObject[] GetCurrentConfigPrefabs()
        {
            if (_configs == null || _configs.Length == 0) return new GameObject[0];
            int idx = Mathf.Clamp(_currentConfigIndex, 0, _configs.Length - 1);
            var config = _configs[idx];
            var result = new GameObject[256];
            for (int i = 0; i < 256; i++)
                result[i] = config?.GetPrefab(i);
            return result;
        }

        // ── Cube 实例管理（Controller 负责 GO 生命周期）──────────────────────

        void RefreshCubeAt(int i, int j, int k)
        {
            if (_cubeObjects[i, j, k] != null)
            {
                Object.DestroyImmediate(_cubeObjects[i, j, k]);
                _cubeObjects[i, j, k] = null;
            }
            int cubeIndex = _blockBuilding.GetCubeIndex(i, j, k);
            _cubeObjects[i, j, k] = GetMesh(cubeIndex);
            if (_cubeObjects[i, j, k] != null)
            {
                Transform t   = _cubeObjects[i, j, k].transform;
                Vector3   pos = _blockBuilding.localToWorld.MultiplyPoint(new Vector3(i, j, k));
                t.SetPositionAndRotation(pos, Quaternion.identity);
                t.localScale = Vector3.one;
            }
        }

        void RefreshCubesAround(int x, int y, int z)
        {
            int minI = Mathf.Clamp(x - 1, 0, _blockBuilding.X - 1);
            int maxI = Mathf.Clamp(x,     0, _blockBuilding.X - 1);
            int minJ = Mathf.Clamp(y - 1, 0, _blockBuilding.Y - 1);
            int maxJ = Mathf.Clamp(y,     0, _blockBuilding.Y - 1);
            int minK = Mathf.Clamp(z - 1, 0, _blockBuilding.Z - 1);
            int maxK = Mathf.Clamp(z,     0, _blockBuilding.Z - 1);

            for (int i = minI; i <= maxI; i++)
            for (int j = minJ; j <= maxJ; j++)
            for (int k = minK; k <= maxK; k++)
                RefreshCubeAt(i, j, k);
        }

        void RefreshAllCubes()
        {
            for (int i = 0; i < _blockBuilding.X; i++)
            for (int j = 0; j < _blockBuilding.Y; j++)
            for (int k = 0; k < _blockBuilding.Z; k++)
                RefreshCubeAt(i, j, k);
        }

        // ── 输入响应 ─────────────────────────────────────────────────────────────

        protected override void OnPointerMove(RaycastHit hit, Ray ray, bool onMesh)
        {
            if (_cursor == null) return;
            _cursor.gameObject.SetActive(onMesh);
            if (!onMesh) return;

            CalcSrcAdj(hit, out var src, out _);
            float cellSize = 1f / BuildingConst.Unit;
            _cursor.transform.position   = _blockBuilding.localToWorld.MultiplyPoint((Vector3)src);
            _cursor.transform.localScale = Vector3.one * cellSize;
        }

        protected override void OnPointerClick(RaycastHit hit, bool left) => FireBuildClick(hit, left);

        void FireBuildClick(RaycastHit hit, bool left)
        {
            CalcSrcAdj(hit, out var src, out var adj);
            HandleBuildClick(src, adj, left);
        }

        static void CalcSrcAdj(RaycastHit hit, out Vector3Int src, out Vector3Int adj)
        {
            Vector3 n  = hit.normal;
            Vector3 ip = hit.point - n * 0.5f;

            int srcX = Mathf.Abs(n.x) > 0.5f ? Mathf.RoundToInt(ip.x + 0.5f) : Mathf.FloorToInt(ip.x) + 1;
            int srcY = Mathf.Abs(n.y) > 0.5f ? Mathf.RoundToInt(ip.y + 0.5f) : Mathf.FloorToInt(ip.y) + 1;
            int srcZ = Mathf.Abs(n.z) > 0.5f ? Mathf.RoundToInt(ip.z + 0.5f) : Mathf.FloorToInt(ip.z) + 1;

            src = new Vector3Int(srcX, srcY, srcZ);
            adj = src + new Vector3Int(
                Mathf.RoundToInt(n.x),
                Mathf.RoundToInt(n.y),
                Mathf.RoundToInt(n.z));
        }

        void HandleBuildClick(Vector3Int src, Vector3Int adj, bool left)
        {
            if (left) CreateCube(adj.x, adj.y, adj.z);
            else      DestroyCube(src.x, src.y, src.z);
        }

        // ── 地形同步 ──────────────────────────────────────────────────────────

        public void SyncWithTerrain()
        {
            int W = RenderWidth;
            int D = RenderDepth;

            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
            {
                bool flat = _terrain.IsCellFlat(cx, cz, out int baseH);
                _cellActive[cx, cz] = flat;
                _cellBaseH[cx, cz]  = baseH;
                _blockBuilding.SetQuadActive(cx, cz, flat, baseH);
            }

            for (int ci = 1; ci <= RenderWidth; ci++)
            for (int cj = 1; cj <= BuildHeight; cj++)
            for (int ck = 1; ck <= RenderDepth; ck++)
            {
                if (!_blockBuilding.IsPointActive(ci, cj, ck)) continue;
                bool conflict =
                    _terrain.GetPointHeight(ci - 1, ck - 1) >= cj ||
                    _terrain.GetPointHeight(ci,     ck - 1) >= cj ||
                    _terrain.GetPointHeight(ci - 1, ck    ) >= cj ||
                    _terrain.GetPointHeight(ci,     ck    ) >= cj;
                if (conflict) DestroyCube(ci, cj, ck);
            }

            RebuildGridMesh();
        }

        // ── BuildGrid mesh ────────────────────────────────────────────────────

        void RebuildGridMesh()
        {
            int W = RenderWidth;
            int D = RenderDepth;

            // 单一 quad 几何源（每 4 顶点 = 一个 quad，每 6 indices = 2 triangle）：
            // 1) 平地 cell（baseMcY 没 cube 占据的）的顶面 quad
            // 2) StructureBuilder.AppendExposedFaces 输出的所有 cube 暴露面 quad
            // visual mesh 和 collider mesh 共享同一套顶点；visual 走 line topology
            // 每 quad 画 4 条边（a-b, b-c, c-d, d-a）。
            var verts = new List<Vector3>();
            var tris  = new List<int>();

            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
            {
                if (!_cellActive[cx, cz]) continue;
                int baseMcY = _cellBaseH[cx, cz] + 1;
                if (_blockBuilding.IsPointActive(cx + 1, baseMcY, cz + 1)) continue;

                float y = _cellBaseH[cx, cz];
                int v0 = verts.Count;
                verts.Add(new Vector3(cx,   y, cz));   verts.Add(new Vector3(cx+1, y, cz));
                verts.Add(new Vector3(cx+1, y, cz+1)); verts.Add(new Vector3(cx,   y, cz+1));
                tris.Add(v0); tris.Add(v0+2); tris.Add(v0+1);
                tris.Add(v0); tris.Add(v0+3); tris.Add(v0+2);
            }

            _blockBuilding.AppendExposedFaces(verts, tris);

            // visual lines：每 quad（4 顶点）→ 4 条边
            int quadCount   = verts.Count / 4;
            var lineIndices = new int[quadCount * 8];
            for (int q = 0; q < quadCount; q++)
            {
                int b = q * 4;
                int li = q * 8;
                lineIndices[li]   = b;     lineIndices[li+1] = b + 1;
                lineIndices[li+2] = b + 1; lineIndices[li+3] = b + 2;
                lineIndices[li+4] = b + 2; lineIndices[li+5] = b + 3;
                lineIndices[li+6] = b + 3; lineIndices[li+7] = b;
            }

            var vArr = verts.ToArray();
            _visualMesh.Clear();
            _visualMesh.vertices = vArr;
            _visualMesh.SetIndices(lineIndices, MeshTopology.Lines, 0);
            _visualMesh.RecalculateBounds();

            _colliderMesh.Clear();
            _colliderMesh.vertices  = vArr;
            _colliderMesh.triangles = tris.ToArray();
            _colliderMesh.RecalculateBounds();
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _colliderMesh;
        }

        // ── 内部建造逻辑 ─────────────────────────────────────────────────────

        void CreateCube(int cx, int cy, int cz)
        {
            if (cx <= 0 || cy <= 0 || cz <= 0) return;
            if (cx >= _blockBuilding.X || cy >= _blockBuilding.Y || cz >= _blockBuilding.Z) return;
            if (_blockBuilding.IsPointActive(cx, cy, cz)) return;

            _blockBuilding.SetPointStatus(cx, cy, cz, true);
            RefreshCubesAround(cx, cy, cz);
            RebuildGridMesh();
        }

        void DestroyCube(int cx, int cy, int cz)
        {
            if (!_blockBuilding.IsPointActive(cx, cy, cz)) return;
            _blockBuilding.SetPointStatus(cx, cy, cz, false);
            RefreshCubesAround(cx, cy, cz);
            RebuildGridMesh();
        }

        // ── Case mesh ────────────────────────────────────────────────────────

        public GameObject GetMesh(int cubeIndex)
        {
            if (_configs == null || _configs.Length == 0) return null;
            int idx    = Mathf.Clamp(_currentConfigIndex, 0, _configs.Length - 1);
            var prefab = _configs[idx]?.GetPrefab(cubeIndex);
            return prefab != null ? Object.Instantiate(prefab) : null;
        }
    }
}

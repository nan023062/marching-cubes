using System.Collections.Generic;
using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public class McController : BuildController
    {
        public uint unit = BuildingConst.Unit;

        [SerializeField] private CasePrefabConfig[] _configs;
        [SerializeField] private int _currentConfigIndex = 0;

        public StructureBuilder Builder { get; private set; }

        public int RenderWidth { get; private set; }
        public int BuildHeight { get; private set; }
        public int RenderDepth { get; private set; }

        // ── 事件 ─────────────────────────────────────────────────────────────
        public event System.Action<Vector3Int, Vector3Int, bool> OnBuildGridClicked;
        public event System.Action                               OnConfigChanged;

        StructureBuilder   _blockBuilding;
        GameObject[,,]     _cubeObjects;
        bool[,]            _cellActive;
        int[,]             _cellBaseH;
        TerrainBuilder     _terrain;

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
            OnConfigChanged?.Invoke();
        }

        // ── 初始化 ───────────────────────────────────────────────────────────

        public void Init(int renderWidth, int buildHeight, int renderDepth)
        {
            RenderWidth = renderWidth;
            BuildHeight = buildHeight;
            RenderDepth = renderDepth;
            transform.localScale = Vector3.one / unit;

            int x      = RenderWidth + 1;
            int y      = BuildHeight;
            int z      = RenderDepth + 1;
            var pos    = transform.position - new Vector3(0.5f, 0.5f, 0.5f);
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one / BuildingConst.Unit);

            _blockBuilding = new StructureBuilder(x, y, z, matrix);
            Builder        = _blockBuilding;

            _cubeObjects = new GameObject[x, y, z];
            _cellActive  = new bool[RenderWidth, RenderDepth];
            _cellBaseH   = new int[RenderWidth, RenderDepth];

            InitGridMeshes("BuildGridVisual", "BuildGridCollider");
        }

        public void SetTerrain(TerrainBuilder t) => _terrain = t;

        // ── IBuildState ───────────────────────────────────────────────────────

        public override void OnEnter()
        {
            OnBuildGridClicked += HandleBuildClick;
            OnConfigChanged    += RefreshAllCubes;
            if (_terrain != null) SyncWithTerrain(_terrain);
            SetInteraction(true);
        }

        public override void OnExit()
        {
            OnBuildGridClicked -= HandleBuildClick;
            OnConfigChanged    -= RefreshAllCubes;
            SetInteraction(false);
        }

        public override void OnUpdate() { }

        public override void OnGUI()
        {
            int count = ConfigCount;
            if (count <= 1) return;
            const float btnW = 140f, btnH = 28f, pad = 8f;
            float totalW = count * (btnW + pad) + pad;
            GUI.Box(new Rect(pad, pad, totalW, btnH + pad * 2), GUIContent.none);
            for (int i = 0; i < count; i++)
            {
                string label = GetConfigName(i);
                Rect r = new Rect(pad + i * (btnW + pad), pad + pad * 0.5f, btnW, btnH);
                if (i == CurrentConfigIndex) GUI.Box(r, label);
                else if (GUI.Button(r, label)) SwitchConfig(i);
            }
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

        // ── Raycast ───────────────────────────────────────────────────────────

        void Update()
        {
            for (int btn = 0; btn <= 1; btn++)
                if (Input.GetMouseButtonDown(btn)) _pressButton = btn;

            if (OnBuildGridClicked == null) return;
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 1000f)) return;
            if (hit.transform != transform) return;

            for (int btn = 0; btn <= 1; btn++)
            {
                if (Input.GetMouseButtonUp(btn) && _pressButton == btn)
                {
                    _pressButton = -1;
                    FireBuildClick(hit, btn == 0);
                    break;
                }
            }
        }

        void FireBuildClick(RaycastHit hit, bool left)
        {
            Vector3 n  = hit.normal;
            Vector3 ip = hit.point - n * 0.5f;

            int srcX = Mathf.Abs(n.x) > 0.5f ? Mathf.RoundToInt(ip.x + 0.5f) : Mathf.FloorToInt(ip.x) + 1;
            int srcY = Mathf.Abs(n.y) > 0.5f ? Mathf.RoundToInt(ip.y + 0.5f) : Mathf.FloorToInt(ip.y) + 1;
            int srcZ = Mathf.Abs(n.z) > 0.5f ? Mathf.RoundToInt(ip.z + 0.5f) : Mathf.FloorToInt(ip.z) + 1;

            var src = new Vector3Int(srcX, srcY, srcZ);
            var adj = src + new Vector3Int(
                Mathf.RoundToInt(n.x),
                Mathf.RoundToInt(n.y),
                Mathf.RoundToInt(n.z));

            OnBuildGridClicked?.Invoke(src, adj, left);
        }

        void HandleBuildClick(Vector3Int src, Vector3Int adj, bool left)
        {
            if (left) CreateCube(adj.x, adj.y, adj.z);
            else      DestroyCube(src.x, src.y, src.z);
        }

        // ── 地形同步 ──────────────────────────────────────────────────────────

        public void SyncWithTerrain(TerrainBuilder terrain)
        {
            int W = RenderWidth;
            int D = RenderDepth;

            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
            {
                bool flat = terrain.IsCellFlat(cx, cz, out int baseH);
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
                    terrain.GetPointHeight(ci - 1, ck - 1) >= cj ||
                    terrain.GetPointHeight(ci,     ck - 1) >= cj ||
                    terrain.GetPointHeight(ci - 1, ck    ) >= cj ||
                    terrain.GetPointHeight(ci,     ck    ) >= cj;
                if (conflict) DestroyCube(ci, cj, ck);
            }

            RebuildGridMesh();
        }

        // ── BuildGrid mesh ────────────────────────────────────────────────────

        void RebuildGridMesh()
        {
            int W = RenderWidth;
            int D = RenderDepth;

            int flatCount = 0;
            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
                if (_cellActive[cx, cz]) flatCount++;

            var vV = new Vector3[flatCount * 8];
            var iV = new int[flatCount * 8];
            int vi = 0;
            const float yOff = 0.02f;

            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
            {
                if (!_cellActive[cx, cz]) continue;
                float y = _cellBaseH[cx, cz] + yOff;
                Vector3 p00 = new Vector3(cx,   y, cz),   p10 = new Vector3(cx+1, y, cz),
                        p11 = new Vector3(cx+1, y, cz+1), p01 = new Vector3(cx,   y, cz+1);
                vV[vi]=p00; iV[vi]=vi; vi++; vV[vi]=p10; iV[vi]=vi; vi++;
                vV[vi]=p10; iV[vi]=vi; vi++; vV[vi]=p11; iV[vi]=vi; vi++;
                vV[vi]=p11; iV[vi]=vi; vi++; vV[vi]=p01; iV[vi]=vi; vi++;
                vV[vi]=p01; iV[vi]=vi; vi++; vV[vi]=p00; iV[vi]=vi; vi++;
            }

            _visualMesh.Clear();
            _visualMesh.vertices = vV;
            _visualMesh.SetIndices(iV, MeshTopology.Lines, 0);
            _visualMesh.RecalculateBounds();

            var cVerts = new List<Vector3>();
            var cTris  = new List<int>();

            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
            {
                if (!_cellActive[cx, cz]) continue;
                int baseMcY = _cellBaseH[cx, cz] + 1;
                if (_blockBuilding.IsPointActive(cx + 1, baseMcY, cz + 1)) continue;

                float y = _cellBaseH[cx, cz] + yOff;
                int v0 = cVerts.Count;
                cVerts.Add(new Vector3(cx,   y, cz));   cVerts.Add(new Vector3(cx+1, y, cz));
                cVerts.Add(new Vector3(cx+1, y, cz+1)); cVerts.Add(new Vector3(cx,   y, cz+1));
                cTris.Add(v0); cTris.Add(v0+1); cTris.Add(v0+2);
                cTris.Add(v0); cTris.Add(v0+2); cTris.Add(v0+3);
            }

            _blockBuilding.AppendExposedFaces(cVerts, cTris);

            _colliderMesh.Clear();
            _colliderMesh.vertices  = cVerts.ToArray();
            _colliderMesh.triangles = cTris.ToArray();
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

        // ── Gizmos ───────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (Application.isPlaying &&
                BuildingManager.Instance?.CurrentMode != BuildMode.Build) return;
            if (_blockBuilding == null) return;

            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = _blockBuilding.localToWorld;
            Gizmos.color  = new Color(1f, 1f, 1f, 0.35f);

            for (int x = 0; x <= _blockBuilding.X; x++)
            for (int y = 0; y <= _blockBuilding.Y; y++)
            for (int z = 0; z <= _blockBuilding.Z; z++)
            {
                if (!_blockBuilding.IsPointActive(x, y, z)) continue;
                if (!_blockBuilding.IsPointActive(x+1, y,   z  )) DrawGizmoFace(new Vector3(x+0.5f, y,      z      ), Vector3.forward, Vector3.up);
                if (!_blockBuilding.IsPointActive(x-1, y,   z  )) DrawGizmoFace(new Vector3(x-0.5f, y,      z      ), Vector3.forward, Vector3.up);
                if (!_blockBuilding.IsPointActive(x,   y+1, z  )) DrawGizmoFace(new Vector3(x,      y+0.5f, z      ), Vector3.right,   Vector3.forward);
                if (!_blockBuilding.IsPointActive(x,   y-1, z  )) DrawGizmoFace(new Vector3(x,      y-0.5f, z      ), Vector3.right,   Vector3.forward);
                if (!_blockBuilding.IsPointActive(x,   y,   z+1)) DrawGizmoFace(new Vector3(x,      y,      z+0.5f), Vector3.right,   Vector3.up);
                if (!_blockBuilding.IsPointActive(x,   y,   z-1)) DrawGizmoFace(new Vector3(x,      y,      z-0.5f), Vector3.right,   Vector3.up);
            }

            for (int cx = 0; cx < _blockBuilding.X; cx++)
            for (int cz = 0; cz < _blockBuilding.Z; cz++)
            {
                if (!_blockBuilding.IsQuadActive(cx, cz)) continue;
                int qh = _blockBuilding.GetQuadBaseH(cx, cz);
                if (_blockBuilding.IsPointActive(cx + 1, qh + 1, cz + 1)) continue;
                DrawGizmoFace(new Vector3(cx + 1f, qh + 0.5f, cz + 1f), Vector3.right, Vector3.forward);
            }

            Gizmos.matrix = prevMatrix;
        }

        static void DrawGizmoFace(Vector3 center, Vector3 right, Vector3 up)
        {
            var a = center - right * 0.5f - up * 0.5f;
            var b = center + right * 0.5f - up * 0.5f;
            var c = center + right * 0.5f + up * 0.5f;
            var d = center - right * 0.5f + up * 0.5f;
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
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

using System.Collections.Generic;
using UnityEngine;
using MarchingCubes.Sample;
using MarchingSquares;

namespace MarchingTerrain
{
    public class TerrainController : BuildController
    {
        [SerializeField] private TerrainCaseConfig _meshConfig;
        [SerializeField, Header("涂刷类型 (0~7，bit 位越高视觉权重越大)"), Range(0, 7)]
        private int  _textureLayer = 1;
        [SerializeField] private bool _colorBrush;

        public int            TextureLayer { get => _textureLayer; set => _textureLayer = value; }
        public  TerrainBuilder Builder      { get; private set; }

        MarchingCubes.Sample.Cursor PlaneBrush => _cursor as MarchingCubes.Sample.Cursor;

        private System.Action _onTerrainChanged;
        private GameObject[,] _tiles;

        static readonly string[] LayerNames = { "泥", "草", "岩", "雪", "紫" };

        // ── 初始化 ───────────────────────────────────────────────────────────

        public void Init(int renderWidth, int renderDepth, int heightRange, TerrainBuilder tiles)
        {
            Builder = tiles;

            float unit = 1f / BuildingConst.Unit;
            transform.localScale = Vector3.one * unit;

            _tiles = new GameObject[renderDepth, renderWidth];
            InitGridMeshes("TerrainGrid", "TerrainCollider");
            InitColliderMesh();

            RefreshAllTiles();
            RebuildGridMesh();
        }

        public void SetSyncCallback(System.Action cb) => _onTerrainChanged = cb;

        // ── IBuildState ───────────────────────────────────────────────────────

        public override void OnEnter() { SetActive(true);  SetInteraction(true);  }
        public override void OnExit()  { SetActive(false); SetInteraction(false); }

        public override void OnUpdate() { }

        public override void DrawGUI()
        {
            const float btnW = 80f, btnH = 28f, pad = 8f, gap = 4f;
            float y = Screen.height - 204f;

            GUI.Label(new Rect(pad, Screen.height - 132f, 480f, 20f),
                _colorBrush
                    ? "左键 叠加当前笔刷  右键 擦除当前笔刷  清空 笔刷内全归零"
                    : "左键 升高地形  右键 降低地形");

            if (GUI.Button(new Rect(pad, Screen.height - 168f, btnW + 20f, btnH),
                    Previewing ? "[ 隐藏预览 ]" : "  预览Cases  "))
                TogglePreview(GetAllTilePrefabs());

            if (GUI.Button(new Rect(pad, y, btnW, btnH),
                    !_colorBrush ? "[ 高度 ]" : "  高度  "))
                _colorBrush = false;

            y -= btnH + gap;
            if (GUI.Button(new Rect(pad, y, btnW, btnH),
                    _colorBrush ? "[ 涂色 ]" : "  涂色  "))
                _colorBrush = true;

            if (_colorBrush)
            {
                y -= btnH + gap;
                float typeBtnW = btnW * 0.55f;
                for (int i = 0; i < LayerNames.Length; i++)
                {
                    bool sel = (_textureLayer == i);
                    if (GUI.Button(new Rect(pad + i * (typeBtnW + gap), y, typeBtnW, btnH),
                            sel ? $"[{LayerNames[i]}]" : LayerNames[i]))
                        _textureLayer = i;
                }
                float clearBtnX = pad + LayerNames.Length * (typeBtnW + gap) + gap;
                if (GUI.Button(new Rect(clearBtnX, y, btnW, btnH), "清空"))
                    if (ClearTerrainMask()) _onTerrainChanged?.Invoke();
            }
        }

        // ── 输入响应 ─────────────────────────────────────────────────────────────

        protected override void OnPointerMove(RaycastHit hit, Ray ray, bool onMesh)
        {
            float unit = 1f / BuildingConst.Unit;
            Vector3 pos;
            if (onMesh)
            {
                pos = hit.point;
            }
            else
            {
                var   bt       = _cursor != null ? _cursor.transform.position : Vector3.zero;
                float northDis = Vector3.Project(bt - ray.origin, Vector3.up).magnitude;
                float cos      = Vector3.Dot(Vector3.down, ray.direction);
                pos = Mathf.Abs(cos) > 0.001f ? ray.origin + ray.direction * (northDis / cos) : bt;
            }
            int px = Mathf.RoundToInt(pos.x / unit);
            int pz = Mathf.RoundToInt(pos.z / unit);
            HandlePointMove(px, pz);
        }

        protected override void OnPointerClick(RaycastHit hit, bool left)
        {
            float unit = 1f / BuildingConst.Unit;
            int px = Mathf.RoundToInt(hit.point.x / unit);
            int pz = Mathf.RoundToInt(hit.point.z / unit);
            HandlePointClicked(px, pz, left);
        }

        void HandlePointMove(int px, int pz)
        {
            if (_cursor == null) return;
            float unit = 1f / BuildingConst.Unit;
            _cursor.transform.position = new Vector3(px * unit, Builder.GetPointHeight(px, pz) * unit + 0.01f, pz * unit);
            _cursor.Style = CursorStyle.QuadY;
            float s = unit * _cursor.Size;
            _cursor.transform.localScale = new Vector3(s, 0f, s);
        }

        void HandlePointClicked(int px, int pz, bool left)
        {
            bool dirty;
            if (_colorBrush)
                dirty = left ? PaintTerrainType(_textureLayer) : EraseTerrainType(_textureLayer);
            else
                dirty = BrushMapHigh(left ? 1 : -1);
            if (dirty) _onTerrainChanged?.Invoke();
        }

        // ── 地形操作（含 tile 刷新）──────────────────────────────────────────

        public bool BrushMapHigh(int delta)
        {
            bool dirty = Builder.BrushMapHigh(PlaneBrush, delta, out var cpts);
            if (dirty)
            {
                RebuildColliderMesh();
                RebuildGridMesh();
                RefreshAffectedTiles(cpts);
            }
            return dirty;
        }

        public bool PaintTerrainType(int type)
        {
            bool dirty = Builder.PaintTerrainType(PlaneBrush, type, out var dpts);
            if (dirty) RefreshAffectedTilesMPB(dpts);
            return dirty;
        }

        public bool EraseTerrainType(int type)
        {
            bool dirty = Builder.EraseTerrainType(PlaneBrush, type, out var dpts);
            if (dirty) RefreshAffectedTilesMPB(dpts);
            return dirty;
        }

        public bool ClearTerrainMask()
        {
            bool dirty = Builder.ClearTerrainMask(PlaneBrush, out var dpts);
            if (dirty) RefreshAffectedTilesMPB(dpts);
            return dirty;
        }

        public bool IsCellFlat(int cx, int cz, out int h) => Builder.IsCellFlat(cx, cz, out h);

        // ── Tile 实例管理（Controller 负责 GO 生命周期）──────────────────────

        void RefreshAllTiles()
        {
            for (int x = 0; x < Builder.length; x++)
            for (int z = 0; z < Builder.width; z++)
                RefreshTile(x, z);
        }

        void RefreshAffectedTiles(IEnumerable<(int px, int pz)> changedPoints)
        {
            foreach (var (px, pz) in changedPoints)
            for (int dx = -1; dx <= 0; dx++)
            for (int dz = -1; dz <= 0; dz++)
            {
                int cx = px + dx, cz = pz + dz;
                if (cx >= 0 && cx < Builder.length && cz >= 0 && cz < Builder.width)
                    RefreshTile(cx, cz);
            }
        }

        void RefreshAffectedTilesMPB(IEnumerable<(int px, int pz)> dirtyPoints)
        {
            var dirtyCells = new HashSet<(int, int)>();
            foreach (var (px, pz) in dirtyPoints)
            for (int dx = -1; dx <= 0; dx++)
            for (int dz = -1; dz <= 0; dz++)
            {
                int cx = px + dx, cz = pz + dz;
                if (cx >= 0 && cx < Builder.length && cz >= 0 && cz < Builder.width)
                    dirtyCells.Add((cx, cz));
            }
            foreach (var (cx, cz) in dirtyCells)
                if (_tiles[cx, cz] != null) ApplyTileMPB(_tiles[cx, cz], cx, cz);
        }

        void RefreshTile(int x, int z)
        {
            if (_meshConfig == null)
            {
                Debug.LogError("[TerrainController] _meshConfig 未赋值，请在 Inspector 中指定 TerrainCaseConfig 资产。", this);
                return;
            }
            if (_tiles[x, z] != null)
            {
                Object.Destroy(_tiles[x, z]);
                _tiles[x, z] = null;
            }
            int caseIndex = Builder.GetCaseIndex(x, z, out int baseH);
            var prefab = _meshConfig.GetPrefab(caseIndex);
            if (prefab == null) prefab = _meshConfig.GetPrefab(0);
            if (prefab == null) return;

            var tile = Object.Instantiate(prefab);
            Transform t = tile.transform;
            t.SetParent(transform);
            t.localPosition = new Vector3(x, baseH, z);
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            ApplyTileMPB(tile, x, z);

            var dbg = tile.GetComponent<TilePrefab>();
            if (dbg != null) { dbg.caseIndex = caseIndex; dbg.baseHeight = baseH; }

            _tiles[x, z] = tile;
        }

        void ApplyTileMPB(GameObject tile, int cx, int cz)
        {
            var mpb = new MaterialPropertyBlock();
            byte mBL = Builder.GetTerrainMask(cx,     cz    );
            byte mBR = Builder.GetTerrainMask(cx + 1, cz    );
            byte mTR = Builder.GetTerrainMask(cx + 1, cz + 1);
            byte mTL = Builder.GetTerrainMask(cx,     cz + 1);
            int idx0 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 0);
            int idx1 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 1);
            int idx2 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 2);
            int idx3 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 3);
            int idx4 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 4);
            mpb.SetVector("_TileMsIdx",  new Vector4(idx0, idx1, idx2, idx3));
            mpb.SetFloat ("_TileMsIdx4", idx4);
            foreach (var mr in tile.GetComponentsInChildren<MeshRenderer>())
                mr.SetPropertyBlock(mpb);
        }

        // ── 碰撞 Mesh ─────────────────────────────────────────────────────────

        void InitColliderMesh()
        {
            int totalVertex = Builder.length * Builder.width * 6;
            var triangles = new int[totalVertex];
            for (int i = 0; i < totalVertex; i++) triangles[i] = i;
            RebuildColliderMesh();                    // 先填顶点
            _colliderMesh.triangles = triangles;     // 再设三角形索引
            _meshCollider.sharedMesh = _colliderMesh; // 三角形到位后强制 re-bake
        }

        void RebuildColliderMesh()
        {
            int L = Builder.length, W = Builder.width;
            var verts = new Vector3[L * W * 6];
            for (int z = 0; z < W; z++)
            for (int x = 0; x < L; x++)
            {
                int idx = (x + L * z) * 6;
                verts[idx + 0] = new Vector3(x,   Builder.GetPointHeight(x,   z),   z);
                verts[idx + 1] = new Vector3(x,   Builder.GetPointHeight(x,   z+1), z+1);
                verts[idx + 2] = new Vector3(x+1, Builder.GetPointHeight(x+1, z),   z);
                verts[idx + 3] = new Vector3(x+1, Builder.GetPointHeight(x+1, z),   z);
                verts[idx + 4] = new Vector3(x,   Builder.GetPointHeight(x,   z+1), z+1);
                verts[idx + 5] = new Vector3(x+1, Builder.GetPointHeight(x+1, z+1), z+1);
            }
            _colliderMesh.vertices = verts;
            _colliderMesh.RecalculateNormals();
            _meshCollider.sharedMesh = _colliderMesh;
        }

        // ── 视觉 Grid mesh ────────────────────────────────────────────────────

        void RebuildGridMesh()
        {
            int W = Builder.width, L = Builder.length;
            int cols = W + 1, rows = L + 1;

            var verts = new Vector3[cols * rows];
            for (int x = 0; x < cols; x++)
            for (int z = 0; z < rows; z++)
                verts[x * rows + z] = new Vector3(x, Builder.GetPointHeight(x, z), z);

            int segCount = cols * L + rows * W;
            var idx = new int[segCount * 2];
            int i = 0;

            for (int x = 0; x < cols; x++)
            for (int z = 0; z < L; z++)
            { idx[i++] = x * rows + z; idx[i++] = x * rows + z + 1; }

            for (int z = 0; z < rows; z++)
            for (int x = 0; x < W; x++)
            { idx[i++] = x * rows + z; idx[i++] = (x + 1) * rows + z; }

            _visualMesh.Clear();
            _visualMesh.vertices = verts;
            _visualMesh.SetIndices(idx, MeshTopology.Lines, 0);
            _visualMesh.RecalculateBounds();
        }
        
        GameObject[] GetAllTilePrefabs()
        {
            var result = new GameObject[TerrainCaseConfig.TerrainCaseCount];
            for (int i = 0; i < TerrainCaseConfig.TerrainCaseCount; i++)
                result[i] = _meshConfig?.GetPrefab(i);
            return result;
        }

        protected override void OnDestroy() { base.OnDestroy(); Builder = null; }
    }
}

using System.Collections.Generic;
using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingSquares
{
    public class TerrainController : BuildController
    {
        [SerializeField] private Brush          _brush;
        [SerializeField] private TileCaseConfig _meshConfig;
        [SerializeField, Header("涂刷类型 (0~7，bit 位越高视觉权重越大)"), Range(0, 7)]
        private int _textureLayer = 1;

        public Brush          Brush        => _brush;
        public int            TextureLayer { get => _textureLayer; set => _textureLayer = value; }
        public TerrainBuilder Builder      { get; private set; }

        private System.Action _onTerrainChanged;
        private GameObject[,] _tiles;
        private bool          _active;

        static readonly string[] LayerNames = { "泥", "草", "岩", "雪", "紫" };

        // ── 初始化 ───────────────────────────────────────────────────────────

        public void Init(int renderWidth, int renderDepth, int heightRange)
        {
            float unit = 1f / BuildingConst.Unit;
            Builder = new TerrainBuilder(renderWidth, renderDepth, heightRange, unit, transform.position);
            Builder.MaxHeightDiff = BuildingConst.TerrainMaxHeightDiff * BuildingConst.Unit;

            transform.localScale = Vector3.one * unit;

            _tiles = new GameObject[renderDepth, renderWidth];
            InitGridMeshes("TerrainGrid", "TerrainCollider");
            InitColliderMesh();

            RefreshAllTiles();
            RebuildGridMesh();
        }

        public void SetSyncCallback(System.Action cb) => _onTerrainChanged = cb;

        // ── IBuildState ───────────────────────────────────────────────────────

        public override void OnEnter() { SetBrushVisible(true);  _active = true;  }
        public override void OnExit()  { SetBrushVisible(false); _active = false; }

        public override void OnUpdate() { }

        public override void DrawGUI()
        {
            const float btnW = 80f, btnH = 28f, pad = 8f, gap = 4f;
            float y = Screen.height - btnH * 3 - pad * 3 - 22f;

            if (GUI.Button(new Rect(pad, y, btnW, btnH),
                    !_brush.colorBrush ? "[ 高度 ]" : "  高度  "))
                _brush.colorBrush = false;

            if (GUI.Button(new Rect(pad + btnW + gap, y, btnW, btnH),
                    _brush.colorBrush ? "[ 涂色 ]" : "  涂色  "))
                _brush.colorBrush = true;

            if (_brush.colorBrush)
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

                y -= btnH + gap;
                GUI.Label(new Rect(pad, y, 480f, btnH),
                    "左键: 叠加当前 type   |   右键: 擦除当前 type   |   清空: 笔刷内所有 type 归零");
            }
        }

        // ── Raycast ───────────────────────────────────────────────────────────

        void Update()
        {
            if (!_active) return;
            if (Camera.main == null) return;

            float unit      = 1f / BuildingConst.Unit;
            Ray   ray       = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool  onTerrain = _meshCollider.Raycast(ray, out var hit, 1000f);

            Vector3 pos;
            if (onTerrain)
            {
                pos = hit.point;
            }
            else
            {
                var   bt       = _brush != null ? _brush.transform.position : Vector3.zero;
                float northDis = Vector3.Project(bt - ray.origin, Vector3.up).magnitude;
                float cos      = Vector3.Dot(Vector3.down, ray.direction);
                pos = Mathf.Abs(cos) > 0.001f ? ray.origin + ray.direction * (northDis / cos) : bt;
            }

            int px = Mathf.RoundToInt(pos.x / unit);
            int pz = Mathf.RoundToInt(pos.z / unit);
            HandlePointMove(px, pz);

            if (!onTerrain) { _pressButton = -1; return; }

            for (int btn = 0; btn <= 1; btn++)
                if (Input.GetMouseButtonDown(btn)) _pressButton = btn;

            for (int btn = 0; btn <= 1; btn++)
            {
                if (Input.GetMouseButtonUp(btn) && _pressButton == btn)
                {
                    _pressButton = -1;
                    HandlePointClicked(px, pz, btn == 0);
                    break;
                }
            }
        }

        void HandlePointMove(int px, int pz)
        {
            if (_brush == null) return;
            float unit = 1f / BuildingConst.Unit;
            var   lw   = Builder.localToWorld;
            var   t    = _brush.transform;
            t.position   = new Vector3(px * unit, Builder.GetPointHeight(px, pz) * unit + 0.01f, pz * unit);
            t.localScale = lw.lossyScale;
            t.rotation   = lw.rotation;
        }

        void HandlePointClicked(int px, int pz, bool left)
        {
            bool dirty;
            if (_brush.colorBrush)
                dirty = left ? PaintTerrainType(_textureLayer) : EraseTerrainType(_textureLayer);
            else
                dirty = BrushMapHigh(left ? 1 : -1);
            if (dirty) _onTerrainChanged?.Invoke();
        }

        // ── 地形操作（含 tile 刷新）──────────────────────────────────────────

        public bool BrushMapHigh(int delta)
        {
            bool dirty = Builder.BrushMapHigh(_brush, delta, out var cpts);
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
            bool dirty = Builder.PaintTerrainType(_brush, type, out var dpts);
            if (dirty) RefreshAffectedTilesMPB(dpts);
            return dirty;
        }

        public bool EraseTerrainType(int type)
        {
            bool dirty = Builder.EraseTerrainType(_brush, type, out var dpts);
            if (dirty) RefreshAffectedTilesMPB(dpts);
            return dirty;
        }

        public bool ClearTerrainMask()
        {
            bool dirty = Builder.ClearTerrainMask(_brush, out var dpts);
            if (dirty) RefreshAffectedTilesMPB(dpts);
            return dirty;
        }

        public bool IsCellFlat(int cx, int cz, out int h) => Builder.IsCellFlat(cx, cz, out h);
        public void SetBrushVisible(bool visible)          { if (_brush != null) _brush.gameObject.SetActive(visible); }

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

        // ── Gizmos ───────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (Application.isPlaying &&
                BuildingManager.Instance?.CurrentMode != BuildMode.Terrain) return;
            if (Builder == null) return;

            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Builder.localToWorld;

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            for (int x = 0; x <= Builder.length; x++)
            for (int z = 0; z <= Builder.width; z++)
            {
                int h = Builder.GetPointHeight(x, z);
                var p = new Vector3(x, h, z);
                if (x < Builder.length)
                    Gizmos.DrawLine(p, new Vector3(x + 1, Builder.GetPointHeight(x + 1, z), z));
                if (z < Builder.width)
                    Gizmos.DrawLine(p, new Vector3(x, Builder.GetPointHeight(x, z + 1), z + 1));
            }

            for (int x = 0; x <= Builder.length; x++)
            for (int z = 0; z <= Builder.width; z++)
            {
                byte mask = Builder.GetTerrainMask(x, z);
                if (mask == 0) continue;
                Gizmos.color = MaskGizmoColor(mask);
                Gizmos.DrawSphere(new Vector3(x, Builder.GetPointHeight(x, z), z), 0.03f);
            }

            Gizmos.matrix = prevMatrix;

#if UNITY_EDITOR
            const int ChunkSize = 4;
            var prevHandlesMatrix = UnityEditor.Handles.matrix;
            UnityEditor.Handles.matrix = Builder.localToWorld;
            UnityEditor.Handles.color  = new Color(1f, 0.85f, 0f, 0.9f);

            for (int z = 0; z <= Builder.width; z += ChunkSize)
            {
                var line = new Vector3[Builder.length + 1];
                for (int x = 0; x <= Builder.length; x++)
                    line[x] = new Vector3(x, Builder.GetPointHeight(x, z), z);
                UnityEditor.Handles.DrawAAPolyLine(3f, line);
            }
            for (int x = 0; x <= Builder.length; x += ChunkSize)
            {
                var line = new Vector3[Builder.width + 1];
                for (int z = 0; z <= Builder.width; z++)
                    line[z] = new Vector3(x, Builder.GetPointHeight(x, z), z);
                UnityEditor.Handles.DrawAAPolyLine(3f, line);
            }

            UnityEditor.Handles.matrix = prevHandlesMatrix;
#endif
        }

        static Color MaskGizmoColor(byte mask)
        {
            if ((mask & 0x10) != 0) return new Color(0.36f, 0.15f, 0.41f);
            if ((mask & 0x08) != 0) return new Color(0.88f, 0.91f, 0.96f);
            if ((mask & 0x04) != 0) return new Color(0.50f, 0.50f, 0.48f);
            if ((mask & 0x02) != 0) return new Color(0.18f, 0.62f, 0.17f);
            if ((mask & 0x01) != 0) return new Color(0.60f, 0.47f, 0.20f);
            return Color.gray;
        }

        private void OnDestroy() { Builder = null; }
    }
}

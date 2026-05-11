using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public class BuildState : IBuildState
    {
        readonly Structure      _structure;
        readonly TerrainBuilder _terrain;

        StructureBuilder _blockBuilding;
        PointCube[,,]    _pointCubes;

        // BuildGrid mesh（直接挂在 Structure 上）
        Mesh         _gridVisualMesh;
        Mesh         _gridColliderMesh;
        MeshCollider _gridCollider;

        // cell 状态
        bool[,] _cellActive;
        int[,]  _cellBaseH;

        public BuildState(Structure structure, TerrainBuilder terrain)
        {
            _structure = structure;
            _terrain   = terrain;
            InitBuilding();
        }

        // ── 初始化 ────────────────────────────────────────────────────────────

        void InitBuilding()
        {
            int x = _structure.RenderWidth  + 1;
            int y = _structure.BuildHeight;
            int z = _structure.RenderDepth  + 1;

            var pos    = _structure.transform.position - new Vector3(0.5f, 0.5f, 0.5f);
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one / BuildingConst.Unit);
            _blockBuilding = new StructureBuilder(x, y, z, matrix, _structure);
            _structure.Builder = _blockBuilding;

            _pointCubes = new PointCube[x + 1, y + 1, z + 1];

            int W = _structure.RenderWidth;
            int D = _structure.RenderDepth;
            _cellActive = new bool[W, D];
            _cellBaseH  = new int[W, D];

            // Structure 自身 MeshFilter/MeshCollider 作为 BuildGrid 载体
            _gridVisualMesh   = new Mesh { name = "BuildGridVisual" };
            _gridColliderMesh = new Mesh { name = "BuildGridCollider" };
            _structure.GetComponent<MeshFilter>().sharedMesh = _gridVisualMesh;
            _gridCollider = _structure.GetComponent<MeshCollider>();
            _gridCollider.sharedMesh = _gridColliderMesh;
        }

        // ── State lifecycle ───────────────────────────────────────────────────

        public void OnEnter()
        {
            _structure.SetBuildHandlers(HandleClick, HandleGridClick, RefreshAllMeshes);
            SyncWithTerrain(_terrain);
            SetInteraction(true);
        }

        public void OnExit()
        {
            _structure.SetBuildHandlers(null, null, null);
            SetInteraction(false);
        }

        // ── GUI ───────────────────────────────────────────────────────────────

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
                if (i == _structure.CurrentConfigIndex) GUI.Box(r, label);
                else if (GUI.Button(r, label))          _structure.SwitchConfig(i);
            }
        }

        public void OnUpdate() { }

        // ── 点击处理 ─────────────────────────────────────────────────────────

        void HandleClick(PointElement element, bool left, Vector3 normal)
        {
            if (!(element is PointCube cube)) return;
            if (left)
            {
                Vector3 local = cube.transform.InverseTransformVector(normal).normalized;
                Vector3 coord = new Vector3(cube.x, cube.y, cube.z) + local;
                CreateCube(Mathf.RoundToInt(coord.x), Mathf.RoundToInt(coord.y), Mathf.RoundToInt(coord.z));
            }
            else
            {
                DestroyCube(cube);
            }
        }

        // BuildGrid（可建造面）点击
        public void HandleGridClick(Vector3 worldPos, bool left)
        {
            int cx = Mathf.FloorToInt(worldPos.x);
            int cz = Mathf.FloorToInt(worldPos.z);
            int W  = _structure.RenderWidth;
            int D  = _structure.RenderDepth;
            if (cx < 0 || cx >= W || cz < 0 || cz >= D) return;
            if (!_cellActive[cx, cz]) return;
            if (left) CreateCube(cx + 1, _cellBaseH[cx, cz] + 1, cz + 1);
        }

        // ── 地形同步 ──────────────────────────────────────────────────────────

        public void SyncWithTerrain(TerrainBuilder terrain)
        {
            int W = _structure.RenderWidth;
            int D = _structure.RenderDepth;

            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
            {
                bool flat = terrain.IsCellFlat(cx, cz, out int baseH);
                _cellActive[cx, cz] = flat;
                _cellBaseH[cx, cz]  = baseH;
                _blockBuilding.SetQuadActive(cx, cz, flat, baseH);
            }
            RebuildGridMesh();

            // PointCube 冲突销毁
            int xMax = W, yMax = _structure.BuildHeight, zMax = D;
            for (int ci = 1; ci <= xMax; ci++)
            for (int cj = 1; cj <= yMax; cj++)
            for (int ck = 1; ck <= zMax; ck++)
            {
                var cube = _pointCubes[ci, cj, ck];
                if (cube == null) continue;
                bool conflict =
                    terrain.GetPointHeight(ci - 1, ck - 1) >= cj ||
                    terrain.GetPointHeight(ci,     ck - 1) >= cj ||
                    terrain.GetPointHeight(ci - 1, ck    ) >= cj ||
                    terrain.GetPointHeight(ci,     ck    ) >= cj;
                if (conflict) DestroyCube(cube);
            }
        }

        // ── BuildGrid mesh 生成 ───────────────────────────────────────────────

        void RebuildGridMesh()
        {
            int W = _structure.RenderWidth;
            int D = _structure.RenderDepth;
            int count = 0;
            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
                if (_cellActive[cx, cz]) count++;

            // 视觉：Lines，每 cell 4 条边
            var vVerts   = new Vector3[count * 8];
            var vIndices = new int[count * 8];
            // 碰撞：Triangles，每 cell 2 个三角
            var cVerts = new Vector3[count * 4];
            var cTris  = new int[count * 6];
            int vi = 0, ci2 = 0, ti = 0;
            const float yOff = 0.02f;

            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
            {
                if (!_cellActive[cx, cz]) continue;
                float   y   = _cellBaseH[cx, cz] + yOff;
                Vector3 p00 = new Vector3(cx,     y, cz    );
                Vector3 p10 = new Vector3(cx + 1, y, cz    );
                Vector3 p11 = new Vector3(cx + 1, y, cz + 1);
                Vector3 p01 = new Vector3(cx,     y, cz + 1);

                // Lines
                vVerts[vi] = p00; vIndices[vi] = vi; vi++;
                vVerts[vi] = p10; vIndices[vi] = vi; vi++;
                vVerts[vi] = p10; vIndices[vi] = vi; vi++;
                vVerts[vi] = p11; vIndices[vi] = vi; vi++;
                vVerts[vi] = p11; vIndices[vi] = vi; vi++;
                vVerts[vi] = p01; vIndices[vi] = vi; vi++;
                vVerts[vi] = p01; vIndices[vi] = vi; vi++;
                vVerts[vi] = p00; vIndices[vi] = vi; vi++;

                // Triangles
                int v0 = ci2;
                cVerts[ci2++] = p00; cVerts[ci2++] = p10;
                cVerts[ci2++] = p11; cVerts[ci2++] = p01;
                cTris[ti++] = v0; cTris[ti++] = v0 + 2; cTris[ti++] = v0 + 1;
                cTris[ti++] = v0; cTris[ti++] = v0 + 3; cTris[ti++] = v0 + 2;
            }

            _gridVisualMesh.Clear();
            _gridVisualMesh.vertices = vVerts;
            _gridVisualMesh.SetIndices(vIndices, MeshTopology.Lines, 0);
            _gridVisualMesh.RecalculateBounds();

            _gridColliderMesh.Clear();
            _gridColliderMesh.vertices  = cVerts;
            _gridColliderMesh.triangles = cTris;
            _gridColliderMesh.RecalculateBounds();
            _gridCollider.sharedMesh = null;
            _gridCollider.sharedMesh = _gridColliderMesh;
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
            t.localPosition = new Vector3(cx - 0.5f, cy - 0.5f, cz - 0.5f);
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
            _structure.GetComponent<MeshRenderer>().enabled = active;
            foreach (var cube in _pointCubes)
                if (cube != null) cube.gameObject.SetActive(active);
        }
    }
}

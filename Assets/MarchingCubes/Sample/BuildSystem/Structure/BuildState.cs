using System.Collections.Generic;
using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public class BuildState : IBuildState
    {
        readonly Structure      _structure;
        readonly TerrainBuilder _terrain;

        StructureBuilder _blockBuilding;

        Mesh         _gridVisualMesh;
        Mesh         _gridColliderMesh;
        MeshCollider _gridCollider;

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

            int W = _structure.RenderWidth;
            int D = _structure.RenderDepth;
            _cellActive = new bool[W, D];
            _cellBaseH  = new int[W, D];

            _gridVisualMesh   = new Mesh { name = "BuildGridVisual" };
            _gridColliderMesh = new Mesh { name = "BuildGridCollider" };
            _structure.GetComponent<MeshFilter>().sharedMesh = _gridVisualMesh;
            _gridCollider = _structure.GetComponent<MeshCollider>();
            _gridCollider.sharedMesh = _gridColliderMesh;
        }

        // ── State lifecycle ───────────────────────────────────────────────────

        public void OnEnter()
        {
            _structure.OnBuildGridClicked += HandleBuildClick;
            _structure.OnConfigChanged    += _blockBuilding.RefreshAllMeshes;
            SyncWithTerrain(_terrain);
            SetInteraction(true);
        }

        public void OnExit()
        {
            _structure.OnBuildGridClicked -= HandleBuildClick;
            _structure.OnConfigChanged    -= _blockBuilding.RefreshAllMeshes;
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

        // ── 点击处理（统一公式：src = 被点面的 MC 格点，adj = 建造目标）────────

        void HandleBuildClick(Vector3Int src, Vector3Int adj, bool left)
        {
            if (left)
                CreateCube(adj.x, adj.y, adj.z);
            else
                DestroyCube(src.x, src.y, src.z);
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

            // 地形升高时销毁被压住的 cube
            int xMax = _structure.RenderWidth;
            int yMax = _structure.BuildHeight;
            int zMax = _structure.RenderDepth;
            for (int ci = 1; ci <= xMax; ci++)
            for (int cj = 1; cj <= yMax; cj++)
            for (int ck = 1; ck <= zMax; ck++)
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

        // ── BuildGrid mesh 生成 ───────────────────────────────────────────────

        void RebuildGridMesh()
        {
            int W = _structure.RenderWidth;
            int D = _structure.RenderDepth;

            // ── 视觉 Lines：所有平地 cell 轮廓 ──────────────────────────────
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
                Vector3 p00 = new Vector3(cx, y, cz), p10 = new Vector3(cx+1, y, cz),
                        p11 = new Vector3(cx+1, y, cz+1), p01 = new Vector3(cx, y, cz+1);
                vV[vi]=p00; iV[vi]=vi; vi++; vV[vi]=p10; iV[vi]=vi; vi++;
                vV[vi]=p10; iV[vi]=vi; vi++; vV[vi]=p11; iV[vi]=vi; vi++;
                vV[vi]=p11; iV[vi]=vi; vi++; vV[vi]=p01; iV[vi]=vi; vi++;
                vV[vi]=p01; iV[vi]=vi; vi++; vV[vi]=p00; iV[vi]=vi; vi++;
            }

            _gridVisualMesh.Clear();
            _gridVisualMesh.vertices = vV;
            _gridVisualMesh.SetIndices(iV, MeshTopology.Lines, 0);
            _gridVisualMesh.RecalculateBounds();

            // ── 碰撞 Triangles：BuildGrid 平面（无 cube）+ cube 暴露面 ───────
            var cVerts = new List<Vector3>();
            var cTris  = new List<int>();

            // 平地 cell（该位置还没有 cube 才加平面）
            for (int cx = 0; cx < W; cx++)
            for (int cz = 0; cz < D; cz++)
            {
                if (!_cellActive[cx, cz]) continue;
                int baseMcY = _cellBaseH[cx, cz] + 1;
                if (_blockBuilding.IsPointActive(cx + 1, baseMcY, cz + 1)) continue; // cube 已存在

                float y = _cellBaseH[cx, cz] + yOff;
                int v0 = cVerts.Count;
                cVerts.Add(new Vector3(cx, y, cz)); cVerts.Add(new Vector3(cx+1, y, cz));
                cVerts.Add(new Vector3(cx+1, y, cz+1)); cVerts.Add(new Vector3(cx, y, cz+1));
                cTris.Add(v0); cTris.Add(v0+1); cTris.Add(v0+2);
                cTris.Add(v0); cTris.Add(v0+2); cTris.Add(v0+3);
            }

            // cube 暴露面
            _blockBuilding.AppendExposedFaces(cVerts, cTris);

            _gridColliderMesh.Clear();
            _gridColliderMesh.vertices  = cVerts.ToArray();
            _gridColliderMesh.triangles = cTris.ToArray();
            _gridColliderMesh.RecalculateBounds();
            _gridCollider.sharedMesh = null;
            _gridCollider.sharedMesh = _gridColliderMesh;
        }

        // ── 内部建造逻辑 ─────────────────────────────────────────────────────

        void CreateCube(int cx, int cy, int cz)
        {
            if (cx <= 0 || cy <= 0 || cz <= 0) return;
            if (cx >= _blockBuilding.X || cy >= _blockBuilding.Y || cz >= _blockBuilding.Z) return;
            if (_blockBuilding.IsPointActive(cx, cy, cz)) return;

            _blockBuilding.SetPointStatus(cx, cy, cz, true);
            RebuildGridMesh();
        }

        void DestroyCube(int cx, int cy, int cz)
        {
            if (!_blockBuilding.IsPointActive(cx, cy, cz)) return;
            _blockBuilding.SetPointStatus(cx, cy, cz, false);
            RebuildGridMesh();
        }

        void SetInteraction(bool active)
            => _structure.GetComponent<MeshRenderer>().enabled = active;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes.Sample
{
    /// <summary>
    /// Marching Edges 建造 Controller：
    /// 继承 BuildController，在 3D 格点网格上管理面元素（墙/地板/围栏等）的放置与移除。
    /// </summary>
    public class FaceController : BuildController
    {
        [SerializeField] MeCaseConfig _config;

        public FaceBuilder Builder { get; private set; }

        GameObject[,,] _vertexObjects;

        // ── 初始化 ───────────────────────────────────────────────────────────

        public void Init(int nx, int ny, int nz, FaceBuilder faces)
        {
            float unit = 1f / BuildingConst.Unit;
            transform.localScale = Vector3.one * unit;

            Builder = faces;
            _vertexObjects = new GameObject[nx, ny, nz];

            InitGridMeshes("MEGridVisual", "MEGridCollider");
            RebuildColliderMesh();
        }

        // ── IBuildState ───────────────────────────────────────────────────────

        public override void OnEnter() { SetActive(true);  SetInteraction(true);  }
        public override void OnExit()  { SetActive(false); SetInteraction(false); }
        public override void OnUpdate() { }
        public override void DrawGUI()  { }

        // ── 输入响应 ─────────────────────────────────────────────────────────

        protected override void OnPointerMove(RaycastHit hit, Ray ray, bool onMesh)
        {
            if (_cursor == null) return;
            _cursor.gameObject.SetActive(onMesh);
            if (!onMesh) return;

            var localHit = transform.InverseTransformPoint(hit.point);
            var localN   = transform.InverseTransformDirection(hit.normal);

            if (!TryGetFaceCenter(localHit, localN, out var localCenter, out int faceAxis))
            {
                _cursor.gameObject.SetActive(false);
                return;
            }

            _cursor.transform.position = transform.TransformPoint(localCenter);
            _cursor.Style = faceAxis switch
            {
                0 => CursorStyle.QuadX,
                1 => CursorStyle.QuadY,
                _ => CursorStyle.QuadZ,
            };
        }

        protected override void OnPointerClick(RaycastHit hit, bool left)
        {
            var localHit = transform.InverseTransformPoint(hit.point);
            var localN   = transform.InverseTransformDirection(hit.normal);

            if (!TryGetFaceIndices(localHit, localN, out int axis, out int a, out int b, out int c))
                return;

            // left = 放置（true），right = 移除（false）
            List<Vector3Int> affected = axis switch
            {
                0 => Builder.SetXFace(a, b, c, left),
                1 => Builder.SetYFace(a, b, c, left),
                _ => Builder.SetZFace(a, b, c, left),
            };

            if (affected != null)
                foreach (var v in affected)
                    RefreshVertex(v.x, v.y, v.z);

            RebuildColliderMesh();
        }

        // ── Vertex prefab 管理 ───────────────────────────────────────────────

        void RefreshVertex(int vx, int vy, int vz)
        {
            if (_vertexObjects[vx, vy, vz] != null)
            {
                Object.Destroy(_vertexObjects[vx, vy, vz]);
                _vertexObjects[vx, vy, vz] = null;
            }

            var (canonIdx, rot) = Builder.GetCanonical(vx, vy, vz);
            if (canonIdx == 0) return;

            var prefab = _config?.GetPrefab(canonIdx);
            if (prefab == null) return;

            float unit = 1f / BuildingConst.Unit;
            var go = Object.Instantiate(prefab);
            go.transform.SetPositionAndRotation(
                transform.TransformPoint(new Vector3(vx, vy, vz)), rot);
            go.transform.localScale = Vector3.one * unit;
            _vertexObjects[vx, vy, vz] = go;
        }

        // ── 射线 hit 解析 ────────────────────────────────────────────────────

        bool TryGetFaceCenter(Vector3 localHit, Vector3 localNormal, out Vector3 center, out int axis)
        {
            if (!TryGetFaceIndices(localHit, localNormal, out axis, out int a, out int b, out int c))
            {
                center = default;
                return false;
            }
            center = axis switch
            {
                0 => new Vector3(a,       b + 0.5f, c + 0.5f), // xFace 中心
                1 => new Vector3(a + 0.5f, b,       c + 0.5f), // yFace 中心
                _ => new Vector3(a + 0.5f, b + 0.5f, c      ), // zFace 中心
            };
            return true;
        }

        bool TryGetFaceIndices(Vector3 localHit, Vector3 localNormal,
                               out int axis, out int a, out int b, out int c)
        {
            float ax = Mathf.Abs(localNormal.x);
            float ay = Mathf.Abs(localNormal.y);
            float az = Mathf.Abs(localNormal.z);

            if (ax >= ay && ax >= az) // YZ 平面面（xFaces）
            {
                axis = 0;
                a    = Mathf.RoundToInt(localHit.x);
                b    = Mathf.FloorToInt(localHit.y);
                c    = Mathf.FloorToInt(localHit.z);
                return Builder.InBoundsX(a, b, c);
            }
            if (ay >= ax && ay >= az) // XZ 平面面（yFaces）
            {
                axis = 1;
                a    = Mathf.FloorToInt(localHit.x);
                b    = Mathf.RoundToInt(localHit.y);
                c    = Mathf.FloorToInt(localHit.z);
                return Builder.InBoundsY(a, b, c);
            }
            // XY 平面面（zFaces）
            axis = 2;
            a    = Mathf.FloorToInt(localHit.x);
            b    = Mathf.FloorToInt(localHit.y);
            c    = Mathf.RoundToInt(localHit.z);
            return Builder.InBoundsZ(a, b, c);
        }

        // ── Collider mesh（全部面槽位置，供点击） ────────────────────────────

        void RebuildColliderMesh()
        {
            var verts = new List<Vector3>();
            var tris  = new List<int>();

            // xFaces：YZ 平面，法线方向 X
            for (int vx = 0; vx < Builder.Nx;     vx++)
            for (int j  = 0; j  < Builder.Ny - 1; j++)
            for (int k  = 0; k  < Builder.Nz - 1; k++)
                AddFaceQuad(verts, tris,
                    new Vector3(vx, j + 0.5f, k + 0.5f),
                    Vector3.up, Vector3.forward, 0.48f);

            // yFaces：XZ 平面，法线方向 Y
            for (int i  = 0; i  < Builder.Nx - 1; i++)
            for (int vy = 0; vy < Builder.Ny;     vy++)
            for (int k  = 0; k  < Builder.Nz - 1; k++)
                AddFaceQuad(verts, tris,
                    new Vector3(i + 0.5f, vy, k + 0.5f),
                    Vector3.right, Vector3.forward, 0.48f);

            // zFaces：XY 平面，法线方向 Z
            for (int i  = 0; i  < Builder.Nx - 1; i++)
            for (int j  = 0; j  < Builder.Ny - 1; j++)
            for (int vz = 0; vz < Builder.Nz;     vz++)
                AddFaceQuad(verts, tris,
                    new Vector3(i + 0.5f, j + 0.5f, vz),
                    Vector3.right, Vector3.up, 0.48f);

            _colliderMesh.Clear();
            _colliderMesh.vertices  = verts.ToArray();
            _colliderMesh.triangles = tris.ToArray();
            _colliderMesh.RecalculateBounds();
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _colliderMesh;
        }

        static void AddFaceQuad(List<Vector3> verts, List<int> tris,
                                Vector3 center, Vector3 tangent, Vector3 bitangent, float s)
        {
            int v0 = verts.Count;
            verts.Add(center - tangent * s - bitangent * s);
            verts.Add(center + tangent * s - bitangent * s);
            verts.Add(center + tangent * s + bitangent * s);
            verts.Add(center - tangent * s + bitangent * s);
            // 双面
            tris.Add(v0); tris.Add(v0 + 2); tris.Add(v0 + 1);
            tris.Add(v0); tris.Add(v0 + 3); tris.Add(v0 + 2);
            tris.Add(v0); tris.Add(v0 + 1); tris.Add(v0 + 2);
            tris.Add(v0); tris.Add(v0 + 2); tris.Add(v0 + 3);
        }
    }
}

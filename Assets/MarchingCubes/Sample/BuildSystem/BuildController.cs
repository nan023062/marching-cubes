using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public abstract class BuildController : MonoBehaviour, IBuildState
    {
        [SerializeField] protected MarchingCubes.Sample.Cursor _cursor;

        protected Mesh         _visualMesh;
        protected Mesh         _colliderMesh;
        protected MeshCollider _meshCollider;
        bool                   _active;

        const    float   ClickMaxDuration = 0.5f;
        readonly float[] _pressTime       = { -1f, -1f };

        // ── 输入主循环（子类不需要自己写 Update）────────────────────────────────

        void Update()
        {
            if (!_active) return;

            bool onMesh = TryRaycast(out var hit, out var ray);
            OnPointerMove(hit, ray, onMesh);

            if (!onMesh) return;

            for (int btn = 0; btn <= 1; btn++)
            {
                bool left = btn == 0;

                if (Input.GetMouseButtonDown(btn))
                {
                    _pressTime[btn] = Time.time;
                    OnPointerDown(hit, left);
                }

                if (Input.GetMouseButton(btn) && _pressTime[btn] >= 0f)
                    OnPointerDrag(hit, left);

                if (Input.GetMouseButtonUp(btn) && _pressTime[btn] >= 0f)
                {
                    if (Time.time - _pressTime[btn] <= ClickMaxDuration)
                        OnPointerClick(hit, left);
                    _pressTime[btn] = -1f;
                    OnPointerUp(hit, left);
                }
            }
        }

        protected virtual void OnPointerMove (RaycastHit hit, Ray ray, bool onMesh) { }
        protected virtual void OnPointerDown (RaycastHit hit, bool left) { }
        protected virtual void OnPointerDrag (RaycastHit hit, bool left) { }
        protected virtual void OnPointerUp   (RaycastHit hit, bool left) { }
        protected virtual void OnPointerClick(RaycastHit hit, bool left) { }

        // ── Raycast 工具 ─────────────────────────────────────────────────────────

        bool TryRaycast(out RaycastHit hit, out Ray ray)
        {
            ray = default;
            hit = default;
            if (Camera.main == null) return false;
            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            return _meshCollider.Raycast(ray, out hit, 1000f);
        }

        // ── 网格基础设施 ─────────────────────────────────────────────────────────

        protected void InitGridMeshes(string visualName, string colliderName)
        {
            _visualMesh   = new Mesh { name = visualName };
            _colliderMesh = new Mesh { name = colliderName };

            var mf = GetComponent<MeshFilter>();
            mf.sharedMesh = _visualMesh;

            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = BuildingManager.Instance.GridMaterial;

            _meshCollider = GetComponent<MeshCollider>();
            _meshCollider.sharedMesh = _colliderMesh;

            SetInteraction(false);
        }

        protected void SetInteraction(bool active)
        {
            GetComponent<MeshRenderer>().enabled = active;
            _meshCollider.enabled                = active;
        }

        // ── IBuildState ──────────────────────────────────────────────────────────

        public void SetActive(bool active)
        {
            _active = active;
            if (_cursor != null) _cursor.gameObject.SetActive(active);
        }

        public abstract void OnEnter();
        public abstract void OnExit();
        public abstract void OnUpdate();
        public abstract void DrawGUI();
    }
}

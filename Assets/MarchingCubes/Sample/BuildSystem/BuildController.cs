using System.Collections.Generic;
using UnityEngine;
using MarchingSquareTerrain;

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

        // ── Case 预览 ────────────────────────────────────────────────────────

        bool                   _previewing;
        readonly List<GameObject> _previewObjects = new List<GameObject>();

        protected bool Previewing => _previewing;

        protected void TogglePreview(GameObject[] prefabs, float spacing = 1.5f)
        {
            if (_previewing) { ClearPreview(); return; }

            var valid = new List<GameObject>();
            foreach (var p in prefabs) if (p != null) valid.Add(p);
            if (valid.Count == 0) return;

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(valid.Count)));

            var cam   = Camera.main;
            Vector3 fwd   = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right   : Vector3.right;
            fwd.y   = 0f; fwd   = fwd.sqrMagnitude   > 0.001f ? fwd.normalized   : Vector3.forward;
            right.y = 0f; right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;

            float dist   = Mathf.Max(cols, Mathf.CeilToInt((float)valid.Count / cols)) * spacing * 0.6f + 6f;
            Vector3 base_ = cam != null ? cam.transform.position : Vector3.zero;
            base_.y = 0f;
            Vector3 origin = base_ + fwd * dist - right * ((cols - 1) * spacing * 0.5f);

            for (int i = 0; i < valid.Count; i++)
            {
                int r = i / cols, c = i % cols;
                Vector3 pos = origin + right * (c * spacing) + fwd * (r * spacing);
                pos.y = 0f;
                _previewObjects.Add(Object.Instantiate(valid[i], pos, Quaternion.identity));
            }
            _previewing = true;
        }

        protected void ClearPreview()
        {
            foreach (var go in _previewObjects) if (go != null) Object.Destroy(go);
            _previewObjects.Clear();
            _previewing = false;
        }

        protected virtual void OnDestroy() => ClearPreview();

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
            if (!active) ClearPreview();
        }

        public abstract void OnEnter();
        public abstract void OnExit();
        public abstract void OnUpdate();
        public abstract void DrawGUI();
    }
}

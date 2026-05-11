using UnityEngine;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public abstract class BuildController : MonoBehaviour, IBuildState
    {
        // ── 共享网格基础设施 ──────────────────────────────────────────────────
        // _visualMesh   → 挂到 MeshFilter，用于显示 grid 线框
        // _colliderMesh → 挂到 MeshCollider，用于交互碰撞检测

        protected Mesh         _visualMesh;
        protected Mesh         _colliderMesh;
        protected MeshCollider _meshCollider;
        protected int          _pressButton = -1;

        // 两个 Mesh 均自行创建（McController 等建造物使用）
        protected void InitGridMeshes(string visualName, string colliderName)
        {
            _visualMesh   = new Mesh { name = visualName };
            _colliderMesh = new Mesh { name = colliderName };
            GetComponent<MeshFilter>().sharedMesh = _visualMesh;
            _meshCollider = GetComponent<MeshCollider>();
            _meshCollider.sharedMesh = _colliderMesh;
        }
        
        // 启用/禁用碰撞层和视觉层（进入/退出建造模式时调用）
        protected void SetInteraction(bool active)
            => GetComponent<MeshRenderer>().enabled = active;

        // ── IBuildState 生命周期（子类实现） ──────────────────────────────────

        public abstract void OnEnter();
        
        public abstract void OnExit();
        
        public abstract void OnUpdate();
        
        public abstract void OnGUI();
    }
}

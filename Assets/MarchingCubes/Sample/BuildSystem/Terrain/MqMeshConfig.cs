using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// 存储 16 个 Marching Squares case（0-15）各自对应的独立 Prefab。
    ///
    /// 角点编号（每个 quad 的四顶点，bit 0~3）：
    ///   V3(TL) ─── V2(TR)
    ///     │           │
    ///   V0(BL) ─── V1(BR)
    ///
    /// case index = bit0(V0) | bit1(V1) | bit2(V2) | bit3(V3)
    /// bit=1 表示该角点处于"高"位（相对 base+1），bit=0 表示 base 高度。
    ///
    /// 不使用 D4 对称归约：Mesh 几何 + 纹理 UV 双重组合要求每个 case 都有独立正确的 UV。
    /// 美术需提供全部 16 个（或至少 15 个，0/15 共用平 quad）独立 mesh。
    /// </summary>
    [CreateAssetMenu(fileName = "MqMeshConfig", menuName = "MarchingCubes/Mq Mesh Config")]
    public sealed class MqMeshConfig : ScriptableObject
    {
        [SerializeField] private GameObject[] _prefabs = new GameObject[16];

        private void OnEnable() => EnsureArray();

        private void EnsureArray()
        {
            if (_prefabs == null || _prefabs.Length != 16)
                _prefabs = new GameObject[16];
        }

        // ── Public API ────────────────────────────────────────────────────────

        public GameObject GetPrefab(int caseIndex)
        {
            EnsureArray();
            return (caseIndex >= 0 && caseIndex < 16) ? _prefabs[caseIndex] : null;
        }

        public void SetPrefab(int caseIndex, GameObject prefab)
        {
            EnsureArray();
            if (caseIndex >= 0 && caseIndex < 16)
                _prefabs[caseIndex] = prefab;
        }
    }
}

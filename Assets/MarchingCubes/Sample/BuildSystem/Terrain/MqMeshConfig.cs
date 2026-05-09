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
    /// 共 19 个 case：
    ///   0-14：标准 case（四角高差 ≤ 1，由 bit mask 决定）
    ///   15-18：对角高差 == 2 的特殊 case（需独立 mesh）
    ///     15=V0+2/V2=base  16=V1+2/V3=base  17=V2+2/V0=base  18=V3+2/V1=base
    /// </summary>
    [CreateAssetMenu(fileName = "MqMeshConfig", menuName = "MarchingCubes/Mq Mesh Config")]
    public sealed class MqMeshConfig : ScriptableObject
    {
        public const int CaseCount = 19;

        [SerializeField] private GameObject[] _prefabs = new GameObject[CaseCount];

        private void OnEnable() => EnsureArray();

        private void EnsureArray()
        {
            if (_prefabs == null || _prefabs.Length != CaseCount)
            {
                var old = _prefabs;
                _prefabs = new GameObject[CaseCount];
                if (old != null)
                    for (int i = 0; i < Mathf.Min(old.Length, CaseCount); i++)
                        _prefabs[i] = old[i];
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public GameObject GetPrefab(int caseIndex)
        {
            EnsureArray();
            return (caseIndex >= 0 && caseIndex < CaseCount) ? _prefabs[caseIndex] : null;
        }

        public void SetPrefab(int caseIndex, GameObject prefab)
        {
            EnsureArray();
            if (caseIndex >= 0 && caseIndex < CaseCount)
                _prefabs[caseIndex] = prefab;
        }
    }
}

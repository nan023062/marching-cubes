using UnityEngine;

namespace MarchingCubes
{
    public abstract class CasePrefabConfig : ScriptableObject
    {
        public abstract GameObject GetPrefab(int caseIndex);
    }
}

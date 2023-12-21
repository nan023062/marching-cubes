using Unity.Entities;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    struct CubeGeneratorByPrefab : IComponentData
    {
        public Entity cubePrefab;
        public int count;
    }
    
    public class CubeGeneratorByAuthoring : MonoBehaviour
    {
        public GameObject cubePrefab;
        [Range(0,100)] public int count = 10;
        
        public class Baker : Baker<CubeGeneratorByAuthoring>
        {
            public override void Bake(CubeGeneratorByAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var prefabEntity = GetEntity(authoring.cubePrefab, TransformUsageFlags.Dynamic);
                AddComponent(entity,new CubeGeneratorByPrefab()
                {
                    cubePrefab = prefabEntity,
                    count = authoring.count
                });
            }
        }
    }
}
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    struct BlueCubeTag : IComponentData
    {
        
    }
    
    public class BlueCubeAuthoring : MonoBehaviour 
    {
        public class Baker : Baker<BlueCubeAuthoring>
        {
            public override void Bake(BlueCubeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new BlueCubeTag());
            }
        }
    }

}
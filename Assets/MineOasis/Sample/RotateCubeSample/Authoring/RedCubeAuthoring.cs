using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    struct RedCubeTag : IComponentData
    {
        
    }
    
    public class RedCubeAuthoring : MonoBehaviour 
    {
        public class Baker : Baker<RedCubeAuthoring>
        {
            public override void Bake(RedCubeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RedCubeTag());
            }
        }
    }

}
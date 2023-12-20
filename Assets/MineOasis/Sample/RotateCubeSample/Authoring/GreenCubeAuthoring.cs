using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace MineOasis.Sample.RotateCubeSample
{
    struct GreenCubeTag : IComponentData
    {
        
    }
    
    public class GreenCubeAuthoring : MonoBehaviour 
    {
        public class Baker : Baker<GreenCubeAuthoring>
        {
            public override void Bake(GreenCubeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GreenCubeTag());
            }
        }
    }

}
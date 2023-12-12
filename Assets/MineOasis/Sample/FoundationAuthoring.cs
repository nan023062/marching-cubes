using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MineOasis.Sample
{
    public class FoundationAuthoring : MonoBehaviour
    {
        public Foundation foundation;
        
        public class FoundationBaker : Baker<FoundationAuthoring>
        {
            public override void Bake(FoundationAuthoring authoring)
            {
                var entity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
                AddComponent(entity, new Cube()
                {
                    Size =  authoring.foundation.size,
                    Center = authoring.foundation.position,
                    Rotation = authoring.foundation.rotation
                });
            }
        }
    }
}
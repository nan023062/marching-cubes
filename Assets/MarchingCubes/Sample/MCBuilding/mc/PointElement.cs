using UnityEngine;
using UnityEngine.Serialization;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public abstract class PointElement : MonoBehaviour
    {
        private MeshRenderer _renderer;
        [FormerlySerializedAs("marchingCubes")] public MCBuilding mcs;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _renderer.enabled = false;
        }

        public void SetHighlight(bool active) => _renderer.enabled = active;
    }
}

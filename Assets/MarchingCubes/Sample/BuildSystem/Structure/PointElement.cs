using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public abstract class PointElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private MeshRenderer _renderer;
        [FormerlySerializedAs("marchingCubes")] public Structure mcs;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _renderer.enabled = false;
        }

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            bool left = eventData.button == PointerEventData.InputButton.Left;
            mcs.OnClicked(this, left, eventData.pointerCurrentRaycast.worldNormal);
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData) => _renderer.enabled = true;
        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)  => _renderer.enabled = false;
    }
}

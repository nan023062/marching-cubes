using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public abstract class PointElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private MeshRenderer _renderer;
        private BoxCollider _collider;
        public MarchingCubesSample marchingCubes;
        
        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<BoxCollider>();
            _renderer.enabled = false;
        }

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            bool left = eventData.button == PointerEventData.InputButton.Left;
            marchingCubes.OnClicked(this, left, eventData.pointerCurrentRaycast.worldNormal);
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            _renderer.enabled = true;
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            _renderer.enabled = false;
        }
    }
}
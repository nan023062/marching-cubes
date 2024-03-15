//****************************************************************************
// File: InputElement.cs
// Author: Li Nan
// Date: 2024-03-08 12:00
// Version: 1.0
//****************************************************************************

using UnityEngine;
using UnityEngine.EventSystems;

namespace MineOasis
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public abstract class InputElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private MeshRenderer _renderer;
        
        public abstract CubedWorld world { get; }
        
        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            OnSelected(eventData);
            
            _renderer ??= GetComponent<MeshRenderer>();
            _renderer.enabled = true;
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            _renderer ??= GetComponent<MeshRenderer>();
            _renderer.enabled = false;
            
            OnDeselected(eventData);
        }

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            OnClicked(eventData);
        }

        protected abstract void OnSelected(PointerEventData eventData);

        protected abstract void OnDeselected(PointerEventData eventData);

        protected abstract void OnClicked(PointerEventData eventData);
    }
}
//****************************************************************************
// File: GameObjectPool.cs
// Author: Li Nan
// Date: 2024-03-08 12:00
// Version: 1.0
//****************************************************************************

using System;

namespace MineOasis
{
    public sealed class GameObjectPool<T> :IDisposable where T : UnityEngine.MonoBehaviour
    {
        private readonly T _prefab;
        
        private readonly int _growth;
        
        private T[] _elements;
        
        private int _index;
        
        public GameObjectPool(T prefab, int capacity, int growth = 8)
        {
            _index = 0;
            _growth = growth;
            _prefab = prefab;
            _elements = new T[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _elements[i] = UnityEngine.Object.Instantiate(prefab);
                _elements[i].gameObject.SetActive(false);
            }
        }
        
        public void Dispose()
        {
            foreach (var element in _elements)
            {
                UnityEngine.Object.Destroy(element.gameObject);
            }
            _index = 0;
            _elements = null;
        }
        
        private void ReSize(int capacity)
        {
            int oldCapacity = _elements.Length;
            if(capacity == oldCapacity)
                return;
            
            T[] newElements = new T[capacity];
            for (int i = 0; i < oldCapacity; i++)
            {
                newElements[i] = _elements[i];
            }
            
            for (int i = oldCapacity; i < capacity; i++)
            {
                newElements[i] = UnityEngine.Object.Instantiate(_prefab);
                newElements[i].gameObject.SetActive(false);
            }
            _elements = newElements;
        }

        public T Spawn()
        {
            int capacity = _elements.Length;
            if (_index < capacity)
            {
                return _elements[_index++];
            }
            
            ReSize(Math.Min(capacity * 2, _growth));
            return Spawn();
        }
        
        
        public void UnSpawn(T element)
        {
            if (_index > 0)
            {
                _elements[--_index] = element;
            }
        }
    }
}
//****************************************************************************
// File: CameraController.cs
// Author: Li Nan
// Date: 2023-08-30 12:00
// Version: 1.0
//****************************************************************************

using System;
using UnityEngine;

namespace MarchingSquares
{
    public class CameraController : MonoBehaviour
    {
        public float wheelMul = 1f;
        public float moveSensitivity = 450F;
        public float sensitivityX = 450F;
        public float sensitivityY = 450F;

        public float minimumY = -70F;
        public float maximumY = 80F;

        private float _rotationX, _rotationY;

        public void Start()
        {
            Vector3 eulerAngles = transform.rotation.eulerAngles;
            _rotationX = eulerAngles.x;
            _rotationY = eulerAngles.y;
        }

        private void Update()
        {
            Transform t = transform;

            float deltaTime = Time.deltaTime;
            float x = Input.GetAxis("Horizontal") * deltaTime;
            float z = Input.GetAxis("Vertical") * deltaTime;
            Vector3 horizontal = t.right * (x * moveSensitivity);
            Vector3 vertical = t.forward * (z * moveSensitivity);
            wheelMul = Mathf.Clamp(wheelMul + Input.GetAxis("Mouse ScrollWheel"), 0.001f, 10f);
            t.position += (horizontal + vertical) * wheelMul;

            if (Input.GetMouseButton(1))
            {
                _rotationY += Input.GetAxis("Mouse X") * sensitivityX * deltaTime;
                _rotationX -= Input.GetAxis("Mouse Y") * sensitivityY * deltaTime;
                _rotationX = Mathf.Clamp(_rotationX, minimumY, maximumY);
                Vector3 eulerAngles = new Vector3(_rotationX, _rotationY, 0);
                t.rotation = Quaternion.Euler(eulerAngles);
            }
        }
    }
}
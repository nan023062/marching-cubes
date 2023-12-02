//****************************************************************************
// File: TPSCamera.cs
// Author: Li Nan
// Date: 2023-12-02 12:00
// Version: 1.0
//****************************************************************************

using UnityEngine;

namespace MarchingCubes.Sample
{
    public class TPSCamera : MonoBehaviour
    {
        public Transform mainCamera;
        public float yawSensitivity = 1500f;
        public float pitchSensitivity = 1000f;
        public float zoomSensitivity = 100f;
        
        private float _yaw;
        private float _pitch;
        private float _cameraDistance;
        
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        
        private void Awake()
        {
            Vector3 euler = mainCamera.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
            _cameraDistance = Vector3.Distance(transform.position, mainCamera.position);
        }
        
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (Input.GetMouseButton(0))
            {
                _yaw += Input.GetAxis("Mouse X") * yawSensitivity * deltaTime;
                _pitch -= Input.GetAxis("Mouse Y") * pitchSensitivity * deltaTime;
            }
            
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            _cameraDistance -= Input.GetAxis("Mouse ScrollWheel") * zoomSensitivity * deltaTime;
            _cameraDistance = Mathf.Clamp(_cameraDistance, 1f, 10f);
        }
        
        private void LateUpdate()
        {
            if (mainCamera != null)
            {
                Vector3 target = transform.position;
                Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 cameraDirection = rotation * Vector3.forward;
                Vector3 cameraPosition = target - cameraDirection * _cameraDistance;
                mainCamera.position = cameraPosition; 
                mainCamera.rotation = rotation;
            }
        }
    }
}
//****************************************************************************
// File: Character.cs
// Author: Li Nan
// Date: 2023-12-02 12:00
// Version: 1.0
//****************************************************************************

using UnityEngine;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(CharacterController))]
    public class Character : MonoBehaviour
    {
        [SerializeField]
        private TPSCamera _tpsCamera;
        
        private CharacterController _characterController;
        
        public float forceSensitive = 20f;
        // 摩擦力
        public float friction = 0.98f;
        public float gravity = 9.8f;
        
        private Vector3 _velocity;

        private void Awake()
        {
            _velocity = new Vector3(0, -100, 0);
            _characterController = GetComponent<CharacterController>();
        }
        
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            bool isGrounded = _characterController.isGrounded;
            float xAcc = Input.GetAxis("Horizontal") * forceSensitive;
            float yAcc = Input.GetAxis("Vertical")* forceSensitive;
            // Debug.Log($"xAcc:{xAcc}, yAcc:{yAcc}");
            Vector3 input = new Vector3(xAcc, 0, yAcc);
            
            Quaternion cameraRotation = Quaternion.Euler(0, _tpsCamera.Yaw, 0);
            Vector3 acc = cameraRotation * input;
            Vector3 frictionAcc = new Vector3(-_velocity.x, 0, -_velocity.z) * friction;
            Vector3 gravityAcc = new Vector3(0, -gravity, 0);
            if (isGrounded)
            {
                if (Input.GetButtonDown("Jump"))
                {
                    _velocity.y = 12f;
                }
            }

            _velocity += (acc + frictionAcc + gravityAcc) * deltaTime;
            Vector3 motion = _velocity * deltaTime;
            _characterController.Move(motion);
            
            if(acc.sqrMagnitude > 0.0001f)
            {
                Quaternion rotation = Quaternion.LookRotation(acc);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, deltaTime * 5f);
            }
        }
    }
}
using System;
using UnityEngine;

namespace CharacterSystem.Core
{
    public class GroundDetector : MonoBehaviour
    {
        public enum GroundCheckMethod
        {
            Raycast,
            SphereCast
        }

        [SerializeField] private LayerMask groundLayers;
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private Vector3 groundCheckOffset = new Vector3(0f, -0.95f, 0f);
        [SerializeField] private GroundCheckMethod groundCheckMethod = GroundCheckMethod.SphereCast;

        public bool IsGrounded { get; private set; }

        public event Action OnGrounded;
        public event Action OnAirborne;

        private bool _wasGrounded;

        private void FixedUpdate()
        {
            Vector3 checkPosition = transform.position + groundCheckOffset;

            IsGrounded = groundCheckMethod switch
            {
                GroundCheckMethod.SphereCast => Physics.CheckSphere(checkPosition, groundCheckRadius, groundLayers),
                GroundCheckMethod.Raycast => Physics.Raycast(checkPosition, Vector3.down, groundCheckRadius, groundLayers),
                _ => false
            };

            if (IsGrounded && !_wasGrounded)
            {
                OnGrounded?.Invoke();
            }
            else if (!IsGrounded && _wasGrounded)
            {
                OnAirborne?.Invoke();
            }

            _wasGrounded = IsGrounded;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 checkPosition = transform.position + groundCheckOffset;
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
        }
    }
}

using System;
using UnityEngine;
using CharacterSystem.Core;
using CharacterSystem.Settings;
using Unity.Netcode;

namespace CharacterSystem.Handlers
{
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(GroundDetector))]
    [RequireComponent(typeof(InputProvider))]
    public class JumpHandler : NetworkBehaviour
    {
        [SerializeField] private JumpSettings jumpSettings;

        public event Action OnJumped;
        public event Action OnLanded;

        private InputProvider inputProvider;
        private CharacterMotor motor;
        private GroundDetector groundDetector;

        private float lastGroundedTime;
        private float jumpBufferTimer;
        private float jumpStartTime;
        private bool hasJumped;
        private bool wasAirborne;

        private void Awake()
        {
            inputProvider = GetComponent<InputProvider>();
            motor = GetComponent<CharacterMotor>();
            groundDetector = GetComponent<GroundDetector>();
        }

        private void OnEnable()
        {
            inputProvider.OnJumpPressed += HandleJumpPressed;
            inputProvider.OnJumpReleased += HandleJumpReleased;
        }

        private void OnDisable()
        {
            inputProvider.OnJumpPressed -= HandleJumpPressed;
            inputProvider.OnJumpReleased -= HandleJumpReleased;
        }

        private void Update()
        {
            if(!IsOwner) return;
            bool grounded = groundDetector.IsGrounded;

            if (grounded)
            {
                lastGroundedTime = Time.time;

                if (wasAirborne)
                {
                    hasJumped = false;
                    wasAirborne = false;
                    OnLanded?.Invoke();
                }
            }
            else
            {
                wasAirborne = true;
            }

            if (jumpBufferTimer > 0f)
            {
                jumpBufferTimer -= Time.deltaTime;

                if (CanJump())
                {
                    ExecuteJump();
                }
            }
        }

        private void HandleJumpPressed()
        {
            if(!IsOwner) return;
            jumpBufferTimer = jumpSettings.JumpBufferTime;

            if (CanJump())
            {
                ExecuteJump();
            }
        }

        private void HandleJumpReleased()
        {
            if(!IsOwner) return;

            if (!hasJumped) return;

            float elapsed = Time.time - jumpStartTime;
            if (elapsed < jumpSettings.MinJumpTime) return;

            if (motor.Velocity.y > 0f)
            {
                motor.SetVerticalVelocity(motor.Velocity.y * jumpSettings.JumpCutMultiplier);
            }
        }

        private bool CanJump()
        {
            if (hasJumped) return false;

            bool withinCoyoteTime = (Time.time - lastGroundedTime) <= jumpSettings.CoyoteTime;
            return groundDetector.IsGrounded || withinCoyoteTime;
        }

        private void ExecuteJump()
        {
            motor.SetVerticalVelocity(jumpSettings.JumpForce);

            hasJumped = true;
            jumpBufferTimer = 0f;
            jumpStartTime = Time.time;
            lastGroundedTime = -999f;

            OnJumped?.Invoke();
        }
    }
}

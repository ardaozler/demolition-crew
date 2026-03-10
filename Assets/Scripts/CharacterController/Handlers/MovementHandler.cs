using UnityEngine;
using CharacterSystem.Core;
using CharacterSystem.Settings;
using Unity.Netcode;

namespace CharacterSystem.Handlers
{
    [RequireComponent(typeof(InputProvider))]
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(GroundDetector))]
    public class MovementHandler : NetworkBehaviour
    {
        [SerializeField] private MovementSettings movementSettings;

        private InputProvider inputProvider;
        private CharacterMotor motor;
        private GroundDetector groundDetector;

        private void Awake()
        {
            inputProvider = GetComponent<InputProvider>();
            motor = GetComponent<CharacterMotor>();
            groundDetector = GetComponent<GroundDetector>();
        }

        private void FixedUpdate()
        {
            if(!IsOwner) return;
            Vector2 rawInput = inputProvider.MoveInput;

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            Vector3 targetDirection = right * rawInput.x + forward * rawInput.y;

            float targetSpeed = targetDirection.magnitude * movementSettings.MoveSpeed;
            targetSpeed = Mathf.Min(targetSpeed, movementSettings.MaxSpeed);
            Vector3 targetVelocity = targetDirection.normalized * targetSpeed;

            Vector3 currentHorizontal = motor.Velocity;
            currentHorizontal.y = 0f;

            float accel = targetSpeed > 0.01f
                ? movementSettings.Acceleration
                : movementSettings.Deceleration;

            if (!groundDetector.IsGrounded)
            {
                accel *= movementSettings.AirControlMultiplier;
            }

            Vector3 newHorizontal = Vector3.MoveTowards(
                currentHorizontal,
                targetVelocity,
                accel * Time.fixedDeltaTime
            );

            motor.SetHorizontalVelocity(newHorizontal);
        }
    }
}

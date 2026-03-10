using UnityEngine;
using Unity.Netcode;
using CharacterSystem.Core;
using CharacterSystem.Settings;

namespace CharacterSystem.Handlers
{
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(InputProvider))]
    public class GravityHandler : NetworkBehaviour
    {
        [SerializeField] private GravitySettings gravitySettings;

        private CharacterMotor motor;
        private InputProvider inputProvider;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            inputProvider = GetComponent<InputProvider>();
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;

            float multiplier = 1f;

            if (motor.Velocity.y < 0f)
            {
                multiplier = gravitySettings.FallMultiplier;
            }
            else if (motor.Velocity.y > 0f && !inputProvider.JumpHeld)
            {
                multiplier = gravitySettings.LowJumpMultiplier;
            }

            Vector3 extraGravity = Physics.gravity * (gravitySettings.GravityScale * multiplier - 1f);
            motor.AddForce(extraGravity, ForceMode.Acceleration);

            Vector3 velocity = motor.Velocity;
            if (velocity.y < -gravitySettings.MaxFallSpeed)
            {
                velocity.y = -gravitySettings.MaxFallSpeed;
                motor.SetVerticalVelocity(velocity.y);
            }
        }
    }
}

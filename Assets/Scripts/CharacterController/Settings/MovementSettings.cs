using UnityEngine;

namespace CharacterSystem.Settings
{
    [CreateAssetMenu(fileName = "MovementSettings", menuName = "Character/Settings/Movement")]
    public class MovementSettings : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("Base movement speed.")]
        [SerializeField] private float moveSpeed = 8f;

        [Tooltip("How quickly the character reaches move speed.")]
        [SerializeField] private float acceleration = 50f;

        [Tooltip("How quickly the character slows to a stop.")]
        [SerializeField] private float deceleration = 40f;

        [Tooltip("Maximum achievable speed.")]
        [SerializeField] private float maxSpeed = 15f;

        [Header("Air Control")]
        [Tooltip("Multiplier applied to movement input while airborne.")]
        [SerializeField, Range(0f, 1f)] private float airControlMultiplier = 0.5f;

        [Header("Turning")]
        [Tooltip("How quickly the character rotates toward the movement direction.")]
        [SerializeField] private float turnSpeed = 10f;

        public float MoveSpeed => moveSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float MaxSpeed => maxSpeed;
        public float AirControlMultiplier => airControlMultiplier;
        public float TurnSpeed => turnSpeed;
    }
}

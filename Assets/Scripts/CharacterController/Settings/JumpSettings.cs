using UnityEngine;

namespace CharacterSystem.Settings
{
    [CreateAssetMenu(fileName = "JumpSettings", menuName = "Character/Settings/Jump")]
    public class JumpSettings : ScriptableObject
    {
        [Header("Jump")]
        [Tooltip("Upward force applied when jumping.")]
        [SerializeField] private float jumpForce = 10f;

        [Tooltip("Maximum number of jumps allowed (including the initial jump).")]
        [SerializeField] private int maxJumps = 2;

        [Header("Timing")]
        [Tooltip("Grace period after leaving a ledge during which a jump is still allowed.")]
        [SerializeField] private float coyoteTime = 0.15f;

        [Tooltip("Duration before landing during which a jump input is buffered.")]
        [SerializeField] private float jumpBufferTime = 0.1f;

        [Header("Variable Height")]
        [Tooltip("Velocity multiplier applied when the jump button is released early.")]
        [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;

        [Tooltip("Minimum time the jump force is applied before a cut can occur.")]
        [SerializeField] private float minJumpTime = 0.1f;

        public float JumpForce => jumpForce;
        public int MaxJumps => maxJumps;
        public float CoyoteTime => coyoteTime;
        public float JumpBufferTime => jumpBufferTime;
        public float JumpCutMultiplier => jumpCutMultiplier;
        public float MinJumpTime => minJumpTime;
    }
}

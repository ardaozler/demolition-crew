using UnityEngine;

namespace CharacterSystem.Settings
{
    [CreateAssetMenu(fileName = "GravitySettings", menuName = "Character/Settings/Gravity")]
    public class GravitySettings : ScriptableObject
    {
        [Header("Gravity")]
        [Tooltip("Overall gravity multiplier.")]
        [SerializeField] private float gravityScale = 2f;

        [Tooltip("Extra gravity multiplier applied when falling.")]
        [SerializeField] private float fallMultiplier = 2.5f;

        [Tooltip("Terminal fall velocity.")]
        [SerializeField] private float maxFallSpeed = 30f;

        [Tooltip("Gravity multiplier applied when the jump button is released early.")]
        [SerializeField] private float lowJumpMultiplier = 2f;

        public float GravityScale => gravityScale;
        public float FallMultiplier => fallMultiplier;
        public float MaxFallSpeed => maxFallSpeed;
        public float LowJumpMultiplier => lowJumpMultiplier;
    }
}

using UnityEngine;

namespace InteractionSystem.Tools
{
    [CreateAssetMenu(fileName = "SledgehammerSettings", menuName = "DestructionCrew/Sledgehammer Settings")]
    public class SledgehammerSettings : ScriptableObject
    {
        [Header("Combat")]
        [SerializeField] private float damage = 50f;
        [SerializeField] private float hitRange = 3f;
        [SerializeField] private float damageRadius = 0.5f;
        [SerializeField] private float cooldown = 0.8f;

        [Header("Physics")]
        [SerializeField] private float hitForce = 10f;
        [SerializeField] private float lateralBias = 0.3f;

        public float Damage => damage;
        public float HitRange => hitRange;
        public float DamageRadius => damageRadius;
        public float Cooldown => cooldown;
        public float HitForce => hitForce;
        public float LateralBias => lateralBias;
    }
}

using UnityEngine;

namespace InteractionSystem.Tools
{
    [CreateAssetMenu(fileName = "SledgehammerSettings", menuName = "DestructionCrew/Sledgehammer Settings")]
    public class SledgehammerSettings : ScriptableObject
    {
        [Header("Combat")]
        [SerializeField] private float damage = 50f;
        [SerializeField] private float hitRange = 3f;
        [SerializeField] private float cooldown = 0.8f;

        [Header("Physics")]
        [SerializeField] private float hitForce = 10f;

        public float Damage => damage;
        public float HitRange => hitRange;
        public float Cooldown => cooldown;
        public float HitForce => hitForce;
    }
}

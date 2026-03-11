using UnityEngine;

namespace InteractionSystem.Tools
{
    [CreateAssetMenu(fileName = "BombSettings", menuName = "DestructionCrew/Bomb Settings")]
    public class BombSettings : ScriptableObject
    {
        [Header("Throw")]
        [SerializeField] private float throwForce = 15f;

        [Header("Explosion")]
        [SerializeField] private float range = 8f;
        [SerializeField] private float strength = 5f;
        [SerializeField] private float damage = 100f;
        [SerializeField] private int chaos = 30;
        [SerializeField] private int variation = 50;

        public float ThrowForce => throwForce;
        public float Range => range;
        public float Strength => strength;
        public float Damage => damage;
        public int Chaos => chaos;
        public int Variation => variation;
    }
}

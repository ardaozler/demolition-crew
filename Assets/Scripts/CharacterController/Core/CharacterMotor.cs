using UnityEngine;

namespace CharacterSystem.Core
{
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterMotor : MonoBehaviour
    {
        private Rigidbody _rb;

        public Rigidbody Rb => _rb;

        public Vector3 Velocity
        {
            get => _rb.linearVelocity;
            set => _rb.linearVelocity = value;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void SetVerticalVelocity(float yVelocity)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, yVelocity, _rb.linearVelocity.z);
        }

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            _rb.AddForce(force, mode);
        }

        public void AddImpulse(Vector3 impulse)
        {
            _rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}

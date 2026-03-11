using UnityEngine;

namespace InteractionSystem
{
    public class InteractionDetector : MonoBehaviour
    {
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private float sphereCastRadius = 0.2f;
        [SerializeField] private LayerMask interactableMask = ~0;

        public IInteractable CurrentTarget { get; private set; }

        private void Update()
        {
            if (rayOrigin == null) return;

            if (Physics.SphereCast(rayOrigin.position, sphereCastRadius, rayOrigin.forward, out RaycastHit hit, interactRange, interactableMask))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                CurrentTarget = interactable;
            }
            else
            {
                CurrentTarget = null;
            }
        }
    }
}

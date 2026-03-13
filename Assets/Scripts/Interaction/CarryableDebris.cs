#nullable enable
using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Added to demolished fragments that are small enough to carry.
    /// Implements IInteractable so the InteractionDetector can target them.
    /// </summary>
    public class CarryableDebris : MonoBehaviour, IInteractable
    {
        public const float MaxCarryableExtent = 2f;

        public int RegistryId { get; set; } = -1;

        public string InteractionPrompt => "Pick up debris";

        public bool CanInteract(GameObject player) => RegistryId >= 0 && !FragmentCarrier.IsClaimed(RegistryId);

        public void Interact(GameObject player) { }

        /// <summary>
        /// Checks fragment size and adds CarryableDebris if small enough.
        /// </summary>
        public static bool TryAdd(GameObject fragment, int registryId)
        {
            var meshRenderer = fragment.GetComponent<Renderer>();
            if (meshRenderer == null)
                return false;

            var size = meshRenderer.bounds.size;
            if (Mathf.Max(size.x, size.y, size.z) > MaxCarryableExtent)
                return false;

            var debris = fragment.AddComponent<CarryableDebris>();
            debris.RegistryId = registryId;
            return true;
        }
    }
}

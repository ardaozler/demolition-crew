#nullable enable
using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Place in the scene with a collider. Players carrying debris can press [E]
    /// to deposit and destroy the fragment. The InteractionDetector picks this up
    /// via IInteractable, and EquipmentHandler delegates to FragmentCarrier.
    /// </summary>
    public class DebrisGrinder : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => "Deposit debris";

        public bool CanInteract(GameObject player)
        {
            var carrier = player.GetComponent<FragmentCarrier>();
            return carrier != null && carrier.IsCarrying;
        }

        public void Interact(GameObject player) { }
    }
}

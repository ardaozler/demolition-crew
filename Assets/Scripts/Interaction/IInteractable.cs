using UnityEngine;

namespace InteractionSystem
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(GameObject player);
        void Interact(GameObject player);
    }
}

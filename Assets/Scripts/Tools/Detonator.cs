using Unity.Netcode;
using UnityEngine;

namespace InteractionSystem
{
    [RequireComponent(typeof(NetworkObject))]
    public class Detonator : NetworkBehaviour, IUsable, IInteractable
    {
        private Bomb linkedBomb;
        private NetworkObject _cachedNetObj;

        public string UsableName => "Detonator";
        public string InteractionPrompt => "Pick up Detonator";

        private void Awake()
        {
            _cachedNetObj = GetComponent<NetworkObject>();
        }

        public void SetBomb(Bomb bomb)
        {
            linkedBomb = bomb;
        }

        public void OnEquip(GameObject owner) { }

        public void OnUnequip(GameObject owner)
        {
            // Player dropped the detonator without detonating
            if (linkedBomb != null && linkedBomb.gameObject != null)
                linkedBomb.ClearDetonatorRef();
        }

        public void OnUseStarted(GameObject owner, Vector3 aimOrigin, Vector3 aimDirection)
        {
            if (!IsServer) return;
            if (linkedBomb == null) return;

            var bomb = linkedBomb;
            linkedBomb = null;

            var handler = owner.GetComponent<EquipmentHandler>();

            // Unequip the detonator BEFORE despawning so equipment state is clean
            if (handler != null)
                handler.ServerForceUnequip();

            bomb.Detonate();

            NetworkObject.Despawn();
        }

        public void OnUseStopped(GameObject owner) { }

        public bool CanInteract(GameObject player)
        {
            return transform.parent == null || GetComponentInParent<NetworkObject>() == _cachedNetObj;
        }

        public void Interact(GameObject player) { }
    }
}

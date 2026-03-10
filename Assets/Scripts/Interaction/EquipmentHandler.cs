using Unity.Netcode;
using UnityEngine;
using CharacterSystem.Handlers;

namespace InteractionSystem
{
    [RequireComponent(typeof(InputProvider))]
    [RequireComponent(typeof(InteractionDetector))]
    public class EquipmentHandler : NetworkBehaviour
    {
        [SerializeField] private Transform holdPoint;

        private InputProvider inputProvider;
        private InteractionDetector detector;

        private NetworkVariable<NetworkObjectReference> equippedItemRef = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IUsable currentUsable;
        private NetworkObject equippedNetObj;

        public IUsable CurrentUsable => currentUsable;
        public bool HasEquipped => currentUsable != null;

        private void Awake()
        {
            inputProvider = GetComponent<InputProvider>();
            detector = GetComponent<InteractionDetector>();
        }

        public override void OnNetworkSpawn()
        {
            equippedItemRef.OnValueChanged += OnEquippedItemChanged;

            if (!IsOwner) return;

            inputProvider.OnUseStarted += HandleUseStarted;
            inputProvider.OnUseCanceled += HandleUseStopped;
            inputProvider.OnInteractStarted += HandleInteract;
        }

        public override void OnNetworkDespawn()
        {
            equippedItemRef.OnValueChanged -= OnEquippedItemChanged;

            if (!IsOwner) return;

            inputProvider.OnUseStarted -= HandleUseStarted;
            inputProvider.OnUseCanceled -= HandleUseStopped;
            inputProvider.OnInteractStarted -= HandleInteract;
        }

        private void HandleUseStarted()
        {
            if (currentUsable == null) return;
            UseServerRpc();
        }

        private void HandleUseStopped()
        {
            if (currentUsable == null) return;
            StopUseServerRpc();
        }

        private void HandleInteract()
        {
            Debug.Log("HandleInteract called");
            if (currentUsable != null)
            {
                UnequipServerRpc();
            }
            else if (detector.CurrentTarget != null)
            {
                Debug.Log("Current target: " + detector.CurrentTarget);
                var targetMono = detector.CurrentTarget as MonoBehaviour;
                if (targetMono == null) return;

                var netObj = targetMono.GetComponentInParent<NetworkObject>();
                if (netObj == null) return;

                if (detector.CurrentTarget.CanInteract(gameObject))
                {
                    Debug.Log("Interacting with " + netObj.name);
                    EquipServerRpc(netObj);
                }
            }
        }

        // --- Server RPCs ---

        [Rpc(SendTo.Server)]
        private void EquipServerRpc(NetworkObjectReference itemRef, RpcParams rpcParams = default)
        {
            Debug.Log("EquipServerRpc called with itemRef: " + itemRef);
            if (!itemRef.TryGet(out NetworkObject itemNetObj)) return;

            var interactable = itemNetObj.GetComponent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(gameObject)) return;

            var usable = itemNetObj.GetComponent<IUsable>();
            if (usable == null)
            {
                Debug.Log(itemNetObj.name + " is not usable and cannot be equipped.");
                return;
            }

            Debug.Log("Equipping " + itemNetObj.name);

            // Reparent to hold point
            itemNetObj.TrySetParent(NetworkObject);
            itemNetObj.transform.localPosition =
                holdPoint != null ? holdPoint.localPosition : new Vector3(0f, 0.5f, 0.8f);
            itemNetObj.transform.localRotation = holdPoint != null ? holdPoint.localRotation : Quaternion.identity;

            // Disable physics on picked-up item
            var rb = itemNetObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            var col = itemNetObj.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            equippedItemRef.Value = itemNetObj;
        }

        [Rpc(SendTo.Server)]
        private void UnequipServerRpc()
        {
            if (!equippedItemRef.Value.TryGet(out NetworkObject itemNetObj)) return;

            // Detach
            itemNetObj.TryRemoveParent();

            // Drop in front of player
            itemNetObj.transform.position = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            itemNetObj.transform.rotation = Quaternion.identity;

            // Re-enable physics
            var rb = itemNetObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }

            var col = itemNetObj.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }

            equippedItemRef.Value = default;
        }

        [Rpc(SendTo.Server)]
        private void UseServerRpc()
        {
            if (!equippedItemRef.Value.TryGet(out NetworkObject itemNetObj)) return;

            var usable = itemNetObj.GetComponent<IUsable>();
            usable?.OnUseStarted(gameObject);
        }

        [Rpc(SendTo.Server)]
        private void StopUseServerRpc()
        {
            if (!equippedItemRef.Value.TryGet(out NetworkObject itemNetObj)) return;

            var usable = itemNetObj.GetComponent<IUsable>();
            usable?.OnUseStopped(gameObject);
        }

        // --- NetworkVariable sync for all clients ---

        private void OnEquippedItemChanged(NetworkObjectReference oldRef, NetworkObjectReference newRef)
        {
            // Clean up old
            if (oldRef.TryGet(out NetworkObject oldObj))
            {
                var oldUsable = oldObj.GetComponent<IUsable>();
                oldUsable?.OnUnequip(gameObject);
            }

            currentUsable = null;
            equippedNetObj = null;

            // Set up new
            if (newRef.TryGet(out NetworkObject newObj))
            {
                equippedNetObj = newObj;
                currentUsable = newObj.GetComponent<IUsable>();
                currentUsable?.OnEquip(gameObject);
            }
        }
    }
}
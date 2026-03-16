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
        [SerializeField] private float maxEquipRange = 4f;
        [SerializeField] private float maxAimOriginTolerance = 2f;
        [SerializeField] private FragmentCarrier fragmentCarrier;
        [SerializeField] private PlayerCarrier playerCarrier;

        private InputProvider inputProvider;
        private InteractionDetector detector;
        private Camera ownerCamera;

        private NetworkVariable<NetworkObjectReference> equippedItemRef = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IUsable currentUsable;
        private NetworkObject equippedNetObj;

        // Server-side rate limiting
        private float _lastUseTime;
        private float _lastEquipTime;
        private const float MinEquipInterval = 0.2f;
        private const float MinUseInterval = 0.15f;

        public IUsable CurrentUsable => currentUsable;
        public bool HasEquipped => currentUsable != null;

        private void Awake()
        {
            inputProvider = GetComponent<InputProvider>();
            detector = GetComponent<InteractionDetector>();
            ownerCamera = GetComponentInChildren<Camera>(true);

            if (fragmentCarrier == null)
                fragmentCarrier = GetComponent<FragmentCarrier>();
            if (playerCarrier == null)
                playerCarrier = GetComponent<PlayerCarrier>();
        }

        public void SetFragmentCarrier(FragmentCarrier carrier) => fragmentCarrier = carrier;

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
            // Server: force-drop held item so it isn't orphaned on disconnect
            if (IsServer && equippedItemRef.Value.TryGet(out NetworkObject heldItem))
            {
                Vector3 dropPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
                heldItem.TryRemoveParent();
                heldItem.transform.position = dropPos;
                SetItemPhysics(heldItem, enabled: true);
                equippedItemRef.Value = default;
            }

            equippedItemRef.OnValueChanged -= OnEquippedItemChanged;

            if (!IsOwner) return;

            inputProvider.OnUseStarted -= HandleUseStarted;
            inputProvider.OnUseCanceled -= HandleUseStopped;
            inputProvider.OnInteractStarted -= HandleInteract;
        }

        private void HandleUseStarted()
        {
            if (ownerCamera == null) return;

            // Throw carried player on left-click
            if (playerCarrier != null && playerCarrier.IsCarryingPlayer)
            {
                Vector3 origin = ownerCamera.transform.position;
                Vector3 direction = ownerCamera.transform.forward;
                playerCarrier.RequestThrow(origin, direction);
                return;
            }

            if (currentUsable == null) return;

            Vector3 aimOrigin = ownerCamera.transform.position;
            Vector3 aimDirection = ownerCamera.transform.forward;
            UseServerRpc(aimOrigin, aimDirection);
        }

        private void HandleUseStopped()
        {
            if (currentUsable == null) return;
            StopUseServerRpc();
        }

        private void HandleInteract()
        {
            if (detector.CurrentTarget != null)
            {
                var targetMono = detector.CurrentTarget as MonoBehaviour;
                if (targetMono == null) return;

                if (!detector.CurrentTarget.CanInteract(gameObject)) return;

                // Grinder: deposit carried debris
                if (targetMono.GetComponent<DebrisGrinder>() != null)
                {
                    if (fragmentCarrier != null && fragmentCarrier.IsCarrying)
                        fragmentCarrier.RequestDeposit();
                    return;
                }

                // Debris pickup (only when not carrying a tool or player)
                var debris = targetMono.GetComponent<CarryableDebris>();
                if (debris != null)
                {
                    if (fragmentCarrier != null && !HasEquipped
                        && (playerCarrier == null || !playerCarrier.IsCarryingPlayer))
                        fragmentCarrier.RequestPickup(debris.RegistryId);
                    return;
                }

                // Player pickup (only when hands are free)
                var carryable = targetMono.GetComponent<PlayerCarrier>();
                if (carryable != null)
                {
                    if (playerCarrier != null && !HasEquipped
                        && !playerCarrier.IsCarryingPlayer
                        && (fragmentCarrier == null || !fragmentCarrier.IsCarrying))
                        playerCarrier.RequestCarry(targetMono.GetComponent<NetworkObject>());
                    return;
                }

                // Tool equip (only when not carrying debris or player)
                if (fragmentCarrier != null && fragmentCarrier.IsCarrying) return;
                if (playerCarrier != null && playerCarrier.IsCarryingPlayer) return;

                var netObj = targetMono.GetComponentInParent<NetworkObject>();
                if (netObj == null) return;

                EquipServerRpc(netObj);
                return;
            }

            // No valid target — drop carried player, debris, or tool
            if (playerCarrier != null && playerCarrier.IsCarryingPlayer)
            {
                playerCarrier.RequestDrop();
                return;
            }

            if (fragmentCarrier != null && fragmentCarrier.IsCarrying)
            {
                fragmentCarrier.RequestDrop();
                return;
            }

            if (currentUsable != null)
            {
                UnequipServerRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void EquipServerRpc(NetworkObjectReference itemRef, RpcParams rpcParams = default)
        {
            // Rate limit
            if (Time.time - _lastEquipTime < MinEquipInterval) return;
            _lastEquipTime = Time.time;

            if (!itemRef.TryGet(out NetworkObject itemNetObj)) return;

            var interactable = itemNetObj.GetComponent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(gameObject)) return;

            var usable = itemNetObj.GetComponent<IUsable>();
            if (usable == null) return;

            // Distance check — prevent remote pickup
            float dist = Vector3.Distance(transform.position, itemNetObj.transform.position);
            if (dist > maxEquipRange) return;

            // Verify item isn't parented to another player
            var existingParent = itemNetObj.transform.parent;
            if (existingParent != null && existingParent != transform)
                return;

            // Drop current item if holding something (swap-equip)
            if (equippedItemRef.Value.TryGet(out NetworkObject oldItem))
            {
                Vector3 dropPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
                oldItem.TryRemoveParent();
                DropItemClientRpc(oldItem, dropPos);
            }

            // Claim the item — if TrySetParent fails, another player won the race
            if (!itemNetObj.TrySetParent(NetworkObject))
                return;

            equippedItemRef.Value = itemNetObj;
        }

        [Rpc(SendTo.Server)]
        private void UnequipServerRpc()
        {
            if (!equippedItemRef.Value.TryGet(out NetworkObject itemNetObj)) return;

            Vector3 dropPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            itemNetObj.TryRemoveParent();
            DropItemClientRpc(itemNetObj, dropPos);
            equippedItemRef.Value = default;
        }

        [Rpc(SendTo.Everyone)]
        private void DropItemClientRpc(NetworkObjectReference itemRef, Vector3 dropPosition)
        {
            if (!itemRef.TryGet(out NetworkObject itemNetObj)) return;

            itemNetObj.transform.position = dropPosition;
            itemNetObj.transform.rotation = Quaternion.identity;
            SetItemPhysics(itemNetObj, enabled: true);
        }

        [Rpc(SendTo.Server)]
        private void UseServerRpc(Vector3 aimOrigin, Vector3 aimDirection)
        {
            if (!equippedItemRef.Value.TryGet(out NetworkObject itemObj)) return;

            // Rate limit
            if (Time.time - _lastUseTime < MinUseInterval) return;
            _lastUseTime = Time.time;

            // Validate aim origin is near the player's actual position
            float originDist = Vector3.Distance(aimOrigin, transform.position);
            if (originDist > maxAimOriginTolerance) return;

            // Reject degenerate aim directions (zero/near-zero → NaN after normalize)
            if (aimDirection.sqrMagnitude < 0.01f) return;

            // Derive usable directly from the equipped item to avoid stale currentUsable
            var usable = itemObj.GetComponent<IUsable>();
            usable?.OnUseStarted(gameObject, aimOrigin, aimDirection.normalized);
        }

        [Rpc(SendTo.Server)]
        private void StopUseServerRpc()
        {
            if (!equippedItemRef.Value.TryGet(out NetworkObject itemObj)) return;

            var usable = itemObj.GetComponent<IUsable>();
            usable?.OnUseStopped(gameObject);
        }

        private void LateUpdate()
        {
            if (equippedNetObj != null && holdPoint != null)
            {
                equippedNetObj.transform.position = holdPoint.position;
                equippedNetObj.transform.rotation = holdPoint.rotation;
            }
        }

        private void OnEquippedItemChanged(NetworkObjectReference oldRef, NetworkObjectReference newRef)
        {
            if (oldRef.TryGet(out NetworkObject oldObj))
            {
                SetItemPhysics(oldObj, enabled: true);
                var oldUsable = oldObj.GetComponent<IUsable>();
                oldUsable?.OnUnequip(gameObject);
            }

            currentUsable = null;
            equippedNetObj = null;

            if (newRef.TryGet(out NetworkObject newObj))
            {
                SetItemPhysics(newObj, enabled: false);
                equippedNetObj = newObj;
                currentUsable = newObj.GetComponent<IUsable>();
                currentUsable?.OnEquip(gameObject);
            }
        }

        /// <summary>
        /// Server-only: force-equip an item without interaction checks.
        /// Used by tools that need to swap the equipped item (e.g. Bomb → Detonator).
        /// </summary>
        public void ServerForceEquip(NetworkObject item)
        {
            if (!IsServer) return;

            // Drop existing item first to prevent ghost equipment
            if (equippedItemRef.Value.TryGet(out NetworkObject oldItem))
            {
                Vector3 dropPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
                oldItem.TryRemoveParent();
                DropItemClientRpc(oldItem, dropPos);
            }

            item.TrySetParent(NetworkObject);
            equippedItemRef.Value = item;
        }

        /// <summary>
        /// Server-only: force-unequip the current item without dropping it.
        /// </summary>
        public void ServerForceUnequip()
        {
            if (!IsServer) return;

            if (equippedItemRef.Value.TryGet(out NetworkObject item))
                item.TryRemoveParent();

            equippedItemRef.Value = default;
        }

        private static void SetItemPhysics(NetworkObject itemNetObj, bool enabled)
        {
            var rb = itemNetObj.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = !enabled;

            var col = itemNetObj.GetComponent<Collider>();
            if (col != null)
                col.enabled = enabled;
        }
    }
}

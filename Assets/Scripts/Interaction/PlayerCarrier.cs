using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using CharacterSystem.Camera;
using CharacterSystem.Core;
using CharacterSystem.Handlers;

namespace InteractionSystem
{
    public class PlayerCarrier : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Transform carryPoint;
        [SerializeField] private float maxPickupRange = 3f;
        [SerializeField] private float throwForce = 15f;

        private readonly NetworkVariable<NetworkObjectReference> _carriedPlayerRef = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<NetworkObjectReference> _myCarrierRef = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Transform _carriedTransform;
        private Rigidbody _carriedRb;
        private CapsuleCollider _carriedCollider;

        public bool IsCarryingPlayer => _carriedPlayerRef.Value.TryGet(out _);
        public bool IsBeingCarried => _myCarrierRef.Value.TryGet(out _);

        private void Awake()
        {
            if (carryPoint == null)
                carryPoint = transform.Find("CarryPoint");
        }

        // IInteractable
        public string InteractionPrompt => "Pick up Player";

        public bool CanInteract(GameObject player)
        {
            if (player == gameObject) return false;
            if (IsBeingCarried) return false;
            return true;
        }

        public void Interact(GameObject player) { }

        public override void OnNetworkSpawn()
        {
            _carriedPlayerRef.OnValueChanged += OnCarriedPlayerChanged;
            _myCarrierRef.OnValueChanged += OnMyCarrierChanged;

            // Handle late-join: resolve existing state
            if (_carriedPlayerRef.Value.TryGet(out NetworkObject carried))
                CacheCarriedPlayer(carried);
            if (_myCarrierRef.Value.TryGet(out NetworkObject carrier))
                CacheCarrier(carrier);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                // If I'm carrying someone, release them
                if (_carriedPlayerRef.Value.TryGet(out NetworkObject carried))
                {
                    var carriedPC = carried.GetComponent<PlayerCarrier>();
                    if (carriedPC != null)
                        carriedPC._myCarrierRef.Value = default;
                    RestorePhysics(carried);
                    _carriedPlayerRef.Value = default;
                }

                // If I'm being carried, notify carrier
                if (_myCarrierRef.Value.TryGet(out NetworkObject carrier))
                {
                    var carrierPC = carrier.GetComponent<PlayerCarrier>();
                    if (carrierPC != null)
                        carrierPC._carriedPlayerRef.Value = default;
                    _myCarrierRef.Value = default;
                }
            }

            _carriedPlayerRef.OnValueChanged -= OnCarriedPlayerChanged;
            _myCarrierRef.OnValueChanged -= OnMyCarrierChanged;
        }

        public void RequestCarry(NetworkObject target)
        {
            if (!IsOwner) return;
            CarryServerRpc(target);
        }

        public void RequestDrop()
        {
            if (!IsOwner) return;
            DropServerRpc();
        }

        public void RequestThrow(Vector3 aimOrigin, Vector3 aimDir)
        {
            if (!IsOwner) return;
            ThrowServerRpc(aimOrigin, aimDir);
        }


        [Rpc(SendTo.Server)]
        private void CarryServerRpc(NetworkObjectReference targetRef, RpcParams rpcParams = default)
        {
            if (!targetRef.TryGet(out NetworkObject targetNetObj)) return;
            if (targetNetObj == NetworkObject) return; // can't carry self

            var targetPC = targetNetObj.GetComponent<PlayerCarrier>();
            if (targetPC == null) return;

            // Can't pick up someone already being carried
            if (targetPC.IsBeingCarried) return;

            // Can't carry if I'm already carrying someone
            if (IsCarryingPlayer) return;

            // Can't carry if I'm being carried
            if (IsBeingCarried) return;

            // Mutual exclusivity: can't carry if holding tool or debris
            var equipment = GetComponent<EquipmentHandler>();
            if (equipment != null && equipment.HasEquipped) return;

            var fragCarrier = GetComponent<FragmentCarrier>();
            if (fragCarrier != null && fragCarrier.IsCarrying) return;

            // Distance check
            float dist = Vector3.Distance(transform.position, targetNetObj.transform.position);
            if (dist > maxPickupRange) return;

            // Set both network variables
            _carriedPlayerRef.Value = targetNetObj;
            targetPC._myCarrierRef.Value = NetworkObject;
        }

        [Rpc(SendTo.Server)]
        private void DropServerRpc()
        {
            ReleaseCarriedPlayer(false, Vector3.zero, Vector3.zero);
        }

        [Rpc(SendTo.Server)]
        private void ThrowServerRpc(Vector3 aimOrigin, Vector3 aimDir)
        {
            if (aimDir.sqrMagnitude < 0.01f) return;
            ReleaseCarriedPlayer(true, aimOrigin, aimDir.normalized);
        }

        private void ReleaseCarriedPlayer(bool doThrow, Vector3 aimOrigin, Vector3 aimDir)
        {
            if (!_carriedPlayerRef.Value.TryGet(out NetworkObject carriedNetObj)) return;

            var carriedPC = carriedNetObj.GetComponent<PlayerCarrier>();
            if (carriedPC != null)
                carriedPC._myCarrierRef.Value = default;

            _carriedPlayerRef.Value = default;

            // Position dropped player in front of carrier
            Vector3 dropPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            carriedNetObj.transform.position = dropPos;

            // Send release + throw to the carried player's owner so it happens
            // atomically — no server RestorePhysics that NT would overwrite.
            Vector3 force = doThrow ? aimDir * throwForce : Vector3.zero;
            if (carriedPC != null)
                carriedPC.OwnerReleaseRpc(dropPos, force);
        }

        /// <summary>
        /// Sent to the carried player's owner. Restores physics and applies
        /// throw force in one atomic step so NetworkTransform doesn't fight.
        /// </summary>
        [Rpc(SendTo.Owner)]
        private void OwnerReleaseRpc(Vector3 dropPosition, Vector3 force)
        {
            transform.position = dropPosition;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                if (force.sqrMagnitude > 0.01f)
                    rb.AddForce(force, ForceMode.Impulse);
            }

            var col = GetComponent<CapsuleCollider>();
            if (col != null)
                col.enabled = true;
        }

        private void OnCarriedPlayerChanged(NetworkObjectReference oldRef, NetworkObjectReference newRef)
        {
            _carriedTransform = null;
            _carriedRb = null;

            if (newRef.TryGet(out NetworkObject newObj))
                CacheCarriedPlayer(newObj);
        }

        private void OnMyCarrierChanged(NetworkObjectReference oldRef, NetworkObjectReference newRef)
        {
            if (newRef.TryGet(out NetworkObject newObj))
            {
                CacheCarrier(newObj);

                // Disable physics on carried player
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = true;

                var col = GetComponent<CapsuleCollider>();
                if (col != null)
                    col.enabled = false;

                // Disable NetworkTransform so it doesn't fight with carry positioning
                var netTransform = GetComponent<NetworkTransform>();
                if (netTransform != null)
                    netTransform.enabled = false;

                // Disable input and movement on the carried player so they can't
                // rotate/move independently while being held
                if (IsOwner)
                    SetInputEnabled(false);
            }
            else
            {
                // Re-enable NetworkTransform before restoring physics
                var netTransform = GetComponent<NetworkTransform>();
                if (netTransform != null)
                    netTransform.enabled = true;

                // Restore physics when released
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = false;

                var col = GetComponent<CapsuleCollider>();
                if (col != null)
                    col.enabled = true;

                // Re-enable input and movement
                if (IsOwner)
                    SetInputEnabled(true);
            }
        }

        /// <summary>
        /// Enables or disables movement, input, and camera on the local player.
        /// Called when being picked up or released.
        /// </summary>
        private void SetInputEnabled(bool enabled)
        {
            var movement = GetComponent<MovementHandler>();
            if (movement != null) movement.enabled = enabled;

            var jump = GetComponent<JumpHandler>();
            if (jump != null) jump.enabled = enabled;

            var gravity = GetComponent<GravityHandler>();
            if (gravity != null) gravity.enabled = enabled;

            var input = GetComponent<InputProvider>();
            if (input != null) input.enabled = enabled;

            var cam = GetComponentInChildren<FirstPersonCamera>();
            if (cam != null) cam.enabled = enabled;
        }

        private void CacheCarriedPlayer(NetworkObject carried)
        {
            _carriedTransform = carried.transform;
            _carriedRb = carried.GetComponent<Rigidbody>();
        }

        private void CacheCarrier(NetworkObject carrier)
        {
            var carrierPC = carrier.GetComponent<PlayerCarrier>();
            if (carrierPC != null)
                _ = carrierPC.carryPoint; // just validate it exists
        }

        private void LateUpdate()
        {
            // Carrier: position carried player at carry point (all clients see this)
            if (_carriedTransform != null && carryPoint != null)
            {
                _carriedTransform.position = carryPoint.position;
                _carriedTransform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }
        }

        private static void RestorePhysics(NetworkObject netObj)
        {
            var rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }

            var col = netObj.GetComponent<CapsuleCollider>();
            if (col != null)
                col.enabled = true;
        }
    }
}

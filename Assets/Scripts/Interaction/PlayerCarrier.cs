using Unity.Netcode;
using UnityEngine;

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
        private Transform _carrierCarryPoint;
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

            RestorePhysics(carriedNetObj);

            if (doThrow)
            {
                var rb = carriedNetObj.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.AddForce(aimDir * throwForce, ForceMode.Impulse);
            }
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
            _carrierCarryPoint = null;

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
            }
            else
            {
                // Restore physics when released
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                    rb.isKinematic = false;

                var col = GetComponent<CapsuleCollider>();
                if (col != null)
                    col.enabled = true;
            }
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
                _carrierCarryPoint = carrierPC.carryPoint;
        }

        private void LateUpdate()
        {
            // Carrier: position carried player at carry point (visual for all clients)
            if (_carriedTransform != null && carryPoint != null)
            {
                _carriedTransform.position = carryPoint.position;
                _carriedTransform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }

            // Being carried + owner: snap to carrier's carry point (authoritative via NetworkTransform)
            if (IsOwner && _carrierCarryPoint != null)
            {
                transform.position = _carrierCarryPoint.position;
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

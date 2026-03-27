#nullable enable
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Allows the player to pick up, carry, and deposit small demolished fragments.
    /// Mutually exclusive with tool equipping — drop your tool first to carry debris.
    /// The carried fragment is positioned at the hold point each frame. On clients,
    /// the NetworkVariable syncs which fragment is held, and LateUpdate positions the
    /// local copy at this player's hold point.
    /// </summary>
    public class FragmentCarrier : NetworkBehaviour
    {
        [SerializeField] private Transform? holdPoint;
        [SerializeField] private float maxPickupRange = 3f;

        // Server-side: tracks which fragments are currently carried by ANY player
        private static readonly HashSet<int> _claimedFragments = new();

        private readonly NetworkVariable<int> _carriedFragmentId = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Transform? _carriedTransform;
        private Collider? _carriedCollider;

        public bool IsCarrying => _carriedFragmentId.Value >= 0;
        public static bool IsClaimed(int fragmentId) => _claimedFragments.Contains(fragmentId);

        private void Awake()
        {
            if (holdPoint == null)
                holdPoint = transform.Find("Main Camera/HoldPoint");

            var handler = GetComponent<EquipmentHandler>();
            if (handler != null)
                handler.SetFragmentCarrier(this);
        }

        public override void OnNetworkSpawn()
        {
            _carriedFragmentId.OnValueChanged += OnCarriedChanged;

            if (_carriedFragmentId.Value >= 0)
                ResolveCarriedFragment(_carriedFragmentId.Value);
        }

        public override void OnNetworkDespawn()
        {
            // Server: force-drop carried fragment so it isn't orphaned on disconnect
            if (IsServer)
                DropFragment();

            _carriedFragmentId.OnValueChanged -= OnCarriedChanged;
        }

        public void RequestPickup(int fragmentId)
        {
            if (!IsOwner) return;
            PickupServerRpc(fragmentId);
        }

        public void RequestDrop()
        {
            if (!IsOwner) return;
            DropServerRpc();
        }

        public void RequestDeposit()
        {
            if (!IsOwner) return;
            DepositServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void PickupServerRpc(int fragmentId, RpcParams rpcParams = default)
        {
            if (_carriedFragmentId.Value >= 0) return;

            // Prevent two players from carrying the same fragment
            if (_claimedFragments.Contains(fragmentId)) return;

            var manager = DestructionNetworkManager.Instance;
            if (manager == null) return;

            if (!manager.Registry.TryGet(fragmentId, out var entry))
                return;
            if (entry.Transform == null) return;

            float dist = Vector3.Distance(transform.position, entry.Transform.position);
            if (dist > maxPickupRange) return;

            var meshRenderer = entry.Transform.GetComponent<Renderer>();
            if (meshRenderer != null)
            {
                var size = meshRenderer.bounds.size;
                if (Mathf.Max(size.x, size.y, size.z) > CarryableDebris.MaxCarryableExtent)
                    return;
            }

            if (entry.Rigidbody != null)
                entry.Rigidbody.isKinematic = true;

            var col = entry.Transform.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            _claimedFragments.Add(fragmentId);
            manager.Broadcaster?.AddForceSync(fragmentId);
            _carriedFragmentId.Value = fragmentId;
        }

        [Rpc(SendTo.Server)]
        private void DropServerRpc()
        {
            DropFragment();
        }

        [Rpc(SendTo.Server)]
        private void DepositServerRpc()
        {
            if (_carriedFragmentId.Value < 0) return;

            int fragId = _carriedFragmentId.Value;
            _claimedFragments.Remove(fragId);
            _carriedFragmentId.Value = -1;

            var manager = DestructionNetworkManager.Instance;
            if (manager == null) return;

            manager.Broadcaster?.RemoveForceSync(fragId);

            if (manager.Registry.TryGet(fragId, out var entry) && entry.Transform != null)
            {
                // Award currency based on fragment volume before destroying it
                var currencyMgr = CurrencyManager.Instance;
                if (currencyMgr != null)
                {
                    var renderer = entry.Transform.GetComponent<Renderer>();
                    int value = currencyMgr.CalculateFragmentValue(renderer);
                    currencyMgr.AwardCurrency(value);
                }

                Destroy(entry.Transform.gameObject);
            }

            manager.Registry.Unregister(fragId);
            manager.DestroyFragmentOnClientsRpc(fragId);
        }

        private void DropFragment()
        {
            if (_carriedFragmentId.Value < 0) return;

            int fragId = _carriedFragmentId.Value;
            _claimedFragments.Remove(fragId);

            var manager = DestructionNetworkManager.Instance;
            if (manager == null)
            {
                _carriedFragmentId.Value = -1;
                return;
            }

            manager.Broadcaster?.RemoveForceSync(fragId);

            if (manager.Registry.TryGet(fragId, out var entry) && entry.Transform != null)
            {
                entry.Transform.position = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;

                if (entry.Rigidbody != null)
                    entry.Rigidbody.isKinematic = false;

                var col = entry.Transform.GetComponent<Collider>();
                if (col != null)
                    col.enabled = true;
            }

            _carriedFragmentId.Value = -1;
        }

        private void OnCarriedChanged(int oldId, int newId)
        {
            if (_carriedCollider != null)
            {
                _carriedCollider.enabled = true;
                _carriedCollider = null;
            }
            _carriedTransform = null;

            if (newId >= 0)
                ResolveCarriedFragment(newId);
        }

        private void ResolveCarriedFragment(int fragmentId)
        {
            var manager = DestructionNetworkManager.Instance;
            if (manager == null) return;

            if (!manager.Registry.TryGet(fragmentId, out var entry) || entry.Transform == null)
                return;

            _carriedTransform = entry.Transform;
            _carriedCollider = entry.Transform.GetComponent<Collider>();

            if (_carriedCollider != null)
                _carriedCollider.enabled = false;

            if (entry.Rigidbody != null)
                entry.Rigidbody.isKinematic = true;
        }

        private void LateUpdate()
        {
            if (_carriedTransform == null || holdPoint == null) return;
            _carriedTransform.position = holdPoint.position;
            _carriedTransform.rotation = holdPoint.rotation;
        }
    }
}

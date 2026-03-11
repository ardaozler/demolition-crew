using RayFire;
using Unity.Netcode;
using UnityEngine;
using InteractionSystem.Tools;

namespace InteractionSystem
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class Bomb : NetworkBehaviour, IUsable, IInteractable
    {
        [SerializeField] private BombSettings settings;
        [SerializeField] private GameObject detonatorPrefab;

        private Rigidbody rb;
        private NetworkObject activeDetonatorNet;
        private EquipmentHandler detonatorHolder;
        private bool detonated;

        private NetworkVariable<bool> thrown = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public string UsableName => "Bomb";
        public string InteractionPrompt => "Pick up Bomb";

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void OnEquip(GameObject equipOwner)
        {
            if (!IsServer) return;
            thrown.Value = false;

            // Player picked the bomb back up — destroy the active detonator
            if (activeDetonatorNet != null)
            {
                // Notify the detonator holder so their equipment state is cleaned up
                if (detonatorHolder != null)
                    detonatorHolder.ServerForceUnequip();

                activeDetonatorNet.Despawn();
                activeDetonatorNet = null;
                detonatorHolder = null;
            }
        }

        public void OnUnequip(GameObject equipOwner) { }

        public void OnUseStarted(GameObject equipOwner, Vector3 aimOrigin, Vector3 aimDirection)
        {
            if (!IsServer) return;
            if (thrown.Value) return;

            thrown.Value = true;

            var handler = equipOwner.GetComponent<EquipmentHandler>();
            if (handler == null) return;

            // Detach bomb from player
            handler.ServerForceUnequip();
            NetworkObject.TryRemoveParent();

            // Position and throw
            transform.position = aimOrigin + aimDirection * 0.5f;
            transform.rotation = Quaternion.identity;

            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(aimDirection * settings.ThrowForce, ForceMode.VelocityChange);

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            // Spawn a detonator and equip it on the player
            if (detonatorPrefab != null)
            {
                var detGo = Instantiate(detonatorPrefab);
                activeDetonatorNet = detGo.GetComponent<NetworkObject>();
                activeDetonatorNet.Spawn();

                var detonator = detGo.GetComponent<Detonator>();
                if (detonator != null)
                    detonator.SetBomb(this);

                detonatorHolder = handler;
                handler.ServerForceEquip(activeDetonatorNet);
            }

            ThrowRpc(aimOrigin + aimDirection * 0.5f, aimDirection);
        }

        public void OnUseStopped(GameObject equipOwner) { }

        public bool CanInteract(GameObject player)
        {
            if (!thrown.Value)
                return transform.parent == null || GetComponentInParent<NetworkObject>() == GetComponent<NetworkObject>();

            // Thrown bomb can be picked back up
            return true;
        }

        public void Interact(GameObject player) { }

        /// <summary>
        /// Called by the Detonator when the player triggers remote detonation.
        /// </summary>
        public void Detonate()
        {
            if (!IsServer) return;
            if (detonated) return;
            detonated = true;

            var bombGo = new GameObject("BombExplosion");
            bombGo.transform.position = transform.position;

            var rfBomb = bombGo.AddComponent<RayfireBomb>();
            rfBomb.range = settings.Range;
            rfBomb.strength = settings.Strength;
            rfBomb.chaos = settings.Chaos;
            rfBomb.variation = settings.Variation;
            rfBomb.affectInactive = true;
            rfBomb.affectKinematic = true;
            rfBomb.applyDamage = true;
            rfBomb.damageValue = settings.Damage;
            rfBomb.delay = 0f;

            rfBomb.Explode(0f);

            Object.Destroy(bombGo);

            // Send RPCs BEFORE despawning so clients receive them
            ExplodeRpc(transform.position);

            activeDetonatorNet = null;
            detonatorHolder = null;
            NetworkObject.Despawn();
        }

        /// <summary>
        /// Called when the detonator is dropped/unequipped without detonating.
        /// Clears references so the detonator doesn't get double-despawned.
        /// </summary>
        public void ClearDetonatorRef()
        {
            activeDetonatorNet = null;
            detonatorHolder = null;
        }

        [Rpc(SendTo.Everyone)]
        private void ThrowRpc(Vector3 position, Vector3 direction)
        {
            // TODO: throw sound / trail VFX
        }

        [Rpc(SendTo.Everyone)]
        private void ExplodeRpc(Vector3 position)
        {
            // TODO: explosion VFX / sound
        }
    }
}

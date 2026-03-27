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
        private NetworkObject _cachedNetObj;
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
            _cachedNetObj = GetComponent<NetworkObject>();
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

        }

        public void OnUseStopped(GameObject equipOwner) { }

        public bool CanInteract(GameObject player)
        {
            if (!thrown.Value)
                return transform.parent == null || GetComponentInParent<NetworkObject>() == _cachedNetObj;

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

            // RayfireBomb applied force to the original shards, but they were
            // kinematic (StabilizeHostShards) so the force had no effect. The
            // damage triggered demolition, creating dynamic fragments. Apply
            // explosion force directly to those new fragments.
            Vector3 center = transform.position;
            float range = settings.Range;
            float force = settings.Strength * 200f;
            var colliders = Physics.OverlapSphere(center, range);
            for (int i = 0; i < colliders.Length; i++)
            {
                var hitRb = colliders[i].attachedRigidbody;
                if (hitRb != null && !hitRb.isKinematic)
                    hitRb.AddExplosionForce(force, center, range, 0.3f, ForceMode.Impulse);
            }

            // Delay destroy to let RayfireBomb finish any deferred physics work
            Object.Destroy(bombGo, 0.1f);

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

        public override void OnNetworkDespawn()
        {
            // Scene-placed NetworkObjects are not destroyed by Despawn(), only
            // deactivated in NGO's tracking. Force-hide the GameObject so
            // late-joining clients don't see a ghost at the original position.
            gameObject.SetActive(false);
        }
    }
}

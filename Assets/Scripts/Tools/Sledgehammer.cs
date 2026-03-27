using Unity.Netcode;
using UnityEngine;
using InteractionSystem.Tools;

namespace InteractionSystem
{
    [RequireComponent(typeof(NetworkObject))]
    public class Sledgehammer : NetworkBehaviour, IUsable, IInteractable
    {
        [SerializeField] private SledgehammerSettings settings;
        [SerializeField] private LayerMask hitMask = ~0;

        private float lastSwingTime;
        private NetworkObject _cachedNetObj;

        public string UsableName => "Sledgehammer";

        public void OnEquip(GameObject equipOwner) { }

        public void OnUnequip(GameObject equipOwner) { }

        public void OnUseStarted(GameObject equipOwner, Vector3 aimOrigin, Vector3 aimDirection)
        {
            if (!IsServer) return;
            if (Time.time - lastSwingTime < settings.Cooldown) return;

            lastSwingTime = Time.time;

            if (Physics.Raycast(aimOrigin, aimDirection, out RaycastHit hit, settings.HitRange, hitMask))
            {
                var rigid = hit.collider.GetComponentInParent<RayFire.RayfireRigid>();
                if (rigid != null)
                    rigid.ApplyDamage(settings.Damage, hit.point, settings.DamageRadius);

                var rb = hit.collider.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    // Un-kinematic so the force actually moves the shard.
                    // Initial shards are kinematic from StabilizeHostShards.
                    if (rb.isKinematic)
                        rb.isKinematic = false;

                    Vector3 right = Vector3.Cross(Vector3.up, aimDirection).normalized;
                    Vector3 forceDir = (aimDirection + -right * settings.LateralBias).normalized;
                    rb.AddForceAtPosition(forceDir * settings.HitForce, hit.point, ForceMode.Impulse);
                }

            }
        }

        public void OnUseStopped(GameObject equipOwner) { }

        public string InteractionPrompt => "Pick up Sledgehammer";

        private void Awake()
        {
            _cachedNetObj = GetComponent<NetworkObject>();
        }

        public bool CanInteract(GameObject player)
        {
            return transform.parent == null || GetComponentInParent<NetworkObject>() == _cachedNetObj;
        }

        public void Interact(GameObject player) { }
    }
}

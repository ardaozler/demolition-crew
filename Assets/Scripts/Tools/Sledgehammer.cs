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
        private GameObject owner;

        public string UsableName => "Sledgehammer";

        public void OnEquip(GameObject equipOwner)
        {
            owner = equipOwner;
        }

        public void OnUnequip(GameObject equipOwner)
        {
            owner = null;
        }

        public void OnUseStarted(GameObject equipOwner, Vector3 aimOrigin, Vector3 aimDirection)
        {
            if (!IsServer) return;
            if (Time.time - lastSwingTime < settings.Cooldown) return;

            lastSwingTime = Time.time;
            SwingClientRpc();

            if (Physics.Raycast(aimOrigin, aimDirection, out RaycastHit hit, settings.HitRange, hitMask))
            {
                var rigid = hit.collider.GetComponentInParent<RayFire.RayfireRigid>();
                if (rigid != null)
                    rigid.ApplyDamage(settings.Damage, hit.point, settings.DamageRadius);

                var rb = hit.collider.GetComponentInParent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 right = Vector3.Cross(Vector3.up, aimDirection).normalized;
                    Vector3 forceDir = (aimDirection + -right * settings.LateralBias).normalized;
                    rb.AddForceAtPosition(forceDir * settings.HitForce, hit.point, ForceMode.Impulse);
                }

                HitClientRpc(hit.point, hit.normal);
            }
        }

        public void OnUseStopped(GameObject equipOwner) { }

        public string InteractionPrompt => "Pick up Sledgehammer";

        public bool CanInteract(GameObject player)
        {
            return transform.parent == null || GetComponentInParent<NetworkObject>() == GetComponent<NetworkObject>();
        }

        public void Interact(GameObject player) { }

        [Rpc(SendTo.Everyone)]
        private void SwingClientRpc()
        {
            // TODO: swing animation / sound
        }

        [Rpc(SendTo.Everyone)]
        private void HitClientRpc(Vector3 hitPoint, Vector3 hitNormal)
        {
            // TODO: hit VFX / impact sound
        }
    }
}

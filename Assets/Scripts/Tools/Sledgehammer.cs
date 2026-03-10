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

        // --- IUsable ---

        public string UsableName => "Sledgehammer";

        public void OnEquip(GameObject equipOwner)
        {
            owner = equipOwner;
        }

        public void OnUnequip(GameObject equipOwner)
        {
            owner = null;
        }

        public void OnUseStarted(GameObject equipOwner)
        {
            if (!IsServer) return;
            if (Time.time - lastSwingTime < settings.Cooldown) return;

            lastSwingTime = Time.time;

            // Notify all clients to play swing visual
            SwingClientRpc();

            // Server raycast from owner's camera/eyes
            Transform eyePoint = GetOwnerEyePoint(equipOwner);
            if (eyePoint == null) return;

            if (Physics.Raycast(eyePoint.position, eyePoint.forward, out RaycastHit hit, settings.HitRange, hitMask))
            {
                // Try RayFire destruction — apply damage directly on server
                var rigid = hit.collider.GetComponentInParent<RayFire.RayfireRigid>();
                if (rigid != null)
                {
                    rigid.ApplyDamage(settings.Damage, hit.point, settings.HitRange);
                }

                // Apply force to any rigidbody
                var rb = hit.collider.GetComponentInParent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddForceAtPosition(eyePoint.forward * settings.HitForce, hit.point, ForceMode.Impulse);
                }

                // Notify clients of hit location for effects
                HitClientRpc(hit.point, hit.normal);
            }
        }

        public void OnUseStopped(GameObject equipOwner)
        {
            // Sledgehammer is instant-use, nothing to do on release
        }

        // --- IInteractable ---

        public string InteractionPrompt => "Pick up Sledgehammer";

        public bool CanInteract(GameObject player)
        {
            // Can only pick up if not already parented (i.e., not held by someone)
            return transform.parent == null || GetComponentInParent<NetworkObject>() == GetComponent<NetworkObject>();
        }

        public void Interact(GameObject player)
        {
            // Handled by EquipmentHandler
        }

        // --- Client RPCs for visuals ---

        [Rpc(SendTo.Everyone)]
        private void SwingClientRpc()
        {
            // TODO: Play swing animation / sound
            // For now, a simple rotation punch to indicate swing
            Debug.Log($"[Sledgehammer] Swing by {(owner != null ? owner.name : "unknown")}");
        }

        [Rpc(SendTo.Everyone)]
        private void HitClientRpc(Vector3 hitPoint, Vector3 hitNormal)
        {
            // TODO: Spawn hit VFX / play impact sound at hitPoint
            Debug.Log($"[Sledgehammer] Hit at {hitPoint}");
        }

        // --- Helpers ---

        private Transform GetOwnerEyePoint(GameObject equipOwner)
        {
            // Look for camera in children (the FirstPersonCamera object)
            var cam = equipOwner.GetComponentInChildren<UnityEngine.Camera>(true);
            if (cam != null) return cam.transform;

            // Fallback to owner transform
            return equipOwner.transform;
        }
    }
}

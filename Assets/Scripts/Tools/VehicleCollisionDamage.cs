#nullable enable
using RayFire;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to heavy vehicles (excavators, wrecking balls, etc.) to let them
/// smash through buildings. On collision above a force threshold, un-kinematics
/// the shard and applies RayFire damage so destruction cascades properly.
/// Host-only: clients do not run physics on building shards.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleCollisionDamage : MonoBehaviour
{
    [SerializeField] private float damagePerHit = 30f;
    [SerializeField] private float damageRadius = 0.5f;
    [SerializeField] private float minImpactForce = 5f;

    private void OnCollisionEnter(Collision collision)
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        if (collision.impulse.magnitude < minImpactForce)
            return;

        var rigid = collision.collider.GetComponentInParent<RayfireRigid>();
        if (rigid == null)
            return;

        var rb = rigid.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic)
            rb.isKinematic = false;

        var contact = collision.GetContact(0);
        rigid.ApplyDamage(damagePerHit, contact.point, damageRadius);
    }
}

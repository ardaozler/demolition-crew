using System.Collections.Generic;
using RayFire;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-side damage processing. Validates client damage requests (rate limiting,
/// distance checks) and applies server-authoritative damage values to RayFire objects.
/// </summary>
public class DamageHandler
{
    private readonly FragmentRegistry _registry;
    private readonly Dictionary<ulong, float> _lastDamageTime = new();

    private readonly float _defaultDamage;
    private readonly float _defaultDamageRadius;
    private readonly float _maxDamageRange;
    private readonly float _cooldown;

    public DamageHandler(FragmentRegistry registry, float defaultDamage, float defaultDamageRadius,
        float maxDamageRange, float cooldown)
    {
        _registry = registry;
        _defaultDamage = defaultDamage;
        _defaultDamageRadius = defaultDamageRadius;
        _maxDamageRange = maxDamageRange;
        _cooldown = cooldown;
    }

    public void ProcessDamageRequest(int sceneObjectId, Vector3 hitPoint, ulong senderId,
        NetworkManager networkManager)
    {
        // Rate limit per client
        if (_lastDamageTime.TryGetValue(senderId, out float lastTime) &&
            Time.time - lastTime < _cooldown)
            return;
        _lastDamageTime[senderId] = Time.time;

        if (!_registry.TryGet(sceneObjectId, out var entry) || entry.Rigid == null)
            return;

        // Distance check — sender must be near the target
        if (networkManager.ConnectedClients.TryGetValue(senderId, out var client) &&
            client.PlayerObject != null)
        {
            float dist = Vector3.Distance(client.PlayerObject.transform.position, hitPoint);
            if (dist > _maxDamageRange) return;
        }

        // Inactive MeshRoot shard — activate before applying damage so the
        // shard becomes dynamic and the demolition gets replicated to clients.
        if (entry.Rigid.simTp == SimType.Inactive && entry.Rigid.meshRoot != null)
            entry.Rigid.Activate();

        if (entry.Rigidbody != null && entry.Rigidbody.isKinematic)
            entry.Rigidbody.isKinematic = false;

        // Server determines damage — never trust client values
        entry.Rigid.ApplyDamage(_defaultDamage, hitPoint, _defaultDamageRadius);
    }

    public void RemoveClient(ulong clientId)
    {
        _lastDamageTime.Remove(clientId);
    }
}

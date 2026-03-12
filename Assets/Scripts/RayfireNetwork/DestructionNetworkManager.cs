using System;
using System.Collections.Generic;
using RayFire;
using Unity.Netcode;
using UnityEngine;

public class DestructionNetworkManager : NetworkBehaviour
{
    public static DestructionNetworkManager Instance { get; private set; }

    [Header("Sync Settings")]
    [SerializeField] private float interpolationSpeed = 20f;
    [SerializeField] private int broadcastEveryNthFixedFrame = 3;

    [Header("Cleanup")]
    [SerializeField] private float purgeIntervalSeconds = 5f;

    [Header("Damage Settings")]
    [SerializeField] private float defaultDamage = 10f;
    [SerializeField] private float defaultDamageRadius = 1f;
    [SerializeField] private float maxDamageRange = 10f;
    [SerializeField] private float damageRpcCooldown = 0.05f;

    private FragmentRegistry _registry;
    private DemolitionReplicator _replicator;
    private TransformSyncBroadcaster _broadcaster;
    private DamageHandler _damageHandler;
    private int _fixedFrameCounter;
    private float _purgeTimer;
    private bool _initialized;

    private readonly HashSet<ulong> _hydratedClients = new();

    public FragmentRegistry Registry => _registry;
    public DemolitionReplicator Replicator => _replicator;
    public TransformSyncBroadcaster Broadcaster => _broadcaster;

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Initialize subsystems BEFORE setting Instance so any code that
        // accesses DestructionNetworkManager.Instance sees a fully initialized object.
        _registry = new FragmentRegistry();
        _registry.RegisterSceneObjects();

        _replicator = new DemolitionReplicator(_registry, IsServer);
        _replicator.SubscribeToEvents();

        _broadcaster = new TransformSyncBroadcaster(_registry, interpolationSpeed);
        _damageHandler = new DamageHandler(_registry, defaultDamage, defaultDamageRadius,
            maxDamageRange, damageRpcCooldown);

        if (!IsServer)
            DisableClientPhysics();

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnect;

        _initialized = true;
        Instance = this;

        // Late-joining client requests state hydration from the host
        if (!IsServer)
            RequestHydrationRpc();
    }

    public override void OnNetworkDespawn()
    {
        _replicator?.UnsubscribeFromEvents();

        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnect;

        if (Instance == this) Instance = null;
    }

    private void OnClientDisconnect(ulong clientId)
    {
        _damageHandler.RemoveClient(clientId);
        _hydratedClients.Remove(clientId);
    }

    private void FixedUpdate()
    {
        if (!_initialized || !IsServer || !IsSpawned) return;

        try
        {
            FlushDemolitions();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        _fixedFrameCounter++;
        if (_fixedFrameCounter >= broadcastEveryNthFixedFrame)
        {
            _fixedFrameCounter = 0;
            try
            {
                BroadcastTransforms();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    private void Update()
    {
        if (!_initialized || !IsSpawned) return;

        if (!IsServer)
        {
            try
            {
                _broadcaster.InterpolateClient(Time.deltaTime);
                _replicator.FlushKinematicEnforcement();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Periodic cleanup of destroyed fragments
        _purgeTimer += Time.deltaTime;
        if (_purgeTimer >= purgeIntervalSeconds)
        {
            _purgeTimer = 0f;
            _registry.PurgeDestroyed();
            _broadcaster.PurgeStale();
        }
    }

    // ----- Host: flush queued demolitions to clients -----

    private void BroadcastTransforms()
    {
        var snapshots = _broadcaster.CaptureHostState();
        if (snapshots.Length > 0)
        {
            SyncTransformsRpc(snapshots);
        }
    }

    private void FlushDemolitions()
    {
        while (_replicator.TryDequeue(out var record))
        {
            DemolishObjectRpc(
                record.SceneObjectId,
                record.HitPoint,
                record.ObjectPosition,
                record.ObjectRotation,
                record.FragmentBaseId,
                record.FragmentCount,
                record.FragmentPositions);
        }
    }

    // ----- RPCs: Client -> Server -----

    [Rpc(SendTo.Server)]
    public void RequestDamageRpc(int sceneObjectId, Vector3 hitPoint, RpcParams rpcParams = default)
    {
        try
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            _damageHandler.ProcessDamageRequest(sceneObjectId, hitPoint, senderId, NetworkManager);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // ----- RPCs: Server -> Clients -----

    [Rpc(SendTo.NotServer)]
    private void DemolishObjectRpc(int sceneObjectId, Vector3 hitPoint,
        Vector3 objectPosition, Quaternion objectRotation,
        int fragBaseId, int fragCount, FragmentSnapshot[] hostFragPositions)
    {
        try
        {
            _replicator.ExecuteOnClient(sceneObjectId, hitPoint, objectPosition, objectRotation,
                fragBaseId, fragCount, hostFragPositions);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [Rpc(SendTo.NotServer)]
    private void SyncTransformsRpc(FragmentSnapshot[] snapshots)
    {
        try
        {
            _broadcaster.ReceiveSnapshots(snapshots);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // ----- Late-join hydration -----

    [Rpc(SendTo.Server)]
    private void RequestHydrationRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // One-shot: ignore duplicate hydration requests
        if (!_hydratedClients.Add(clientId)) return;

        try
        {
            var history = _replicator.DemolitionHistory;

            if (history.Count == 0) return;

            Debug.Log($"[DestructionNetworkManager] Sending {history.Count} demolition records to client {clientId}.");

            for (int i = 0; i < history.Count; i++)
            {
                var record = history[i];
                HydrateClientRpc(
                    record.SceneObjectId,
                    record.HitPoint,
                    record.ObjectPosition,
                    record.ObjectRotation,
                    record.FragmentBaseId,
                    record.FragmentCount,
                    record.FragmentPositions,
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
            }

            // Send current transform state so fragments appear at their resting positions,
            // not at their mid-air demolition-time positions.
            var snapshots = _broadcaster.CaptureFullState();
            if (snapshots.Length > 0)
            {
                HydrateTransformsRpc(snapshots, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void HydrateTransformsRpc(FragmentSnapshot[] snapshots, RpcParams rpcParams = default)
    {
        try
        {
            _broadcaster.ReceiveSnapshots(snapshots);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void HydrateClientRpc(int sceneObjectId, Vector3 hitPoint,
        Vector3 objectPosition, Quaternion objectRotation,
        int fragBaseId, int fragCount, FragmentSnapshot[] hostFragPositions,
        RpcParams rpcParams = default)
    {
        try
        {
            _replicator.ExecuteOnClient(sceneObjectId, hitPoint, objectPosition, objectRotation,
                fragBaseId, fragCount, hostFragPositions);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // ----- Client initialization -----

    private void DisableClientPhysics()
    {
        // Disable registered shards
        foreach (var kvp in _registry.All)
        {
            var frag = kvp.Value;
            if (frag.Rigid != null)
                frag.Rigid.dmlTp = DemolitionType.None;
            if (frag.Rigidbody != null)
            {
                frag.Rigidbody.isKinematic = true;
                frag.Rigidbody.interpolation = RigidbodyInterpolation.None;
            }
        }

        // Also disable MeshRoot objects (not in registry) so RayFire's internal
        // collision/activation can't fire on clients and cause desync.
        var allRigids = FindObjectsByType<RayfireRigid>(FindObjectsSortMode.None);
        foreach (var rigid in allRigids)
        {
            if (rigid.objTp == ObjectType.MeshRoot)
            {
                rigid.dmlTp = DemolitionType.None;
                rigid.simTp = SimType.Kinematic;
            }
        }
    }

    // ----- Public API -----

    public bool TryGetSceneId(RayfireRigid rigid, out int id) => _registry.TryGetId(rigid, out id);
}

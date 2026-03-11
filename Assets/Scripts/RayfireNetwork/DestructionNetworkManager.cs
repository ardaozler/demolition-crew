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

    private FragmentRegistry _registry;
    private DemolitionReplicator _replicator;
    private TransformSyncBroadcaster _broadcaster;
    private int _fixedFrameCounter;
    private float _purgeTimer;
    private bool _initialized;

    public FragmentRegistry Registry => _registry;

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _registry = new FragmentRegistry();
        _registry.RegisterSceneObjects();

        _replicator = new DemolitionReplicator(_registry, IsServer);
        _replicator.SubscribeToEvents();

        _broadcaster = new TransformSyncBroadcaster(_registry, interpolationSpeed);

        if (!IsServer)
            DisableClientPhysics();

        _initialized = true;
    }

    public override void OnNetworkDespawn()
    {
        _replicator?.UnsubscribeFromEvents();
        if (Instance == this) Instance = null;
    }

    private void FixedUpdate()
    {
        if (!_initialized || !IsServer) return;

        FlushDemolitions();

        _fixedFrameCounter++;
        if (_fixedFrameCounter >= broadcastEveryNthFixedFrame)
        {
            _fixedFrameCounter = 0;
            BroadcastTransforms();
        }
    }

    private void Update()
    {
        if (!_initialized) return;

        if (!IsServer)
            _broadcaster.InterpolateClient(Time.deltaTime);

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

    [Header("Damage Settings")]
    [SerializeField] private float defaultDamage = 10f;
    [SerializeField] private float defaultDamageRadius = 1f;
    [SerializeField] private float maxDamageRange = 10f;
    [SerializeField] private float damageRpcCooldown = 0.05f;

    private readonly Dictionary<ulong, float> _lastDamageTime = new();

    [Rpc(SendTo.Server)]
    public void RequestDamageRpc(int sceneObjectId, Vector3 hitPoint, RpcParams rpcParams = default)
    {
        // Rate limit per client
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (_lastDamageTime.TryGetValue(senderId, out float lastTime) &&
            Time.time - lastTime < damageRpcCooldown)
            return;
        _lastDamageTime[senderId] = Time.time;

        if (!_registry.TryGet(sceneObjectId, out var entry) || entry.Rigid == null)
            return;

        // Distance check — sender must be near the target
        if (NetworkManager.ConnectedClients.TryGetValue(senderId, out var client) &&
            client.PlayerObject != null)
        {
            float dist = Vector3.Distance(client.PlayerObject.transform.position, hitPoint);
            if (dist > maxDamageRange) return;
        }

        // Inactive MeshRoot shard — activate directly
        if (entry.Rigid.simTp == SimType.Inactive && entry.Rigid.meshRoot != null)
        {
            entry.Rigid.Activate();
            return;
        }

        if (entry.Rigidbody != null && entry.Rigidbody.isKinematic)
            entry.Rigidbody.isKinematic = false;

        // Server determines damage — never trust client values
        entry.Rigid.ApplyDamage(defaultDamage, hitPoint, defaultDamageRadius);
    }

    // ----- RPCs: Server -> Clients -----

    [Rpc(SendTo.NotServer)]
    private void DemolishObjectRpc(int sceneObjectId, Vector3 hitPoint,
        Vector3 objectPosition, Quaternion objectRotation,
        int fragBaseId, int fragCount, FragmentSnapshot[] hostFragPositions)
    {
        _replicator.ExecuteOnClient(sceneObjectId, hitPoint, objectPosition, objectRotation,
            fragBaseId, fragCount, hostFragPositions);
    }

    [Rpc(SendTo.NotServer)]
    private void SyncTransformsRpc(FragmentSnapshot[] snapshots)
    {
        _broadcaster.ReceiveSnapshots(snapshots);
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

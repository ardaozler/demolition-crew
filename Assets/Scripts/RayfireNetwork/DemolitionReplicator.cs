using System.Collections.Generic;
using InteractionSystem;
using RayFire;
using Unity.Netcode;
using UnityEngine;

public class DemolitionReplicator
{
    public struct DemolitionRecord
    {
        public int SceneObjectId;
        public int FragmentBaseId;
        public Vector3 HitPoint;
        public int FragmentCount;
        public Vector3 ObjectPosition;
        public Quaternion ObjectRotation;
        public FragmentSnapshot[] FragmentPositions;
    }

    private const int MaxHistorySize = 512;

    private readonly FragmentRegistry _registry;
    private readonly Queue<DemolitionRecord> _pendingRecords = new();
    private readonly List<DemolitionRecord> _demolitionHistory = new();
    private readonly bool _isHost;
    private bool _needsKinematicEnforcement;

    public IReadOnlyList<DemolitionRecord> DemolitionHistory => _demolitionHistory;

    public DemolitionReplicator(FragmentRegistry registry, bool isHost)
    {
        _registry = registry;
        _isHost = isHost;
    }

    public void SubscribeToEvents()
    {
        if (_isHost)
            RFDemolitionEvent.GlobalEvent += OnHostDemolition;
    }

    public void UnsubscribeFromEvents()
    {
        if (_isHost)
            RFDemolitionEvent.GlobalEvent -= OnHostDemolition;
    }

    public bool TryDequeue(out DemolitionRecord record) => _pendingRecords.TryDequeue(out record);

    private void OnHostDemolition(RayfireRigid demolished)
    {
        if (!_registry.TryGetId(demolished, out int sceneId))
        {
            Debug.LogWarning($"[DemolitionReplicator] Demolished object '{demolished.name}' not in registry.");
            return;
        }

        int fragmentCount = demolished.HasFragments ? demolished.fragments.Count : 0;
        int fragBaseId = fragmentCount > 0 ? _registry.AllocateBlock(fragmentCount) : 0;

        // Register fragments and capture their positions so clients can match by proximity
        var fragPositions = new FragmentSnapshot[fragmentCount];
        for (int i = 0; i < fragmentCount; i++)
        {
            var frag = demolished.fragments[i];
            int fragId = fragBaseId + i;
            _registry.Register(fragId, frag);
            fragPositions[i] = new FragmentSnapshot(fragId, frag.transform.position, frag.transform.rotation);
            CarryableDebris.TryAdd(frag.gameObject, fragId);
        }

        _registry.Unregister(sceneId);

        var record = new DemolitionRecord
        {
            SceneObjectId = sceneId,
            FragmentBaseId = fragBaseId,
            FragmentCount = fragmentCount,
            HitPoint = demolished.lim.contactVector3,
            ObjectPosition = demolished.transform.position,
            ObjectRotation = demolished.transform.rotation,
            FragmentPositions = fragPositions,
        };

        _pendingRecords.Enqueue(record);
        _demolitionHistory.Add(record);

        // Cap history to prevent unbounded memory growth
        if (_demolitionHistory.Count > MaxHistorySize)
            _demolitionHistory.RemoveAt(0);
    }

    public void ExecuteOnClient(int sceneObjectId, Vector3 hitPoint,
        Vector3 objectPosition, Quaternion objectRotation,
        int fragBaseId, int fragCount, FragmentSnapshot[] hostFragPositions)
    {
        if (!_registry.TryGet(sceneObjectId, out var entry))
        {
            Debug.LogWarning(
                $"[DemolitionReplicator] Received demolition for unknown object ID {sceneObjectId}.");
            return;
        }

        var rigid = entry.Rigid;

        // Sub-fragments registered via RegisterTransformOnly have Rigid = null.
        // Recover the component from the GameObject so we can demolish again.
        if (rigid == null && entry.Transform != null)
        {
            rigid = entry.Transform.GetComponent<RayfireRigid>();
            if (rigid == null)
            {
                Debug.LogWarning(
                    $"[DemolitionReplicator] No RayfireRigid on object ID {sceneObjectId}.");
                return;
            }
        }

        // Teleport the client object to match the host's position at demolition
        // time. The object may have moved on the host (e.g. from hit impulses)
        // while the client copy stayed kinematic at its original position.
        rigid.transform.position = objectPosition;
        rigid.transform.rotation = objectRotation;

        // DisableClientPhysics sets dmlTp = None on all client objects.
        // Restore it so DemolishForced actually produces fragments.
        rigid.dmlTp = DemolitionType.Runtime;

        rigid.lim.contactVector3 = hitPoint;
        rigid.lim.demolitionShould = true;

        rigid.DemolishForced();

        _registry.Unregister(sceneObjectId);

        if (rigid.HasFragments && hostFragPositions != null && hostFragPositions.Length > 0)
        {
            if (rigid.fragments.Count != hostFragPositions.Length)
            {
                Debug.LogWarning(
                    $"[DemolitionReplicator] Fragment count mismatch for object {sceneObjectId}: " +
                    $"host={hostFragPositions.Length}, client={rigid.fragments.Count}");
            }

            RegisterClientFragments(rigid.fragments, hostFragPositions);
        }

        // Flag that kinematic state needs re-enforcement after this frame's
        // demolitions are done. The manager batches this into a single pass.
        _needsKinematicEnforcement = true;
    }

    /// <summary>
    /// If any demolitions ran this frame, re-enforce kinematic state on all
    /// registered fragments. Called once per frame by the manager, not per-demolition.
    /// </summary>
    public void FlushKinematicEnforcement()
    {
        if (!_needsKinematicEnforcement) return;
        _needsKinematicEnforcement = false;

        foreach (var kvp in _registry.All)
        {
            var frag = kvp.Value;
            if (frag.Rigidbody != null && !frag.Rigidbody.isKinematic)
            {
                frag.Rigidbody.isKinematic = true;
                frag.Rigidbody.interpolation = RigidbodyInterpolation.None;
            }
        }
    }
    

    /// <summary>
    /// Match client fragments to host fragment IDs, register them, and make them kinematic.
    /// When counts match, uses index-based matching (RayFire produces deterministic fragment
    /// order for the same mesh when position/rotation match). Falls back to greedy
    /// nearest-position matching when counts differ.
    /// </summary>
    private void RegisterClientFragments(List<RayfireRigid> clientFragments, FragmentSnapshot[] hostPositions)
    {
        if (clientFragments.Count == hostPositions.Length)
        {
            // Counts match — use direct index mapping (deterministic RayFire output)
            for (int i = 0; i < hostPositions.Length; i++)
                RegisterAndSnapFragment(clientFragments[i], hostPositions[i]);
        }
        else
        {
            // Counts differ — fall back to greedy nearest-position matching
            RegisterClientFragmentsProximity(clientFragments, hostPositions);
        }
    }

    private void RegisterClientFragmentsProximity(List<RayfireRigid> clientFragments, FragmentSnapshot[] hostPositions)
    {
        var used = new bool[clientFragments.Count];

        for (int h = 0; h < hostPositions.Length; h++)
        {
            var hostSnap = hostPositions[h];
            float bestDist = float.MaxValue;
            int bestIdx = -1;

            for (int c = 0; c < clientFragments.Count; c++)
            {
                if (used[c]) continue;
                float dist = (clientFragments[c].transform.position - hostSnap.Position).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = c;
                }
            }

            if (bestIdx >= 0)
            {
                used[bestIdx] = true;
                RegisterAndSnapFragment(clientFragments[bestIdx], hostPositions[h]);
            }
        }

        // Destroy unmatched client fragments — they have no host counterpart
        for (int c = 0; c < clientFragments.Count; c++)
        {
            if (!used[c])
                Object.Destroy(clientFragments[c].gameObject);
        }
    }

    private void RegisterAndSnapFragment(RayfireRigid clientFrag, FragmentSnapshot hostSnap)
    {
        clientFrag.dmlTp = DemolitionType.None;
        var rb = clientFrag.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        // Snap to host position immediately so there's no visual gap
        // until the next transform sync arrives
        clientFrag.transform.position = hostSnap.Position;
        clientFrag.transform.rotation = hostSnap.Rotation;

        _registry.RegisterTransformOnly(hostSnap.Id, clientFrag.transform, rb);
        CarryableDebris.TryAdd(clientFrag.gameObject, hostSnap.Id);
    }
}

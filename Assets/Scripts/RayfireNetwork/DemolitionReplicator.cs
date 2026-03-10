using System.Collections.Generic;
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
        public FragmentSnapshot[] FragmentPositions;
    }

    private readonly FragmentRegistry _registry;
    private readonly Queue<DemolitionRecord> _pendingRecords = new();
    private readonly bool _isHost;

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
        }

        _registry.Unregister(sceneId);

        var record = new DemolitionRecord
        {
            SceneObjectId = sceneId,
            FragmentBaseId = fragBaseId,
            FragmentCount = fragmentCount,
            HitPoint = demolished.lim.contactVector3,
            FragmentPositions = fragPositions,
        };

        _pendingRecords.Enqueue(record);
    }

    public void ExecuteOnClient(int sceneObjectId, Vector3 hitPoint, int fragBaseId,
        int fragCount, FragmentSnapshot[] hostFragPositions)
    {
        if (!_registry.TryGet(sceneObjectId, out var entry) || entry.Rigid == null)
        {
            Debug.LogWarning(
                $"[DemolitionReplicator] Received demolition for unknown object ID {sceneObjectId}.");
            return;
        }

        var rigid = entry.Rigid;
        rigid.lim.contactVector3 = hitPoint;
        rigid.lim.demolitionShould = true;

        rigid.DemolishForced();

        _registry.Unregister(sceneObjectId);

        // Register client fragments matched to host fragment IDs by nearest position
        if (rigid.HasFragments && hostFragPositions != null && hostFragPositions.Length > 0)
        {
            RegisterClientFragments(rigid.fragments, hostFragPositions);
        }
    }

    /// <summary>
    /// Match client fragments to host fragment IDs using nearest-position greedy matching,
    /// then register them and make them kinematic (clients don't simulate physics).
    /// </summary>
    private void RegisterClientFragments(List<RayfireRigid> clientFragments, FragmentSnapshot[] hostPositions)
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
                var clientFrag = clientFragments[bestIdx];

                // Disable local physics — host drives these via transform sync
                clientFrag.dmlTp = DemolitionType.None;
                var rb = clientFrag.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                }

                _registry.RegisterTransformOnly(hostSnap.Id, clientFrag.transform, rb);
            }
        }
    }
}

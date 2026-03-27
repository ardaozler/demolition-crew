using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every N fixed frames, gathers transform snapshots from the FragmentRegistry and broadcasts them to clients.
/// Only sends fragments that have moved beyond a threshold (delta compression).
/// Does NOT skip sleeping rigidbodies — they may have settled at new positions since last broadcast.
///
/// Client-side: fragments are dynamic (players can push them). Host corrections use
/// MovePosition/MoveRotation which smoothly moves dynamic bodies while respecting physics.
/// Large desync snaps instantly. Small differences converge over a few frames.
/// </summary>
public class TransformSyncBroadcaster
{
    private const float PositionThresholdSqr = 0.0025f;   // 0.05 units — delta compress threshold
    private const float RotationThreshold = 1f;            // 1 degree
    private const float SnapDistSqr = 9f;                 // > 3m — teleport instantly
    private const float ConvergenceThresholdSqr = 0.001f;  // stop interpolating when close

    private readonly FragmentRegistry _registry;
    private readonly float _interpSpeed;

    // Client-side interpolation: flat list for GC-free iteration
    private readonly List<int> _activeTargetIds = new();
    private readonly HashSet<int> _activeTargetIdSet = new();
    private readonly Dictionary<int, (Vector3 pos, Quaternion rot)> _targetMap = new();

    // Host-side delta compression
    private readonly Dictionary<int, (Vector3 pos, Quaternion rot)> _lastSent = new();

    // Fragments that must be synced even when kinematic (e.g. carried fragments)
    private readonly HashSet<int> _forceSyncIds = new();

    // Pre-allocated buffers reused to avoid GC
    private readonly List<FragmentSnapshot> _snapshotBuffer = new();
    private readonly List<int> _purgeBuffer = new();
    private FragmentSnapshot[] _snapshotArray = System.Array.Empty<FragmentSnapshot>();

    // Stats for debug display
    public int InterpolationTargetCount => _activeTargetIds.Count;
    public int LastBroadcastCount => _snapshotArray.Length;

    public TransformSyncBroadcaster(FragmentRegistry registry, float interpSpeed = 20f)
    {
        _registry = registry;
        _interpSpeed = interpSpeed;
    }

    public void AddForceSync(int id) => _forceSyncIds.Add(id);
    public void RemoveForceSync(int id) { _forceSyncIds.Remove(id); _lastSent.Remove(id); }

    public FragmentSnapshot[] CaptureHostState()
    {
        _snapshotBuffer.Clear();
        var all = _registry.All;

        foreach (var kvp in all)
        {
            var frag = kvp.Value;
            if (frag.Transform == null) continue;

            // Skip kinematic rigidbodies — they are pre-demolition scene shards.
            // Exception: fragments in the force-sync set (e.g. being carried).
            if (frag.Rigidbody != null && frag.Rigidbody.isKinematic && !_forceSyncIds.Contains(kvp.Key))
                continue;

            int id = kvp.Key;
            var pos = frag.Transform.position;
            var rot = frag.Transform.rotation;

            // Only send if moved since last broadcast
            if (_lastSent.TryGetValue(id, out var prev))
            {
                if ((pos - prev.pos).sqrMagnitude < PositionThresholdSqr &&
                    Quaternion.Angle(rot, prev.rot) < RotationThreshold)
                    continue;
            }

            _lastSent[id] = (pos, rot);
            _snapshotBuffer.Add(new FragmentSnapshot(id, pos, rot));
        }

        if (_snapshotBuffer.Count == 0)
            return System.Array.Empty<FragmentSnapshot>();

        int count = _snapshotBuffer.Count;
        if (_snapshotArray.Length != count)
            _snapshotArray = new FragmentSnapshot[count];

        for (int i = 0; i < count; i++)
            _snapshotArray[i] = _snapshotBuffer[i];

        return _snapshotArray;
    }

    /// <summary>
    /// Captures ALL registered non-kinematic fragment positions, ignoring delta compression.
    /// Used for late-join hydration so clients see current resting positions.
    /// </summary>
    public FragmentSnapshot[] CaptureFullState()
    {
        _snapshotBuffer.Clear();
        var all = _registry.All;

        foreach (var kvp in all)
        {
            var frag = kvp.Value;
            if (frag.Transform == null) continue;
            if (frag.Rigidbody != null && frag.Rigidbody.isKinematic && !_forceSyncIds.Contains(kvp.Key)) continue;

            _snapshotBuffer.Add(new FragmentSnapshot(kvp.Key, frag.Transform.position, frag.Transform.rotation));
        }

        if (_snapshotBuffer.Count == 0)
            return System.Array.Empty<FragmentSnapshot>();

        var result = new FragmentSnapshot[_snapshotBuffer.Count];
        for (int i = 0; i < _snapshotBuffer.Count; i++)
            result[i] = _snapshotBuffer[i];

        return result;
    }

    public void ReceiveSnapshots(FragmentSnapshot[] snapshots)
    {
        for (int i = 0; i < snapshots.Length; i++)
        {
            var pos = snapshots[i].Position;
            // Skip snapshots with NaN positions (corrupt data from half-precision)
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
                continue;

            int id = snapshots[i].Id;
            _targetMap[id] = (pos, snapshots[i].Rotation);

            if (_activeTargetIdSet.Add(id))
                _activeTargetIds.Add(id);

            // If this fragment was settled (kinematic) and the host says it
            // moved, wake it up so MovePosition can move it.
            if (_registry.TryGet(id, out var entry) &&
                entry.Rigidbody != null && entry.Rigidbody.isKinematic)
            {
                float moveDist = (entry.Transform.position - pos).sqrMagnitude;
                if (moveDist > PositionThresholdSqr)
                    entry.Rigidbody.isKinematic = false;
            }
        }
    }

    public void InterpolateClient(float deltaTime)
    {
        float t = 1f - Mathf.Exp(-_interpSpeed * deltaTime);

        for (int i = _activeTargetIds.Count - 1; i >= 0; i--)
        {
            int id = _activeTargetIds[i];

            if (!_registry.TryGet(id, out var entry) || entry.Transform == null)
            {
                SwapRemove(_activeTargetIds, i);
                _targetMap.Remove(id);
                continue;
            }

            if (!_targetMap.TryGetValue(id, out var target))
            {
                SwapRemove(_activeTargetIds, i);
                continue;
            }

            var curPos = entry.Transform.position;
            var curRot = entry.Transform.rotation;
            float distSqr = (curPos - target.pos).sqrMagnitude;

            Vector3 newPos;
            Quaternion newRot;

            if (distSqr > SnapDistSqr)
            {
                // Major desync — snap instantly
                newPos = target.pos;
                newRot = target.rot;
                if (entry.Rigidbody != null)
                {
                    entry.Rigidbody.linearVelocity = Vector3.zero;
                    entry.Rigidbody.angularVelocity = Vector3.zero;
                }
            }
            else
            {
                newPos = Vector3.Lerp(curPos, target.pos, t);
                newRot = Quaternion.Slerp(curRot, target.rot, t);
            }

            if (entry.Rigidbody != null)
            {
                entry.Rigidbody.MovePosition(newPos);
                entry.Rigidbody.MoveRotation(newRot);
            }
            else
            {
                entry.Transform.position = newPos;
                entry.Transform.rotation = newRot;
            }

            // Converged — settle the fragment to kinematic so gravity can't
            // pull it away from the host-authoritative resting position.
            // ReceiveSnapshots will wake it up if the host says it moved again.
            if ((newPos - target.pos).sqrMagnitude < ConvergenceThresholdSqr &&
                Quaternion.Angle(newRot, target.rot) < 0.5f)
            {
                if (entry.Rigidbody != null && !entry.Rigidbody.isKinematic)
                {
                    entry.Rigidbody.linearVelocity = Vector3.zero;
                    entry.Rigidbody.angularVelocity = Vector3.zero;
                    entry.Rigidbody.isKinematic = true;
                    entry.Transform.position = target.pos;
                    entry.Transform.rotation = target.rot;
                }

                SwapRemove(_activeTargetIds, i);
                _targetMap.Remove(id);
            }
        }
    }

    public void PurgeStale()
    {
        _purgeBuffer.Clear();

        for (int i = _activeTargetIds.Count - 1; i >= 0; i--)
        {
            int id = _activeTargetIds[i];
            if (!_registry.TryGet(id, out var entry) || entry.Transform == null)
            {
                SwapRemove(_activeTargetIds, i);
                _targetMap.Remove(id);
            }
        }

        _purgeBuffer.Clear();
        foreach (var kvp in _lastSent)
        {
            if (!_registry.TryGet(kvp.Key, out var entry) || entry.Transform == null)
                _purgeBuffer.Add(kvp.Key);
        }
        for (int i = 0; i < _purgeBuffer.Count; i++)
            _lastSent.Remove(_purgeBuffer[i]);
    }

    private void SwapRemove(List<int> list, int index)
    {
        int id = list[index];
        _activeTargetIdSet.Remove(id);
        int last = list.Count - 1;
        if (index < last)
            list[index] = list[last];
        list.RemoveAt(last);
    }
}

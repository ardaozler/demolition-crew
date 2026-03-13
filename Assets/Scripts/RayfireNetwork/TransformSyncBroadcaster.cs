using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every N fixed frames, gathers transform snapshots from the FragmentRegistry and broadcasts them to clients.
/// Only sends fragments that have moved beyond a threshold (delta compression).
/// Does NOT skip sleeping rigidbodies — they may have settled at new positions since last broadcast.
/// </summary>
public class TransformSyncBroadcaster
{
    private const float PositionThresholdSqr = 0.0025f; // 0.05 units
    private const float RotationThreshold = 1f;          // 1 degree
    private const float ConvergenceThresholdSqr = 0.0001f; // 0.01 units — close enough to stop interpolating

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

            // Skip kinematic rigidbodies — they are static scene objects.
            // Exception: fragments in the force-sync set (e.g. being carried).
            // NOTE: do NOT skip sleeping rigidbodies here. A fragment may have
            // settled at a new position since the last broadcast. The delta
            // check below already prevents re-sending unchanged positions.
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

        // Reuse array if size matches; only reallocate when count changes
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
            int id = snapshots[i].Id;
            var target = (snapshots[i].Position, snapshots[i].Rotation);
            _targetMap[id] = target;

            // Add to active list if not already tracked (O(1) check via HashSet)
            if (_activeTargetIdSet.Add(id))
                _activeTargetIds.Add(id);
        }
    }

    public void InterpolateClient(float deltaTime)
    {
        float t = 1f - Mathf.Exp(-_interpSpeed * deltaTime);

        // Iterate flat list backwards so we can swap-remove converged entries
        for (int i = _activeTargetIds.Count - 1; i >= 0; i--)
        {
            int id = _activeTargetIds[i];

            if (!_registry.TryGet(id, out var entry) || entry.Transform == null)
            {
                // Fragment gone — remove from active list
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

            var newPos = Vector3.Lerp(curPos, target.pos, t);
            var newRot = Quaternion.Slerp(curRot, target.rot, t);

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

            // If converged, stop interpolating until a new snapshot arrives
            if ((newPos - target.pos).sqrMagnitude < ConvergenceThresholdSqr &&
                Quaternion.Angle(newRot, target.rot) < 0.5f)
            {
                SwapRemove(_activeTargetIds, i);
                _targetMap.Remove(id);
            }
        }
    }

    /// <summary>
    /// Remove stale entries for fragments that no longer exist or have gone kinematic.
    /// </summary>
    public void PurgeStale()
    {
        _purgeBuffer.Clear();

        // Purge active targets (client-side)
        for (int i = _activeTargetIds.Count - 1; i >= 0; i--)
        {
            int id = _activeTargetIds[i];
            if (!_registry.TryGet(id, out var entry) || entry.Transform == null)
            {
                SwapRemove(_activeTargetIds, i);
                _targetMap.Remove(id);
            }
        }

        // Purge _lastSent for fragments no longer in the registry or now kinematic (host-side)
        _purgeBuffer.Clear();
        foreach (var kvp in _lastSent)
        {
            if (!_registry.TryGet(kvp.Key, out var entry) || entry.Transform == null)
                _purgeBuffer.Add(kvp.Key);
            else if (entry.Rigidbody != null && entry.Rigidbody.isKinematic && !_forceSyncIds.Contains(kvp.Key))
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

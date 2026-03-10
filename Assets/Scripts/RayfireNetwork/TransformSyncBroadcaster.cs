using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every N fixed frames, gathers transform snapshots from the FragmentRegistry and broadcasts them to clients.
/// Only sends fragments that have moved beyond a threshold (delta compression).
/// Skips sleeping rigidbodies to save bandwidth.
/// </summary>
public class TransformSyncBroadcaster
{
    private const float PositionThresholdSqr = 0.0025f; // 0.05 units
    private const float RotationThreshold = 1f;          // 1 degree

    private readonly FragmentRegistry _registry;
    private readonly float _interpSpeed;

    private readonly Dictionary<int, (Vector3 pos, Quaternion rot)> _targets = new();
    private readonly Dictionary<int, (Vector3 pos, Quaternion rot)> _lastSent = new();

    // Pre-allocated list reused each broadcast to avoid GC
    private readonly List<FragmentSnapshot> _snapshotBuffer = new();

    public TransformSyncBroadcaster(FragmentRegistry registry, float interpSpeed = 20f)
    {
        _registry = registry;
        _interpSpeed = interpSpeed;
    }

    public FragmentSnapshot[] CaptureHostState()
    {
        _snapshotBuffer.Clear();
        var all = _registry.All;

        foreach (var kvp in all)
        {
            var frag = kvp.Value;
            if (frag.Transform == null) continue;

            // Skip sleeping or kinematic rigidbodies — they aren't moving
            if (frag.Rigidbody != null && (frag.Rigidbody.IsSleeping() || frag.Rigidbody.isKinematic))
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

        return _snapshotBuffer.Count > 0 ? _snapshotBuffer.ToArray() : System.Array.Empty<FragmentSnapshot>();
    }

    public void ReceiveSnapshots(FragmentSnapshot[] snapshots)
    {
        for (int i = 0; i < snapshots.Length; i++)
        {
            _targets[snapshots[i].Id] = (snapshots[i].Position, snapshots[i].Rotation);
        }
    }

    public void InterpolateClient(float deltaTime)
    {
        float t = 1f - Mathf.Exp(-_interpSpeed * deltaTime);

        foreach (var kvp in _targets)
        {
            if (!_registry.TryGet(kvp.Key, out var entry) || entry.Transform == null)
                continue;

            var target = kvp.Value;

            var newPos = Vector3.Lerp(entry.Transform.position, target.pos, t);
            var newRot = Quaternion.Slerp(entry.Transform.rotation, target.rot, t);

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
        }
    }

    /// <summary>
    /// Remove stale entries for fragments that no longer exist.
    /// </summary>
    public void PurgeStale()
    {
        var toRemove = new List<int>();
        foreach (var kvp in _targets)
        {
            if (!_registry.TryGet(kvp.Key, out var entry) || entry.Transform == null)
                toRemove.Add(kvp.Key);
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            _targets.Remove(toRemove[i]);
            _lastSent.Remove(toRemove[i]);
        }
    }
}

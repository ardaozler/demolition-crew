using System;
using System.Collections.Generic;
using RayFire;
using UnityEngine;

/// <summary>
/// Deterministic ID assignment for all Rayfire Objects.
/// Sorts by hierarchy path to guarantee the same IDs on host and clients.
/// </summary>
public class FragmentRegistry
{
    public struct TrackedFragment
    {
        public Transform Transform;
        public Rigidbody Rigidbody;
        public RayfireRigid Rigid;
    }

    private readonly Dictionary<int, TrackedFragment> _fragments = new();
    private readonly Dictionary<RayfireRigid, int> _rigidToId = new();
    private readonly List<int> _purgeBuffer = new();
    private int _nextId;

    public IReadOnlyDictionary<int, TrackedFragment> All => _fragments;
    public int Count => _fragments.Count;

    public void RegisterSceneObjects()
    {
        var allRigids = UnityEngine.Object.FindObjectsByType<RayfireRigid>(FindObjectsSortMode.None);

        // Sort by hierarchy path for deterministic ordering across host and clients
        Array.Sort(allRigids, (a, b) =>
            string.Compare(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform), StringComparison.Ordinal));

        foreach (var rigid in allRigids)
        {
            //skip meshroots
            if (rigid.objTp == ObjectType.MeshRoot) continue;
            //skip non-demolishable objects without fragments
            if (rigid.dmlTp == DemolitionType.None && rigid.meshRoot == null) continue;

            int id = _nextId++;
            Register(id, rigid);
        }

        Debug.Log($"[FragmentRegistry] Registered {_nextId} scene objects.");
    }

    /// <summary>
    /// Reserve a contiguous block of IDs for new fragments. Returns the base ID.
    /// </summary>
    public int AllocateBlock(int count)
    {
        int baseId = _nextId;
        _nextId += count;
        return baseId;
    }

    public void Register(int id, RayfireRigid rigid)
    {
        var entry = new TrackedFragment
        {
            Transform = rigid.transform,
            Rigidbody = rigid.GetComponent<Rigidbody>(),
            Rigid = rigid
        };
        _fragments[id] = entry;
        _rigidToId[rigid] = id;
    }

    //only for clients to sync with the host, since after the client replays the demolition, the fragments will already exist and have no RayfireRigid component. The registry just needs to track their transforms and rigidbodies for kinematic enforcement.
    public void RegisterTransformOnly(int id, Transform tf, Rigidbody rb)
    {
        var entry = new TrackedFragment
        {
            Transform = tf,
            Rigidbody = rb,
            Rigid = null
        };
        _fragments[id] = entry;
    }

    public void Unregister(int id)
    {
        if (_fragments.TryGetValue(id, out var entry))
        {
            if (entry.Rigid != null)
                _rigidToId.Remove(entry.Rigid);
            _fragments.Remove(id);
        }
    }

    /// <summary>
    /// Remove entries whose Transform has been destroyed.
    /// Returns the number of entries removed.
    /// </summary>
    public int PurgeDestroyed()
    {
        _purgeBuffer.Clear();
        foreach (var kvp in _fragments)
        {
            if (kvp.Value.Transform == null)
                _purgeBuffer.Add(kvp.Key);
        }

        for (int i = 0; i < _purgeBuffer.Count; i++)
            Unregister(_purgeBuffer[i]);

        return _purgeBuffer.Count;
    }

    public bool TryGet(int id, out TrackedFragment entry) => _fragments.TryGetValue(id, out entry);
    public bool TryGetId(RayfireRigid rigid, out int id) => _rigidToId.TryGetValue(rigid, out id);

    private static string GetHierarchyPath(Transform tf)
    {
        string path = tf.name;
        while (tf.parent != null)
        {
            tf = tf.parent;
            path = tf.name + "/" + path;
        }
        return path;
    }
}

using System.Collections;
using RayFire;
using Unity.Netcode;
using UnityEngine;

public class RFBreaker : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float damageRadius = 1f;
    [SerializeField] private int poolDelayFrames = 2;

    [HideInInspector] public ProjectileLauncher launcher;

    private readonly ContactPoint[] _contactBuffer = new ContactPoint[1];
    private bool _hit;

    private void OnEnable()
    {
        _hit = false;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (_hit) return;

        if (!IsBreakable(other.gameObject))
            return;

        _hit = true;

        other.GetContacts(_contactBuffer);
        Vector3 hitPoint = _contactBuffer[0].point;

        bool networked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (networked)
            HandleNetworked(other.gameObject, hitPoint);
        else
            HandleLocal(other, hitPoint);

        StartCoroutine(ReturnToPoolDelayed());
    }

    private IEnumerator ReturnToPoolDelayed()
    {
        for (int i = 0; i < poolDelayFrames; i++)
            yield return new WaitForFixedUpdate();

        if (launcher != null)
            launcher.ReturnToPool(gameObject);
    }

    private void HandleNetworked(GameObject hitObject, Vector3 hitPoint)
    {
        var manager = DestructionNetworkManager.Instance;
        if (manager == null) return;

        RayfireRigid rigid = FindRegisteredRigid(manager, hitObject);
        if (rigid == null) return;

        if (!manager.TryGetSceneId(rigid, out int sceneId)) return;

        if (NetworkManager.Singleton.IsServer)
            ApplyDamageOrActivate(rigid, hitPoint);
        else
            manager.RequestDamageRpc(sceneId, hitPoint);
    }

    private void HandleLocal(Collision other, Vector3 hitPoint)
    {
        if (other.gameObject.TryGetComponent(out RayfireRigid rfRigid))
            ApplyDamageOrActivate(rfRigid, hitPoint);
    }

    private void ApplyDamageOrActivate(RayfireRigid rigid, Vector3 hitPoint)
    {
        if (rigid.simTp == SimType.Inactive && rigid.meshRoot != null)
        {
            rigid.Activate();
            return;
        }

        WakeUpIfKinematic(rigid);
        rigid.ApplyDamage(damage, hitPoint, damageRadius);
    }

    private static RayfireRigid FindRegisteredRigid(DestructionNetworkManager manager, GameObject hitObj)
    {
        Transform t = hitObj.transform;
        while (t != null)
        {
            if (t.TryGetComponent(out RayfireRigid rigid) && manager.TryGetSceneId(rigid, out _))
                return rigid;
            t = t.parent;
        }

        return null;
    }

    private static void WakeUpIfKinematic(RayfireRigid rigid)
    {
        var rb = rigid.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic)
            rb.isKinematic = false;
    }

    private static bool IsBreakable(GameObject go)
    {
        Transform t = go.transform;
        while (t != null)
        {
            if (t.CompareTag("Breakable")) return true;
            t = t.parent;
        }

        return false;
    }
}
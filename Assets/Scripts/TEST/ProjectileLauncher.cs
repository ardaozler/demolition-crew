using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProjectileLauncher : NetworkBehaviour
{
    public GameObject projectilePrefab;
    public float launchForce = 30f;
    public float projectileLifetime = 5f;
    public float fireRate = 0.1f;
    public int poolSize = 100;

    private float _lastFireTime;
    private Camera _mainCam;
    private Queue<GameObject> _pool;

    // Track active return coroutines so stale ones can be cancelled
    private readonly Dictionary<GameObject, Coroutine> _activeTimers = new();

    private void Awake()
    {
        _pool = new Queue<GameObject>(poolSize);
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(projectilePrefab);
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            _mainCam = Camera.main;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (_mainCam == null) return;

        if (_lastFireTime + fireRate < Time.time && (Mouse.current.leftButton.wasPressedThisFrame ||
                                                     Mouse.current.rightButton.isPressed))
        {
            _lastFireTime = Time.time;
            Ray ray = _mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());

            SpawnProjectile(ray.origin, ray.direction, damageEnabled: true);
            ShootRpc(ray.origin, ray.direction);
        }
    }

    [Rpc(SendTo.NotOwner)]
    private void ShootRpc(Vector3 origin, Vector3 direction)
    {
        SpawnProjectile(origin, direction, damageEnabled: false);
    }

    private void SpawnProjectile(Vector3 origin, Vector3 direction, bool damageEnabled)
    {
        GameObject projectile = GetFromPool();
        if (projectile == null) return;

        // Cancel any stale return timer from a previous use of this pooled object
        CancelTimer(projectile);

        projectile.transform.SetPositionAndRotation(origin, Quaternion.identity);

        var breaker = projectile.GetComponent<RFBreaker>();
        if (breaker != null)
        {
            breaker.enabled = damageEnabled;
            breaker.launcher = this;
        }

        // Disable collider on visual-only projectiles so they don't trigger
        // RayFire's internal collision handling on remote clients.
        var col = projectile.GetComponent<Collider>();
        if (col != null)
            col.enabled = damageEnabled;

        projectile.SetActive(true);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (damageEnabled)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(direction * launchForce, ForceMode.VelocityChange);
        }
        else
        {
            rb.isKinematic = true;
            StartCoroutine(MoveVisualProjectile(projectile.transform, direction * launchForce));
        }

        _activeTimers[projectile] = StartCoroutine(ReturnToPoolAfterDelay(projectile, projectileLifetime));
    }

    private GameObject GetFromPool()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        return Instantiate(projectilePrefab);
    }

    public void ReturnToPool(GameObject obj)
    {
        if (obj != null && obj.activeSelf)
        {
            CancelTimer(obj);
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    private void CancelTimer(GameObject obj)
    {
        if (_activeTimers.TryGetValue(obj, out var coroutine))
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
            _activeTimers.Remove(obj);
        }
    }

    private IEnumerator MoveVisualProjectile(Transform tf, Vector3 velocity)
    {
        while (tf != null && tf.gameObject.activeSelf)
        {
            tf.position += velocity * Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ReturnToPoolAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(obj);
    }
}

using Unity.Netcode;
using UnityEngine;
using CharacterSystem.Camera;
using CharacterSystem.Core;
using CharacterSystem.Handlers;

public class NetworkPlayerSetup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Owner: enable Rigidbody interpolation for smooth local movement.
        // The prefab defaults to None (correct for non-owners where
        // NetworkTransform handles interpolation instead).
        if (IsOwner)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            return;
        }

        DisableComponent<InputProvider>();
        DisableComponent<InteractionSystem.InteractionDetector>();
        DisableComponent<InteractionSystem.InteractionHighlighter>();
        DisableComponent<GroundDetector>();
        DisableComponent<FirstPersonCamera>(searchChildren: true);
        DisableComponent<CameraInputProvider>(searchChildren: true);
        DisableComponent<UnityEngine.Camera>(searchChildren: true);
        DisableComponent<AudioListener>(searchChildren: true);

        DisableComponent<MovementHandler>();
        DisableComponent<JumpHandler>();
        DisableComponent<GravityHandler>();
    }

    private void DisableComponent<T>(bool searchChildren = false) where T : Behaviour
    {
        T component = searchChildren
            ? GetComponentInChildren<T>()
            : GetComponent<T>();

        if (component != null)
            component.enabled = false;
    }
}

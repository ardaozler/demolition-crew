using Unity.Netcode;
using UnityEngine;
using CharacterSystem.Camera;
using CharacterSystem.Handlers;

public class NetworkPlayerSetup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsOwner) return;

        DisableComponent<InputProvider>();
        DisableComponent<InteractionSystem.InteractionDetector>();
        DisableComponent<FirstPersonCamera>(searchChildren: true);
        DisableComponent<CameraInputProvider>(searchChildren: true);
        DisableComponent<UnityEngine.Camera>(searchChildren: true);
        DisableComponent<AudioListener>(searchChildren: true);
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

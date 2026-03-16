using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkMenuUI : MonoBehaviour
{
    private bool showMenu = true;
    private string joinAddress = "127.0.0.1";
    private string statusMessage = "";

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        }
    }

    private void OnTransportFailure()
    {
        Debug.LogError("[NET] Transport failure! Could not bind or connect.");
        showMenu = true;
        statusMessage = "Transport failed — port in use or network error.";
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NET] Client connected: clientId={clientId}, localId={NetworkManager.Singleton.LocalClientId}, isHost={NetworkManager.Singleton.IsHost}");

        if (clientId == NetworkManager.Singleton.LocalClientId
            || NetworkManager.Singleton.IsConnectedClient)
        {
            showMenu = false;
            statusMessage = "";
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        var reason = NetworkManager.Singleton.DisconnectReason;
        Debug.LogWarning($"[NET] Client disconnected: clientId={clientId}, localId={NetworkManager.Singleton.LocalClientId}, reason=\"{reason}\"");

        if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            showMenu = true;
            statusMessage = string.IsNullOrEmpty(reason)
                ? "Disconnected. Check the IP and try again."
                : $"Disconnected: {reason}";
        }
    }

    private void OnGUI()
    {
        if (!showMenu) return;

        float width = 260f;
        float height = 200f;
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUILayout.BeginArea(new Rect(x, y, width, height));

        GUILayout.Label("Destruction Crew", GUILayout.Height(30f));

        if (!string.IsNullOrEmpty(statusMessage))
            GUILayout.Label(statusMessage);

        if (GUILayout.Button("Host", GUILayout.Height(40f)))
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            Debug.Log($"[NET] Starting Host — listen=0.0.0.0:{transport.ConnectionData.Port}");

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[NET] Host started successfully.");
                showMenu = false;
                statusMessage = "";
            }
            else
            {
                Debug.LogError("[NET] StartHost() returned false!");
                statusMessage = "Failed to start host.";
            }
        }

        GUILayout.Space(5f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("IP:", GUILayout.Width(20f));
        joinAddress = GUILayout.TextField(joinAddress);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Join", GUILayout.Height(40f)))
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.ConnectionData.Address = joinAddress;
            Debug.Log($"[NET] Starting Client — connecting to {joinAddress}:{transport.ConnectionData.Port}");

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("[NET] Client starting, waiting for connection...");
                showMenu = false;
                statusMessage = "";
            }
            else
            {
                Debug.LogError("[NET] StartClient() returned false!");
                statusMessage = "Failed to start client.";
            }
        }

        GUILayout.EndArea();
    }
}

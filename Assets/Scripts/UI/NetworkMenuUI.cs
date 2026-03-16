using UnityEngine;
using Unity.Netcode;

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
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Host hides menu immediately in StartHost; for clients, hide when ANY
        // connection is confirmed for the local instance (LocalClientId may not
        // be assigned yet when this fires, so also accept if we're a connected client).
        if (clientId == NetworkManager.Singleton.LocalClientId
            || NetworkManager.Singleton.IsConnectedClient)
        {
            showMenu = false;
            statusMessage = "";
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        // Only re-show menu if the LOCAL client disconnected (connection failed or kicked)
        if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            showMenu = true;
            statusMessage = "Disconnected. Check the IP and try again.";
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
            statusMessage = "";
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            NetworkManager.Singleton.StartHost();
            showMenu = false;
        }

        GUILayout.Space(5f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("IP:", GUILayout.Width(20f));
        joinAddress = GUILayout.TextField(joinAddress);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Join", GUILayout.Height(40f)))
        {
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            transport.ConnectionData.Address = joinAddress;
            NetworkManager.Singleton.StartClient();
            showMenu = false;
            statusMessage = "";
        }

        GUILayout.EndArea();
    }
}

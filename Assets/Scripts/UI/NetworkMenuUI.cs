using UnityEngine;
using Unity.Netcode;

public class NetworkMenuUI : MonoBehaviour
{
    private bool showMenu = true;
    private string joinAddress = "127.0.0.1";

    private void OnGUI()
    {
        if (!showMenu) return;

        float width = 260f;
        float height = 160f;
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUILayout.BeginArea(new Rect(x, y, width, height));

        GUILayout.Label("Destruction Crew", GUILayout.Height(30f));

        if (GUILayout.Button("Host", GUILayout.Height(40f)))
        {
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
        }

        GUILayout.EndArea();
    }
}

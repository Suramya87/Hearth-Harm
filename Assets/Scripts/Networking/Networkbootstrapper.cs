using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkBootstrapper : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Do not overwrite Offline (single-player) mode — prevents party mode
        // from being clobbered if NGO objects spawn before shutdown completes.
        if (GameManager.Mode == GameMode.Offline) return;

        if (IsHost)
        {
            GameManager.SetMode(GameMode.Host);
            Debug.Log("[NetworkBootstrapper] Mode → Host");
        }
        else
        {

            GameManager.SetMode(GameMode.Client);
            Debug.Log("[NetworkBootstrapper] Mode → Client");
        }
    }
}
using Unity.Netcode;
using UnityEngine;

public static class NetworkHelper
{
    public static bool IsListening => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public static bool IsOffline => !IsListening;

    public static bool IsServer => IsListening && NetworkManager.Singleton.IsServer;

    public static bool HasServerAuthority => IsOffline || (IsListening && NetworkManager.Singleton.IsServer);

    public static bool IsClient => IsListening && NetworkManager.Singleton.IsClient;

    public static ulong LocalClientId => IsListening ? NetworkManager.Singleton.LocalClientId : 0;

    public static bool IsLocalPlayer(Collider collision)
    {
        if (collision == null) return false;
        if (!collision.CompareTag("Player") && collision.GetComponentInParent<CharacterController2D>() == null)
            return false;

        if (IsListening)
        {
            NetworkObject netObj = collision.GetComponentInParent<NetworkObject>();
            return netObj != null && netObj.IsOwner;
        }

        return true;
    }
}

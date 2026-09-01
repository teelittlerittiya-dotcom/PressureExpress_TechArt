namespace PressureExpress.Network
{
    /// <summary>
    /// Chosen exactly once by <see cref="GameBootstrap"/> and never changed afterwards.
    /// The transport is selected by instantiating a different NetworkManager prefab per mode,
    /// so no code path may swap <c>NetworkConfig.NetworkTransport</c> at runtime.
    /// </summary>
    public enum NetworkMode
    {
        /// <summary>Editor only. UnityTransport over 127.0.0.1, Steam never initialises.</summary>
        LocalLoopback,

        /// <summary>Builds. FacepunchTransport over the Steam relay.</summary>
        Steam
    }

    public enum SteamState
    {
        /// <summary>Init has not finished yet.</summary>
        Unknown,

        /// <summary>Steam is running and SteamClient is valid.</summary>
        Ready,

        /// <summary>Steam is not running, or SteamAPI_Init failed (commonly a missing steam_appid.txt next to the exe).</summary>
        NotRunning,

        /// <summary>We are in LocalLoopback, Steam is intentionally not used.</summary>
        Disabled
    }

    public enum SessionState
    {
        Idle,
        Hosting,
        Searching,
        Connecting,
        InSession,
        Leaving
    }

    /// <summary>
    /// Typed outcome for every host/join attempt. Replaces the old bool return so the UI can
    /// tell the player which of several very different failures actually happened.
    /// </summary>
    public enum SessionResult
    {
        Success,

        /// <summary>Code was empty, wrong length, or contained characters outside the code alphabet.</summary>
        InvalidCode,

        /// <summary>Steam mode, but SteamClient is not valid. Never silently fall back to loopback.</summary>
        SteamUnavailable,

        /// <summary>Already hosting or connected.</summary>
        AlreadyInSession,

        /// <summary>SteamMatchmaking.CreateLobbyAsync returned no lobby.</summary>
        LobbyCreateFailed,

        /// <summary>Could not find a free room code after several attempts.</summary>
        CodeUnavailable,

        /// <summary>No lobby published that code.</summary>
        CodeNotFound,

        /// <summary>Lobby exists but is full.</summary>
        LobbyFull,

        /// <summary>Steam refused the lobby join, or the host's approval check rejected us.</summary>
        JoinDenied,

        /// <summary>Transport never reported a connection before the timeout elapsed.</summary>
        ConnectTimeout,

        /// <summary>Unexpected exception, see the log.</summary>
        Failed
    }

    public static class SessionResultText
    {
        public static string ToMessage(this SessionResult result)
        {
            switch (result)
            {
                case SessionResult.Success: return "Connected.";
                case SessionResult.InvalidCode: return "That room code doesn't look right.";
                case SessionResult.SteamUnavailable: return "Steam is not running. Please launch the game through Steam.";
                case SessionResult.AlreadyInSession: return "Already in a session.";
                case SessionResult.LobbyCreateFailed: return "Steam could not create a room. Try again.";
                case SessionResult.CodeUnavailable: return "Could not reserve a room code. Try again.";
                case SessionResult.CodeNotFound: return "No room found with that code.";
                case SessionResult.LobbyFull: return "That room is full.";
                case SessionResult.JoinDenied: return "The host refused the connection.";
                case SessionResult.ConnectTimeout: return "Could not reach the host. They may have closed the game.";
                default: return "Something went wrong. Check the log.";
            }
        }
    }
}

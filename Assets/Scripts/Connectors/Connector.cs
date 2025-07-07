using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using Fusion.Sockets;
using System;
using Game.Utility;

public class Connector : Singleton<Connector>, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner networkRunner;
    //[SerializeField] private NetworkObject playerPrefab; // Assign this in the Inspector
    // Public property to expose NetworkRunner
    public NetworkRunner NetworkRunner => networkRunner;
    internal async void ConnectToServer(string sessionName)
    {
        if (networkRunner == null)
        {
            networkRunner = gameObject.AddComponent<NetworkRunner>();
            Debug.Log("[Connector] Created new NetworkRunner component");
        }
        networkRunner.ProvideInput = true;
        var sceneRef = SceneRef.FromIndex(1);
        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

        var result = await networkRunner.StartGame(new StartGameArgs()
        {
            GameMode = Fusion.GameMode.Shared,
            SessionName = sessionName,
            Scene = sceneInfo,
            PlayerCount = 2,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });


        if (result.Ok)
        {
            Debug.Log("Fusion: Game started successfully.");
        }
        else
        {
            Debug.LogError($"Fusion: Failed to start game. Reason: {result.ShutdownReason}");
        }
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log($"Photon Callback - Connected to server");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.Log($"Photon Callback - Connect failed: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        Debug.Log($"Photon Callback - Connect request: {request}");
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        Debug.Log($"Photon Callback - Custom authentication response: {data}");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Photon Callback - Disconnected from server: {reason}");
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log($"Photon Callback - Host migration: {hostMigrationToken}");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        Debug.Log($"Photon Callback - Input received: {input}");
        if (!runner.IsRunning) return;

        NetworkInputData inputData = new NetworkInputData();
        BoxerController localPlayer = FindObjectsOfType<BoxerController>()
            .FirstOrDefault(p => p.Object.InputAuthority == runner.LocalPlayer);

        if (localPlayer != null && localPlayer.IsInputEnabled)
        {
            // Use the locally buffered input from BoxerController
            inputData.punchPressed = localPlayer.GetPunchInputTriggered();
            if (inputData.punchPressed)
            {
                Debug.Log($"[Connector] Input collected for {localPlayer.PlayerTag}: punchPressed={inputData.punchPressed}");
            }
        }

        input.Set(inputData);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        Debug.Log($"Photon Callback - Input missing for player {player}: {input}");
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        Debug.Log($"Photon Callback - Object entered AOI: {obj} for player {player}");
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        Debug.Log($"Photon Callback - Object exited AOI: {obj} for player {player}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Connector] Player joined: {player}, Total players: {runner.ActivePlayers.Count()},LocalPlayer: {runner.LocalPlayer}");
        if (player == runner.LocalPlayer)
        {
            // Delegate spawning to GameplayManager
            StartCoroutine(GameplayManager.Instance.SpawnPlayer(runner));
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Connector] Player left: {player}, LocalPlayer: {runner.LocalPlayer}, ActivePlayers: {runner.ActivePlayers.Count()}");

        if (GameplayManager.Instance != null && GameplayManager.Instance.IsGameStarted)
        {
            var remainingPlayers = runner.ActivePlayers.ToList();
            if (remainingPlayers.Count < 2)
            {
                PlayerRef remainingPlayer = remainingPlayers[0];
                BoxerController[] boxers = UnityEngine.Object.FindObjectsByType<BoxerController>(FindObjectsSortMode.None);
                BoxerController remainingBoxer = boxers.FirstOrDefault(b =>
                    b.Object != null && b.Object.InputAuthority == remainingPlayer);

                if (remainingBoxer != null)
                {
                    string winnerTag = remainingBoxer.PlayerTag;
                    Debug.Log($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Connector] Declaring {winnerTag} as the winner.");
                    GameplayManager.Instance.RPC_PlayerLeft(winnerTag);
                }
                else
                {
                    Debug.LogError($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Connector] Could not find remaining player's BoxerController");
                    Bridge.Instance.PostMatchAbort("Error determining winner", "BoxerController not found", "1020");
                }
            }
            else
            {
                Debug.LogError($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Connector] Unexpected number of remaining players: {remainingPlayers.Count}");
                Bridge.Instance.PostMatchAbort("Unexpected player count", "", "");
            }
        }
        else
        {
            Debug.Log($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Connector] Player left before game start or GameplayManager not initialized");
            Bridge.Instance.PostMatchAbort("Player left before game start", "", "");
        }
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        Debug.Log($"Photon Callback - Reliable data progress: {progress} for player {player}");
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        Debug.Log($"Photon Callback - Reliable data received for player {player}: {data}");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"Photon Callback - Scene load done");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log($"Photon Callback - Scene load start");        
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"Photon Callback - Session list updated");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Photon Callback - Shutdown: {shutdownReason}");
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        Debug.Log($"Photon Callback - User simulation message: {message}");
    }
}
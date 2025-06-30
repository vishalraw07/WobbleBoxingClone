using Fusion;
using UnityEngine;
public class NetworkCommunicator : NetworkBehaviour
{
    [Networked] public int ArenaIndex { get; set; }
    public static NetworkCommunicator Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitGameplay()
    {
        if (HasStateAuthority) // Host only
        {
            ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
            if (arenaManager != null && arenaManager.arenaPrefabs != null)
            {
                ArenaIndex = Random.Range(0, arenaManager.arenaPrefabs.Length);
                Debug.Log($"[NetworkCommunicator] Selected arena index: {ArenaIndex}");
                RPC_StartGame(ArenaIndex);
            }
            else
            {
                Debug.LogError("[NetworkCommunicator] ArenaManager or arenaPrefabs not found!");
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartGame(int arenaIndex)
    {
        ArenaIndex = arenaIndex;
        ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
        if (arenaManager != null)
        {
            arenaManager.SpawnSpecificArena(arenaIndex);
            Debug.Log($"[NetworkCommunicator] Spawned arena {arenaIndex} on this client");
        }
        else
        {
            Debug.LogError("[NetworkCommunicator] ArenaManager not found during initialization!");
        }

        GameplayManager gameManager = FindObjectOfType<GameplayManager>();
        if (gameManager != null)
        {
            gameManager.StartGameUI();
        }
        else
        {
            Debug.LogError("[NetworkCommunicator] GameManager not found during initialization!");
        }
    }

}


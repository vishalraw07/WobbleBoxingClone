using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Fusion;
using Game.Utility;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class GameplayManager : Singleton<GameplayManager>
{
    public int player1Score = 0;
    public int player2Score = 0;
    public TextMeshProUGUI player1Text; // Label for Player1 (left side)
    public TextMeshProUGUI player2Text; // Label for Player2 (right side)
    public int scoreToWin = 2;
    public TextMeshProUGUI scoreText;
    public Image winLoadImage;
    public Image waitingImage;
    public Sprite p1WinSprite;
    public Sprite p2WinSprite;
    public Sprite load1Sprite;
    public Sprite load2Sprite;
    public Sprite load3Sprite;
    public Sprite loadStartSprite;
    public Sprite youWinSprite;  // Sprite for "You Win"
    public Sprite youLoseSprite; // Sprite for "You Lose"

    public GameObject KO;

    private bool isGameEnded = false;
    [Networked] private NetworkBool IsGameEnded { get; set; } = false;

    public GameObject scoreboardPanel;
    private bool isOnePlayerMode = false;
    public Transform player1Pos, player2Pos;
    public NetworkObject playerPrefab;

    private PlayerController player1Single; // For single-player
    private PlayerController player2Single; // For single-player

    private BoxerController player1Multi;  // For multiplayer
    private BoxerController player2Multi;  // For multiplayer

    // New field for K.O. display duration
    private float koDisplayTime = 1f; // Time to show "K.O." message
    [Networked] public NetworkBool IsTransitioning { get; set; } = false;

    public override void Awake()
    {
        base.Awake();

        // Initialize spawn points if not assigned
        if (player1Pos == null)
        {
            GameObject player1Spawn = new GameObject("Player1Spawn");
            player1Spawn.transform.position = new Vector3(-6, 0, 0);
            player1Pos = player1Spawn.transform;
            Debug.Log("[GameplayManager] Created Player1Spawn at (-6, 0, 0)");
        }

        if (player2Pos == null)
        {
            GameObject player2Spawn = new GameObject("Player2Spawn");
            player2Spawn.transform.position = new Vector3(6, 0, 0);
            player2Pos = player2Spawn.transform;
            Debug.Log("[GameplayManager] Created Player2Spawn at (6, 0, 0)");
        }
    }

    // SpawnPlayer updated to be a coroutine for multiplayer spawning
    internal IEnumerator SpawnPlayer(NetworkRunner runner)
    {
        Debug.Log("[GameplayManager] Spawning Player");

        // Validate spawn positions
        if (player1Pos == null || player2Pos == null)
        {
            Debug.LogError("[GameplayManager] Player1Pos or Player2Pos is not assigned!");
            Bridge.Instance.PostMatchAbort("Game setup failed", "Spawn positions not set", "1014");
            yield break;
        }

        // Wait for the runner to be initialized
        yield return new WaitUntil(() => runner != null && runner.IsRunning);
        // Check if a player has already been spawned for this runner.LocalPlayer
        if (FindObjectsOfType<BoxerController>().Any(p => p.Object.InputAuthority == runner.LocalPlayer))
        {
            Debug.LogWarning("[GameplayManager] Player already spawned for this client!");
            yield break;
        }

        string playerTag;
        Vector3 spawnPosition;
        // Assign Player1 or Player2 based on join order
        int playerCount = runner.ActivePlayers.Count();

        // Assign Player1 or Player2 based on join order
        if (playerCount <= 1)
        {
            playerTag = "Player1";
            spawnPosition = player1Pos.position;
        }
        else
        {
            playerTag = "Player2";
            spawnPosition = player2Pos.position;
        }

        // Spawn the player
        NetworkObject playerObject = runner.Spawn(playerPrefab, spawnPosition, playerPrefab.transform.rotation, runner.LocalPlayer);
        BoxerController player = playerObject.GetComponent<BoxerController>();
        if (player == null)
        {
            Debug.LogError("[GameplayManager] BoxerController component not found on spawned player prefab!");
            Bridge.Instance.PostMatchAbort("Game setup failed", "BoxerController not found", "1013");
            yield break;
        }

        // Set PlayerTag and appearance
        player.PlayerTag = playerTag;
        player.PlayerNumber = playerTag == "Player1" ? 1 : 2; // 
        Vector3 scale = playerTag == "Player1" ? new Vector3(-4, 4, 1) : new Vector3(4, 4, 1);
        player.RPC_SetPositionAndScale(spawnPosition, scale);
        player.RandomizeAppearance();
        Debug.Log($"[GameplayManager] Spawned {playerTag} at {spawnPosition}, scale: {scale}");

        // Start waiting for both players
        StartCoroutine(WaitForPlayers());
    }
    void Start()
    {
        Time.timeScale = 1f;
        KO.SetActive(false);
        ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
        if (arenaManager != null) arenaManager.enabled = false;

        if (Connector.Instance != null)
        {
            // Multiplayer mode: Show waiting image
            if (waitingImage != null) waitingImage.gameObject.SetActive(true);
            // Multiplayer mode: Start the SpawnPlayer coroutine
            StartCoroutine(SpawnPlayer(Connector.Instance.NetworkRunner));
        }
        else
        {
            // Single-player mode
            StartGame(true);
        }
    }

    public void StartGameUI()
    {
        if (scoreboardPanel != null) scoreboardPanel.SetActive(true);
        if (scoreText != null) scoreText.gameObject.SetActive(true);
        if (player1Text != null) player1Text.gameObject.SetActive(true);
        if (player2Text != null) player2Text.gameObject.SetActive(true);
        // Set player labels
        if (player1Text != null && player2Text != null)
        {
            if (isOnePlayerMode)
            {
                player1Text.text = "You";
                player2Text.text = "Opponent";
                Debug.Log("[GameplayManager] Set single-player labels: P1=You, P2=Opponent");
            }
            else
            {
                // Find the local player's BoxerController using HasInputAuthority
                BoxerController localPlayer = FindObjectsOfType<BoxerController>()
                    .FirstOrDefault(p => p.Object.HasInputAuthority);
                string localPlayerTag = localPlayer != null ? localPlayer.PlayerTag : null;

                if (localPlayerTag == "Player1")
                {
                    player1Text.text = "You";
                    player2Text.text = "Opponent";
                }
                else if (localPlayerTag == "Player2")
                {
                    player1Text.text = "Opponent";
                    player2Text.text = "You";
                }
                else
                {
                    Debug.LogWarning($"[GameplayManager] Could not determine local player tag. Defaulting labels. Bridge.PlayerId={Bridge.PlayerId}");
                    player1Text.text = "Player1";
                    player2Text.text = "Player2";
                }
                Debug.Log($"[GameplayManager] Set multiplayer labels: P1={player1Text.text}, P2={player2Text.text}, LocalPlayerTag={localPlayerTag}, Bridge.PlayerId={Bridge.PlayerId}");
            }
        }
        else
        {
            Debug.LogError("[GameplayManager] player1Text or player2Text is not assigned!");
        }

        Debug.Log("[GameplayManager] Game UI started, main menu hidden");
    }
    public void StartGame(bool isOnePlayer)
    {
        Debug.Log($"[GameplayManager] Starting {(isOnePlayer ? "single-player" : "multiplayer")} game");
        isOnePlayerMode = isOnePlayer;
        if (scoreText != null) scoreText.gameObject.SetActive(true);

        if (isOnePlayer)
        {
            // Destroy existing players
            foreach (var player in FindObjectsOfType<PlayerController>())
                Destroy(player.gameObject);
            foreach (var player in FindObjectsOfType<BoxerController>())
                Destroy(player.gameObject);

            // Spawn single-player players
            SpawnLocalPlayers();
            PlayerController[] players = FindObjectsOfType<PlayerController>();
            if (players.Length < 2)
            {
                Debug.LogError($"[GameManager] Only {players.Length} players found!");
                Bridge.Instance.PostMatchAbort("Game setup failed", "Insufficient players", "1011");
                return;
            }
            player1Single = players.FirstOrDefault(p => p.playerTag == "Player1");
            player2Single = players.FirstOrDefault(p => p.playerTag == "Player2");
            if (player1Single == null || player2Single == null)
            {
                Debug.LogError("[GameManager] Failed to find Player1 or Player2 in single-player mode!");
                Bridge.Instance.PostMatchAbort("Game setup failed", "Players not found", "1011");
                return;
            }

            // Initialize players
            player1Single.gameObject.SetActive(true);
            player2Single.gameObject.SetActive(true);
            player1Single.enabled = true;
            player2Single.enabled = true;

            // Initialize arena
            ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
            if (arenaManager != null)
            {
                arenaManager.enabled = true;
                arenaManager.SpawnNextArena();
            }
            else
            {
                Debug.LogError("[GameManager] ArenaManager not found!");
                Bridge.Instance.PostMatchAbort("Game setup failed", "ArenaManager not found", "1012");
            }

            StartGameUI();
            UpdateScoreUI();
            player1Single.transform.position = new Vector3(-6, 0, 0);
            player2Single.transform.position = new Vector3(6, 0, 0);
            player1Single.SetInputEnabled(true);
            player2Single.SetInputEnabled(true);
        }
    }

    public void StartMultiplayerGame()
    {
        Debug.Log("[GameplayManager] Starting multiplayer game");
        // Deactivate waiting image
        if (waitingImage != null) waitingImage.gameObject.SetActive(false);

        BoxerController[] pcs = FindObjectsOfType<BoxerController>();
        Debug.Log($"[GameplayManager] Found {pcs.Length} players");
        foreach (var p in pcs)
        {
            Debug.Log($"[GameplayManager] Found player with tag: {p.PlayerTag}, Position: {p.transform.position}, Scale: {p.transform.localScale}");
        }

        player1Multi = pcs.FirstOrDefault(p => p.PlayerTag == "Player1");
        player2Multi = pcs.FirstOrDefault(p => p.PlayerTag == "Player2");

        if (player1Multi == null || player2Multi == null)
        {
            Debug.LogError($"[GameplayManager] Failed to find Player1 or Player2! P1: {player1Multi}, P2: {player2Multi}");
            Bridge.Instance.PostMatchAbort("Game setup failed", "Players not found", "1011");
            return;
        }

        // Ensure correct positions
        player1Multi.transform.position = player1Pos.position;
        player2Multi.transform.position = player2Pos.position;
        player1Multi.transform.localScale = new Vector3(-4, 4, 1); // Face right
        player2Multi.transform.localScale = new Vector3(4, 4, 1); // Face left

        // Close the session if host
        NetworkRunner runner = Connector.Instance.NetworkRunner;
        if (runner.IsServer)
        {
            runner.SessionInfo.IsOpen = false;
            Debug.Log("[GameplayManager] Session closed by host");
        }

        // Initialize arena
        ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
        if (arenaManager != null)
        {
            arenaManager.enabled = true;
            arenaManager.SpawnNextArena();
        }
        else
        {
            Debug.LogError("[GameplayManager] ArenaManager not found!");
            Bridge.Instance.PostMatchAbort("Game setup failed", "ArenaManager not found", "1012");
            return;
        }

        StartGameUI();
        UpdateScoreUI();

        player1Multi.SetInputEnabled(true);
        player2Multi.SetInputEnabled(true);

        Debug.Log("[GameplayManager] Multiplayer game started successfully");
    }

    private IEnumerator WaitForPlayers()
    {
        float timeout = 200f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            BoxerController[] players = FindObjectsOfType<BoxerController>();
            var player1 = players.FirstOrDefault(p => p.PlayerTag == "Player1");
            var player2 = players.FirstOrDefault(p => p.PlayerTag == "Player2");
            Debug.Log($"[GameplayManager] Waiting for 2 players... Found {players.Length}, P1: {(player1 != null ? player1.PlayerTag : "null")}, P2: {(player2 != null ? player2.PlayerTag : "null")}");

            if (player1 != null && player2 != null)
            {
                StartMultiplayerGame();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        Debug.LogError("[GameplayManager] Timed out waiting for 2 players!");
        Bridge.Instance.PostMatchAbort("Game setup failed", "Timed out waiting for players", "1011");
    }

    private void SpawnLocalPlayers()
    {
        Debug.Log("[GameplayManager] Spawning local players for single-player");
        var prefab = Resources.Load<GameObject>("SinglePlayer");
        if (prefab == null)
        {
            Debug.LogError("[GameplayManager] PlayerPrefabSingleplayer not found in Resources!");
            return;
        }

        GameObject player1Obj = Instantiate(prefab, new Vector3(-6, 0, 0), Quaternion.identity);
        PlayerController player1Ctrl = player1Obj.GetComponent<PlayerController>();
        player1Ctrl.isPlayer1 = true;
        player1Ctrl.playerTag = "Player1";
        player1Ctrl.inputKey = "space";
        player1Ctrl.isAIControlled = false;
        player1Obj.tag = "Player1";
        player1Obj.transform.localScale = new Vector3(-4, 4, 1); // Face right
        GameObject player1Glove = player1Obj.transform.Find("Glove")?.gameObject;
        //if (player1Glove != null)
        //    player1Glove.tag = "Player1";
        //else
        //    Debug.LogError("[GameplayManager] Player1 glove child not found!");

        GameObject player2Obj = Instantiate(prefab, new Vector3(6, 0, 0), Quaternion.identity);
        PlayerController player2Ctrl = player2Obj.GetComponent<PlayerController>();
        player2Ctrl.isPlayer1 = false;
        player2Ctrl.playerTag = "Player2";
        player2Ctrl.inputKey = "return";
        player2Ctrl.isAIControlled = true;
        player2Obj.tag = "Player2";
        player2Obj.transform.localScale = new Vector3(4, 4, 1); // Face left
        GameObject player2Glove = player2Obj.transform.Find("Glove")?.gameObject;
        //if (player2Glove != null)
        //    player2Glove.tag = "Player2";
        //else
        //    Debug.LogError("[GameplayManager] Player2 glove child not found!");

        Debug.Log($"[GameplayManager] Spawned Player1 at {player1Obj.transform.position}, isAIControlled: {player1Ctrl.isAIControlled}, tag: {player1Obj.tag}, glove tag: {player1Glove?.tag}");
        Debug.Log($"[GameplayManager] Spawned Player2 at {player2Obj.transform.position}, isAIControlled: {player2Ctrl.isAIControlled}, tag: {player2Obj.tag}, glove tag: {player2Glove?.tag}");
    }
    public void SinglePlayerRegisterHit(bool isPlayer2)
    {
        if (isGameEnded) return;

        Debug.Log($"[GameplayManager] RegisterHit: {(isPlayer2 ? "Player 2" : "Player 1")} scored a K.O.");
        if (isPlayer2)
            player2Score++;
        else
            player1Score++;
        StartCoroutine(DisplayKOAndTransition(isPlayer2));
    }

    [Rpc(RpcSources.StateAuthority , RpcTargets.All)]
    public void RPC_RegisterHit(bool isPlayer2)
    {
        Debug.Log($"[GameplayManager] entering transistion state {IsGameEnded}, {IsTransitioning}");

        if (IsGameEnded || IsTransitioning) return;

        IsTransitioning = true; // Lock transitions immediately

        Debug.Log($"[GameplayManager] RegisterHit: {(isPlayer2 ? "Player 2" : "Player 1")} scored a K.O.");

        if (isPlayer2)
            player2Score++;
        else
            player1Score++;

        Debug.Log($"[GameplayManager] Score updated: P1={player1Score}, P2={player2Score}");

        RPC_DisplayKOAndTransition(isPlayer2);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DisplayKOAndTransition(bool isPlayer2)
    {
        StartCoroutine(DisplayKOAndTransition(isPlayer2));
    }

    private IEnumerator DisplayKOAndTransition(bool isPlayer2)
    {
        // Display K.O. message
        if (scoreText != null)
        {
            scoreText.text = "K.O.";
            KO.SetActive(true);
            Time.timeScale = 0.1f;
            Time.fixedDeltaTime = Time.timeScale * 0.02f;
            yield return new WaitForSeconds(0.3f);
            KO.SetActive(false);
            Time.timeScale = 1f;
            Debug.Log("[GameplayManager] Displaying K.O.");
        }
        else
        {
            Debug.LogError("[GameplayManager] scoreText is not assigned!");
        }

        // Disable player input during K.O.
        if (isOnePlayerMode)
        {
            if (player1Single != null) player1Single.SetInputEnabled(false);
            if (player2Single != null) player2Single.SetInputEnabled(false);
        }
        else
        {
            if (player1Multi != null) player1Multi.SetInputEnabled(false);
            if (player2Multi != null) player2Multi.SetInputEnabled(false);
        }

        yield return new WaitForSeconds(koDisplayTime);

        // Update score UI
        if (scoreText != null)
        {
            scoreText.text = isOnePlayerMode ? $" {player1Score} | {player2Score}" : $" {player1Score} | {player2Score}";
            Debug.Log($"[GameplayManager] Updated score UI: P1={(isOnePlayerMode ? player1Score : player1Score)}, P2={(isOnePlayerMode ? player2Score : player2Score)}");
        }

        // Check win condition
        int p1Score = isOnePlayerMode ? player1Score : player1Score;
        int p2Score = isOnePlayerMode ? player2Score : player2Score;
        if (p1Score >= scoreToWin || p2Score >= scoreToWin)
        {
            if (isOnePlayerMode)
                isGameEnded = true;
            else
                IsGameEnded = true;
            string winner = p1Score >= scoreToWin ? "Player 1" : "Player 2";
            if (isOnePlayerMode)
                EndGame(winner);
            else
                RPC_EndGame(winner);
            yield break;
        }

        // Transition to next arena without resetting scores
        if (isOnePlayerMode)
        {
            if (player1Single != null)
            {
                player1Single.RandomizeAppearance();
                player1Single.transform.position = new Vector3(-6, 0, 0);
                player1Single.SetInputEnabled(true);
            }
            if (player2Single != null)
            {
                player2Single.RandomizeAppearance();
                player2Single.transform.position = new Vector3(6, 0, 0);
                player2Single.SetInputEnabled(true);
            }
            ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
            if (arenaManager != null)
                arenaManager.SpawnNextArena();
            StartGameUI();
        }
        else
        {
            NetworkRunner runner = Connector.Instance.NetworkRunner;
            if (runner != null  ) // Only state authority (server) updates positions
            {
                if (player1Multi != null)
                {
                    player1Multi.RandomizeAppearance();
                    player1Multi.SetPositionAndScale(player1Pos.position, new Vector3(-4, 4, 1));
                    //player1Multi.ResetRigidbodies();
                    player1Multi.SetInputEnabled(true);
                }
                if (player2Multi != null)
                {
                    player2Multi.RandomizeAppearance();
                    player2Multi.SetPositionAndScale(player2Pos.position, new Vector3(4, 4, 1));
                    //player2Multi.ResetRigidbodies();
                    player2Multi.SetInputEnabled(true);
                }
                ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
                if (arenaManager != null)
                    arenaManager.SpawnNextArena();
            }
            else
                Debug.Log("not happening");
            yield return new WaitForSeconds(0.1f); // Stabilization delay
            IsTransitioning = false; // Unlock transitions
            StartGameUI();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $" {player1Score} | {player2Score}";
            Debug.Log($"[GameplayManager] Updated score UI: P1={player1Score}, P2={player2Score}");
        }
        else
        {
            Debug.LogError("[GameplayManager] scoreText is not assigned!");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_EndGame(string winner)
    {
        Debug.Log($"[GameplayManager] {winner} Wins!");
        if (isOnePlayerMode)
        {
            if (player1Single != null) player1Single.SetInputEnabled(false);
            if (player2Single != null) player2Single.SetInputEnabled(false);
        }
        else
        {
            if (player1Multi != null) player1Multi.SetInputEnabled(false);
            if (player2Multi != null) player2Multi.SetInputEnabled(false);
        }

        if (winLoadImage != null)
        {
            scoreboardPanel.SetActive(false);
            scoreText.gameObject.SetActive(false);
            winLoadImage.gameObject.SetActive(true);
            // Multiplayer: Determine outcome for local player
            BoxerController localPlayer = FindObjectsOfType<BoxerController>().FirstOrDefault(p => p.Object.HasInputAuthority);
            string localPlayerTag = localPlayer != null ? localPlayer.PlayerTag : null;

            string outcome = (localPlayerTag != null && ((winner == "Player 1" && localPlayerTag == "Player1") ||
                             (winner == "Player 2" && localPlayerTag == "Player2"))) ? "won" : "lost";
            winLoadImage.sprite = (outcome == "won") ? youWinSprite : youLoseSprite;
        }

        // Calculate outcome for PostMatchResult
        BoxerController localPlayerForPost = FindObjectsOfType<BoxerController>().FirstOrDefault(p => p.Object.HasInputAuthority);
        string localPlayerTagForPost = localPlayerForPost != null ? localPlayerForPost.PlayerTag : null;
        string outcomeForPost = (localPlayerTagForPost != null &&
                                 ((winner == "Player 1" && localPlayerTagForPost == "Player1") ||
                                  (winner == "Player 2" && localPlayerTagForPost == "Player2"))) ? "won" : "lost";

        int score;
        if (isOnePlayerMode)
        {
            // For single-player (not the focus here, but included for completeness)
            score = player1Score; // Post the human player's score
        }
        else
        {
            // Multiplayer: Set score based on the local player's tag
            if (localPlayerTagForPost == "Player1")
            {
                score = player1Score;
            }
            else if (localPlayerTagForPost == "Player2")
            {
                score = player2Score;
            }
            else
            {
                Debug.LogError("[GameplayManager] Could not determine local player tag for score!");
                score = 0; // Fallback in case of an error
            }
        }

        Bridge.Instance.PostMatchResult(outcomeForPost, score);

        // Multiplayer: don�t restart, quit after 5 seconds
        Debug.Log("[GameplayManager] Game ended in multiplayer, showing win screen.");
        StartCoroutine(EndMultiplayerGameCoroutine());
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $" {player1Score} | {player2Score}";
            Debug.Log($"[GameplayManager] Updated score UI: P1={player1Score}, P2={player2Score}");
        }
        else
        {
            Debug.LogError("[GameplayManager] scoreText is not assigned!");
        }
    }

    void EndGame(string winner)
    {
        Debug.Log($"[GameplayManager] {winner} Wins!");
        if (isOnePlayerMode)
        {
            if (player1Single != null) player1Single.SetInputEnabled(false);
            if (player2Single != null) player2Single.SetInputEnabled(false);
        }
        else
        {
            if (player1Multi != null) player1Multi.SetInputEnabled(false);
            if (player2Multi != null) player2Multi.SetInputEnabled(false);
        }

        if (winLoadImage != null)
        {
            scoreboardPanel.SetActive(false);
            scoreText.gameObject.SetActive(false);
            winLoadImage.gameObject.SetActive(true);
            winLoadImage.sprite = winner == "Player 1" ? youWinSprite : youLoseSprite;
        }

        string outcome = isOnePlayerMode ? (winner == "Player 1" ? "won" : "lost") :
            (winner == "Player 1" && Bridge.PlayerId == player1Multi?.PlayerTag) ||
            (winner == "Player 2" && Bridge.PlayerId == player2Multi?.PlayerTag) ? "won" : "lost";
        int score = winner == "Player 1" ? player1Score : player2Score;
        // Bridge.Instance.PostMatchResult(outcome, score);
        StartCoroutine(PostMatchResultWithDelay(outcome, score));
    }

    private IEnumerator PostMatchResultWithDelay(string outcome, int score)
    {
        yield return new WaitForSeconds(5f);
        Bridge.Instance.PostMatchResult(outcome, score);
    }

    IEnumerator TransitionToLoadSequence(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        if (winLoadImage != null)
        {
            winLoadImage.sprite = load1Sprite;
            yield return new WaitForSeconds(1f);
            winLoadImage.sprite = load2Sprite;
            yield return new WaitForSeconds(1f);
            winLoadImage.sprite = load3Sprite;
            yield return new WaitForSeconds(1f);
            winLoadImage.sprite = loadStartSprite;
            yield return new WaitForSeconds(1f);
            winLoadImage.gameObject.SetActive(false);
            scoreText.gameObject.SetActive(true);
        }
        // Restart game without resetting scores
        if (isOnePlayerMode)
        {
            if (player1Single != null)
            {
                player1Single.RandomizeAppearance();
                player1Single.transform.position = new Vector3(-6, 0, 0);
                player1Single.SetInputEnabled(true);
            }
            if (player2Single != null)
            {
                player2Single.RandomizeAppearance();
                player2Single.transform.position = new Vector3(6, 0, 0);
                player2Single.SetInputEnabled(true);
            }
            ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
            if (arenaManager != null)
                arenaManager.SpawnNextArena();
        }
        else
        {
            if (player1Multi != null)
            {
                player1Multi.RandomizeAppearance();
                player1Multi.transform.position = new Vector3(-6, 0, 0);
                player1Multi.SetInputEnabled(true);
            }
            if (player2Multi != null)
            {
                player2Multi.RandomizeAppearance();
                player2Multi.transform.position = new Vector3(6, 0, 0);
                player2Multi.SetInputEnabled(true);
            }
            ArenaManager arenaManager = FindObjectOfType<ArenaManager>();
            if (arenaManager != null)
                arenaManager.SpawnNextArena();
        }
        StartGameUI();
        UpdateScoreUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private IEnumerator SinglePlayerWinSequence()
    {
        yield return new WaitForSeconds(5f);
        if (player1Single != null) Destroy(player1Single.gameObject);
        if (player2Single != null) Destroy(player2Single.gameObject);
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator EndMultiplayerGameCoroutine()
    {
        // Wait for 5 seconds to display the win sprite
        yield return new WaitForSeconds(5f);

        // Find the local player's BoxerController
        BoxerController localPlayer = FindObjectsOfType<BoxerController>().FirstOrDefault(p => p.Object.HasInputAuthority);
        if (localPlayer != null)
        {
            // Explicitly despawn the local player's network object
            Connector.Instance.NetworkRunner.Despawn(localPlayer.Object);
            Debug.Log("[GameplayManager] player despawned.");
        }

        // Shutdown the network runner and load the main menu scene after completion
        Connector.Instance.NetworkRunner.Shutdown();
    }

}


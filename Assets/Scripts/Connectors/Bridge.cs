using UnityEngine;
using System;
using System.Collections;
using Game.Utility;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;

public class Bridge : Singleton<Bridge>
{

    public static string MatchId { get; private set; }
    public static string PlayerId { get; private set; }
    public static string OpponentId { get; private set; }
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void SendMatchResult(string MatchId ,string PlayerId ,string OpponentId, string outcome, int score1, int score2);
    [DllImport("__Internal")] private static extern void SendMatchAbort(string message, string error, string errorCode);
    
#endif



    void Start()
    {
        // Parse URL parameters
        string url = Application.absoluteURL;
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("[Bridge] Running in Editor, generating random parameters.");
            MatchId = "Room01";
            PlayerId = $"player_{UnityEngine.Random.Range(1000, 9999)}";
            OpponentId = $"player_{UnityEngine.Random.Range(1000, 9999)}";// $"player_{UnityEngine.Random.Range(1000, 9999)}";
            // Ensure unique OpponentId
            while (OpponentId == PlayerId)
            {
                OpponentId = $"player_{UnityEngine.Random.Range(1000, 9999)}";
            }

        }
        else
        {
            Uri uri = new Uri(url);
            string query = uri.Query;
            if (!string.IsNullOrEmpty(query))
            {
                string[] pairs = query.TrimStart('?').Split('&');
                foreach (var pair in pairs)
                {
                    string[] kv = pair.Split('=');
                    if (kv.Length == 2)
                    {
                        string key = Uri.UnescapeDataString(kv[0]);
                        string value = Uri.UnescapeDataString(kv[1]);
                        if (key == "matchId") MatchId = value;
                        else if (key == "playerId") PlayerId = value;
                        else if (key == "opponentId") OpponentId = value;
                    }
                }
            }
        }

        Debug.Log($"[Bridge] MatchId: {MatchId}, PlayerId: {PlayerId}, OpponentId: {OpponentId}");

        if (string.IsNullOrEmpty(MatchId) || string.IsNullOrEmpty(PlayerId))
        {
            PostMatchAbort("Invalid match parameters", "Missing URL parameters", "1004");
            return;
        }

        // Check for bot or multiplayer
        if (PlayerUtils.IsBot(OpponentId))
        {
            Debug.Log("[Bridge] Bot detected, starting single-player mode.");
            SceneManager.LoadScene("Game");
        }
        else
        {
            Debug.Log("[Bridge] Starting multiplayer session.");
            Connector.Instance.ConnectToServer(MatchId);
        }
    }

    public void PostMatchResult(string outcome, int score1 = 0, int score2 = 0)
    {
        Debug.Log($"[Bridge] Sending match_result: outcome={outcome}, score={score1}, opponentscore= {score2}");

#if UNITY_WEBGL && !UNITY_EDITOR
        SendMatchResult(MatchId, PlayerId, OpponentId, outcome, score1, score2);
#else
        Debug.Log($"[Editor] match_result: {{ matchId: {MatchId}, playerId: {PlayerId}, opponentId: {OpponentId}, outcome: {outcome}, score: {score1},opponentscore={score2} }}");
#endif
    }

    public void PostMatchAbort(string message, string error = "", string errorCode = "")
    {
        Debug.Log($"[Bridge] Sending match_abort: message={message}, error={error}, errorCode={errorCode}");

#if UNITY_WEBGL && !UNITY_EDITOR
        SendMatchAbort(message, error, errorCode);
#else
        Debug.Log($"[Editor] match_abort: {{ message: {message}, error: {error}, errorCode: {errorCode} }}");
#endif
    }

    internal bool IsMobile()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return GetDeviceType() == 1;
#else
        return false;
#endif
    }

    public static class PlayerUtils
    {
        public static bool IsBot(string playerId)
        {
            return !string.IsNullOrEmpty(playerId) && (playerId.StartsWith("a9") || playerId.StartsWith("b9"));
        }
    }
}

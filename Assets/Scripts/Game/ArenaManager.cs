using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class ArenaManager : MonoBehaviour
{
    public GameObject[] arenaPrefabs;
    private GameObject currentArena;
    private int currentIndex = -1;

    void Start()
    {
        if (arenaPrefabs == null || arenaPrefabs.Length == 0)
        {
            Debug.LogError("[ArenaManager] No arena prefabs assigned!");
            Bridge.Instance.PostMatchAbort("Game setup failed", "No arena prefabs", "1016");
        }
    }

    public void SpawnNextArena()
    {
        if (arenaPrefabs == null || arenaPrefabs.Length == 0) return;

        if (currentArena != null)
        {
            Destroy(currentArena);
            Debug.Log("[ArenaManager] Destroyed previous arena");
        }

        currentIndex = (currentIndex + 1) % arenaPrefabs.Length;
        currentArena = Instantiate(arenaPrefabs[currentIndex], Vector3.zero, Quaternion.identity);
        Debug.Log($"[ArenaManager] Spawned arena: {currentIndex}");
    }

    public void SpawnSpecificArena(int arenaIndex)
    {
        if (arenaPrefabs == null || arenaIndex < 0 || arenaIndex >= arenaPrefabs.Length)
        {
            Debug.LogError($"[ArenaManager] Invalid arena index: {arenaIndex}");
            Bridge.Instance.PostMatchAbort("Game setup failed", "Invalid arena index", "1017");
            return;
        }

        if (currentArena != null)
        {
            Destroy(currentArena);
            Debug.Log("[ArenaManager] Destroyed previous arena");
        }

        currentIndex = arenaIndex;
        currentArena = Instantiate(arenaPrefabs[currentIndex], Vector3.zero, Quaternion.identity);
        Debug.Log($"[ArenaManager] Spawned arena: {currentIndex}");
    }

}


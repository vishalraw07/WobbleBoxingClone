using Fusion;
using UnityEngine;

public class NetworkArenaManager : NetworkBehaviour
{
    [SerializeField] public NetworkObject[] arenaPrefabs; // Prefabs with NetworkObject
    [Networked] public int CurrentArenaIndex { get; set; } = -1;
    private NetworkObject currentArena;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log("[NetworkArenaManager] Awake, marked as DontDestroyOnLoad");
        ValidateArenaPrefabs();
    }

    void ValidateArenaPrefabs()
    {
        if (arenaPrefabs == null || arenaPrefabs.Length == 0)
        {
            Debug.LogError("[NetworkArenaManager] arenaPrefabs array is null or empty in Inspector!");
            return;
        }
        for (int i = 0; i < arenaPrefabs.Length; i++)
        {
            if (arenaPrefabs[i] == null)
            {
                Debug.LogError($"[NetworkArenaManager] Arena prefab at index {i} is null!");
                continue;
            }
            if (arenaPrefabs[i].GetComponent<NetworkObject>() == null)
            {
                Debug.LogError($"[NetworkArenaManager] Arena prefab at index {i} lacks NetworkObject component!");
            }
            SpriteRenderer sr = arenaPrefabs[i].GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
            {
                Debug.LogError($"[NetworkArenaManager] Arena prefab at index {i} lacks SpriteRenderer in children!");
            }
        }
        Debug.Log($"[NetworkArenaManager] Validated {arenaPrefabs.Length} arena prefabs");
    }

    public override void Spawned()
    {
        Debug.Log($"[NetworkArenaManager] Spawned, HasStateAuthority: {HasStateAuthority}, Object: {gameObject.name}");
        if (arenaPrefabs == null || arenaPrefabs.Length == 0)
        {
            Debug.LogError("[NetworkArenaManager] No arena prefabs assigned or array is empty!");
            Bridge.Instance.PostMatchAbort("Game setup failed", "No arena prefabs", "1016");
        }
    }

    public void SpawnNextArena()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[NetworkArenaManager] SpawnNextArena called on non-authoritative instance!");
            return;
        }
        int nextIndex = (CurrentArenaIndex + 1) % arenaPrefabs.Length;
        SpawnSpecificArena(nextIndex);
    }

    public void SpawnSpecificArena(int arenaIndex)
    {
        Debug.Log($"[NetworkArenaManager] Spawning arena index: {arenaIndex}, HasStateAuthority: {HasStateAuthority}");
        if (arenaPrefabs == null || arenaIndex < 0 || arenaIndex >= arenaPrefabs.Length)
        {
            Debug.LogError($"[NetworkArenaManager] Invalid arena index: {arenaIndex}, array length: {(arenaPrefabs?.Length ?? 0)}");
            Bridge.Instance.PostMatchAbort("Game setup failed", "Invalid arena index", "1017");
            return;
        }
        if (arenaPrefabs[arenaIndex] == null)
        {
            Debug.LogError($"[NetworkArenaManager] Arena prefab at index {arenaIndex} is null!");
            Bridge.Instance.PostMatchAbort("Game setup failed", "Invalid arena prefab", "1017");
            return;
        }

        if (currentArena != null)
        {
            Runner.Despawn(currentArena);
            Debug.Log("[NetworkArenaManager] Despawned previous arena");
        }

        CurrentArenaIndex = arenaIndex;
        currentArena = Runner.Spawn(arenaPrefabs[arenaIndex], Vector3.zero, Quaternion.identity);
        SpriteRenderer sr = currentArena.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "Background";
            sr.sortingOrder = -10;
            Debug.Log("[NetworkArenaManager] Set SpriteRenderer sorting layer");
        }
        else
        {
            Debug.LogWarning("[NetworkArenaManager] No SpriteRenderer found in arena prefab!");
        }
        GameObject floor = currentArena.transform.Find("Floor")?.gameObject;
        if (floor != null)
        {
            floor.tag = "Ground";
            Debug.Log("[NetworkArenaManager] Set Floor tag to Ground");
        }
        else
        {
            Debug.LogWarning("[NetworkArenaManager] Floor child not found in arena prefab!");
        }
        Debug.Log($"[NetworkArenaManager] Spawned arena index: {arenaIndex}, NetworkObject: {currentArena?.name}");
    }
}
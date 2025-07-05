using UnityEngine;
using Fusion;

public class GloveCollisionHandler : MonoBehaviour
{
    private BoxerController boxerController;
    private float lastHitTime;
    private const float hitCooldown = 0.5f;

    void Awake()
    {
        // Get reference to the parent BoxerController
        boxerController = GetComponentInParent<BoxerController>();
        if (boxerController == null)
        {
            Debug.LogError($"[GloveCollisionHandler] {gameObject.name} missing BoxerController in parent hierarchy");
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (Time.time - lastHitTime < hitCooldown || !boxerController.IsInputEnabled)
        {
            Debug.Log($"[GloveCollisionHandler] {boxerController.PlayerTag} skipping collision: Cooldown active or input disabled");
            return;
        }

        Debug.Log($"[GloveCollisionHandler] {boxerController.PlayerTag} OnTriggerEnter2D: This GameObject={gameObject.name}, Tag={gameObject.tag}, Layer={LayerMask.LayerToName(gameObject.layer)}, Collided with={collision.gameObject.name}, Collided Tag={collision.gameObject.tag}, Collided Layer={LayerMask.LayerToName(collision.gameObject.layer)}");

        GameObject target = collision.gameObject;
        BoxerController targetController = target.GetComponentInParent<BoxerController>();
        if (targetController == null)
        {
            Debug.LogWarning($"[GloveCollisionHandler] {boxerController.PlayerTag} collided with {collision.gameObject.name}, but no BoxerController found in parent hierarchy");
            return;
        }
        string targetTag = targetController.PlayerTag;

        // Ensure the collision is with the opponent's body and not self
        if ((targetTag == "Player1" || targetTag == "Player2") && targetController != boxerController)
        {
            Debug.Log($"[GloveCollisionHandler] {boxerController.PlayerTag}'s glove hit {targetTag}'s body");
            GameplayManager gameManager = GameplayManager.Instance;
            if (gameManager != null)
            {
                lastHitTime = Time.time;
                gameManager.RPC_RegisterHit(boxerController.PlayerTag != "Player1");
                if (boxerController.HasStateAuthority)
                {
                    boxerController.RPC_OnHit(collision.transform.position + Vector3.up * 0.5f);
                }
                // Enable the hit indicator on the opponent's prefab
                targetController.RPC_EnableHitIndicator();
            }
        }
    }
}
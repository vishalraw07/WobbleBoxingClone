using UnityEngine;
using Fusion;

public class GloveCollisionSP : MonoBehaviour
{
    private PlayerController boxerController;
    private float lastHitTime;
    private const float hitCooldown = 0.5f;

    void Awake()
    {
        // Get reference to the parent BoxerController
        boxerController = GetComponentInParent<PlayerController>();
        if (boxerController == null)
        {
            Debug.LogError($"[GloveCollisionHandler] {gameObject.name} missing BoxerController in parent hierarchy");
        }

    }


    void OnTriggerStay2D(Collider2D collision)
    {
        if (Time.time - lastHitTime < hitCooldown || !boxerController.isInputEnabled)
        {
            Debug.Log($"[GloveCollisionHandler] {boxerController.playerTag} skipping collision: Cooldown active or input disabled");
            return;
        }

        Debug.Log($"[GloveCollisionHandler] {boxerController.playerTag} OnTriggerEnter2D: This GameObject={gameObject.name}, Tag={gameObject.tag}, Layer={LayerMask.LayerToName(gameObject.layer)}, Collided with={collision.gameObject.name}, Collided Tag={collision.gameObject.tag}, Collided Layer={LayerMask.LayerToName(collision.gameObject.layer)}");

        // Check if the collider belongs to the glove (trigger) and the collision is with a body


        GameObject target = collision.gameObject;
        PlayerController targetController = target.GetComponentInParent<PlayerController>();
        if (targetController == null)
        {
            Debug.LogWarning($"[GloveCollisionHandler] {boxerController.playerTag} collided with {collision.gameObject.name}, but no BoxerController found in parent hierarchy");
            return;
        }
        string targetTag = targetController.playerTag;

        // Ensure the collision is with the opponent's body and not self
        if ((targetTag == "Player1" || targetTag == "Player2") && targetController != boxerController)
        {
            Debug.Log($"[GloveCollisionHandler] {boxerController.playerTag}'s glove hit {targetTag}'s body");
            GameplayManager gameManager = GameplayManager.Instance;
            if (gameManager != null)
            {
                lastHitTime = Time.time;
                gameManager.SinglePlayerRegisterHit(boxerController.playerTag != "Player1");

            }
            targetController.EnableHitIndicator();
        }
    }
}

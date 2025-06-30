using UnityEngine;
using System.Collections;
public class PlayerController : MonoBehaviour
{
    public Rigidbody2D armRigidbody;
    public SpriteRenderer bodySpriteRenderer;
    public SpriteRenderer armSpriteRenderer;
    public SpriteRenderer gloveSpriteRenderer;
    public Sprite[] bodySpritesDark;
    public Sprite[] bodySpritesLight;
    public Sprite[] armSprites;
    public Sprite[] gloveSprites;

    public AudioClip punchHitSFX;       // Assign in Inspector
    public AudioClip jumpSFX;           // Assign in Inspector
    private AudioSource audioSource;    // Reference to AudioSource component

    public float punchForce = 500f;
    public float jumpForce = 350f;
    public string inputKey = "space";
    public string playerTag = "Player1";
    public bool isPlayer1 = true;
    public bool isAIControlled = false;
    private Rigidbody2D bodyRigidbody;

    // Add Animator reference
    private Animator animator;
    private string currentAnimationState = "Idle";

    private bool isPunching = false;
    private bool isInputEnabled = true;
    private Vector3 baseScale = new Vector3(4f, 4f, 1f);
    public float swingSpeed = 2f;
    public float swingAngle = 15f;
    private float swingTime;
    private bool isGrounded;
    private float lastHitTime;
    private const float hitCooldown = 0.5f;
    public GameObject hitEffectPrefab;
    private float nextAIActionTime;
    private bool punchInputTriggered;
    private float spawnTime;
    private const float initialGracePeriod = 1f;

    // AI difficulty parameters
    private float aiPunchForce;
    private float aiJumpForce;
    private float minAIActionDelay; // Minimum delay between AI actions
    private float maxAIActionDelay; // Maximum delay between AI actions
    void Start()
    {
        bodyRigidbody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($"[PlayerController] {playerTag} missing Animator!");
        }
        // Initialize AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError($"[PlayerController] {playerTag} missing AudioSource!");
        }

        // Set AI difficulty parameters based on OpponentId
        if (isAIControlled)
        {
            if (Bridge.OpponentId != null && Bridge.OpponentId.StartsWith("a9"))
            {
                // Easy AI
                aiPunchForce = punchForce * 0.7f; // 70% of normal punch force
                aiJumpForce = jumpForce * 0.7f;   // 70% of normal jump force
                minAIActionDelay = 1.0f;          // Slower reaction
                maxAIActionDelay = 2.0f;
                Debug.Log($"[PlayerController] {playerTag} initialized as Easy AI (a9)");
            }
            else if (Bridge.OpponentId != null && Bridge.OpponentId.StartsWith("b9"))
            {
                // Hard AI
                aiPunchForce = punchForce * 1.2f; // 120% of normal punch force
                aiJumpForce = jumpForce * 1.2f;   // 120% of normal jump force
                minAIActionDelay = 0.3f;          // Faster reaction
                maxAIActionDelay = 0.8f;
                Debug.Log($"[PlayerController] {playerTag} initialized as Hard AI (b9)");
            }
            else
            {
                // Default AI (fallback)
                aiPunchForce = punchForce;
                aiJumpForce = jumpForce;
                minAIActionDelay = 0.5f;
                maxAIActionDelay = 1.5f;
                Debug.LogWarning($"[PlayerController] {playerTag} initialized with default AI parameters (OpponentId: {Bridge.OpponentId})");
            }
        }
        else
        {
            aiPunchForce = punchForce;
            aiJumpForce = jumpForce;
            minAIActionDelay = 0.5f;
            maxAIActionDelay = 1.5f;
        }

        RandomizeAppearance();
        bodyRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        bodyRigidbody.linearVelocity = new Vector2(0, bodyRigidbody.linearVelocity.y);
        Debug.Log($"[PlayerController] {playerTag} initialized, isAIControlled: {isAIControlled}, inputKey: {inputKey}, tag: {gameObject.tag}");
        if (isAIControlled)
            nextAIActionTime = Time.time + Random.Range(0.5f, 1.5f);

        // Initialize lastHitTime to prevent immediate hit registration
        lastHitTime = Time.time - hitCooldown;
        spawnTime = Time.time;
    }

    void Update()
    {
        // Update animation state
        if (animator != null)
        {
            animator.Play(currentAnimationState);
        }

        if (!isInputEnabled || isAIControlled) return;

        if (Input.GetKeyDown(inputKey))
        {
            punchInputTriggered = true;
            Debug.Log($"[PlayerController] {playerTag} detected input: {inputKey}");
        }
    }

    void FixedUpdate()
    {
        if (!isInputEnabled) return;

        swingTime += swingSpeed * Time.fixedDeltaTime;
        float swingRotation = Mathf.Sin(swingTime) * swingAngle;
        transform.rotation = Quaternion.Euler(0, 0, swingRotation);

        if (isGrounded && !isPunching)
            bodyRigidbody.linearVelocity = new Vector2(0, bodyRigidbody.linearVelocity.y);

        if (!isAIControlled)
        {
            if (punchInputTriggered && !isPunching)
            {
                StartCoroutine(PunchAndJump(Random.Range(0.8f, 1.2f), isPlayer1 ? Vector2.right : Vector2.left));
                punchInputTriggered = false;
            }
        }
        else
        {
            if (Time.time >= nextAIActionTime && !isPunching)
            {
                // Dynamic delay based on opponent proximity
                float distanceToOpponent = CalculateDistanceToOpponent();
                float delay = distanceToOpponent < 2f ? Random.Range(0.3f, 0.8f) : Random.Range(0.5f, 1.5f);
                StartCoroutine(PunchAndJump(Random.Range(0.8f, 1.2f), isPlayer1 ? Vector2.right : Vector2.left));
                nextAIActionTime = Time.time + Random.Range(1f, 3f);
            }
        }
    }
    // Helper method to calculate distance to opponent
    private float CalculateDistanceToOpponent()
    {
        GameObject opponent = GameObject.FindGameObjectWithTag(isPlayer1 ? "Player2" : "Player1");
        if (opponent != null)
        {
            return Vector2.Distance(transform.position, opponent.transform.position);
        }
        return float.MaxValue; // Fallback if opponent not found
    }
    private IEnumerator PunchAndJump(float randomFactor, Vector2 punchDirection)
    {
        isPunching = true;
        // Set punch animation and play jump sound
        currentAnimationState = "Punch";
        if (audioSource != null && jumpSFX != null)
        {
            audioSource.PlayOneShot(jumpSFX);
        }

        float swingValue = Mathf.Sin(swingTime);
        Vector2 jumpDirection = swingValue > 0 ? Vector2.left : Vector2.right;
        // Use AI-specific forces if AI-controlled
        float currentPunchForce = isAIControlled ? aiPunchForce : punchForce;
        float currentJumpForce = isAIControlled ? aiJumpForce : jumpForce;

        armRigidbody.AddForce(punchDirection * (randomFactor * punchForce), ForceMode2D.Impulse);
        bodyRigidbody.AddForce((Vector2.up * 0.7f + jumpDirection * 1.2f) * (randomFactor * jumpForce), ForceMode2D.Impulse);

        Vector3 originalScale = armSpriteRenderer.transform.localScale;
        armSpriteRenderer.transform.localScale = originalScale * 1.2f;
        gloveSpriteRenderer.transform.localScale = originalScale * 1.2f;

        yield return new WaitForSeconds(0.2f);

        armSpriteRenderer.transform.localScale = originalScale;
        gloveSpriteRenderer.transform.localScale = originalScale;
        // Return to idle animation
        currentAnimationState = "Idle";
        isPunching = false;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Ignore hits during initial grace period
        if (Time.time < spawnTime + initialGracePeriod)
        {
            Debug.Log($"[TriggerDebug] {playerTag} ignoring trigger during grace period with {collision.gameObject.name} at Time.time: {Time.time}");
            return;
        }

        if (Time.time - lastHitTime < hitCooldown || !isInputEnabled) return;

        // Get the root GameObject (parent or self)
        GameObject target = collision.gameObject;
        PlayerController targetController = target.GetComponentInParent<PlayerController>();
        if (targetController == null)
        {
            Debug.LogWarning($"[PlayerController] {playerTag} collided with {collision.gameObject.name}, but no PlayerController found in parent hierarchy");
            return;
        }
        string targetTag = targetController.playerTag;

        Debug.Log($"[TriggerDebug] {playerTag} triggered with {collision.gameObject.name} (tag: {collision.gameObject.tag}, effective tag: {targetTag}) at Time.time: {Time.time}, lastHitTime: {lastHitTime}");

        // Only register hit if this player hits the opponent (not self)
        if ((targetTag == (isPlayer1 ? "Player2" : "Player1")) && targetController != this)
        {
            Debug.Log($"[PlayerController] {playerTag} hit {targetTag}");
            GameplayManager gameManager = FindObjectOfType<GameplayManager>();
            if (gameManager != null)
            {
                lastHitTime = Time.time;
                gameManager.SinglePlayerRegisterHit(!isPlayer1);
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, collision.transform.position + Vector3.up * 0.5f, Quaternion.identity);
                }
                if (audioSource != null && punchHitSFX != null)
                {
                    audioSource.PlayOneShot(punchHitSFX);
                }
            }
            else
                Debug.Log($"[gameplaymanager] not found ");
        }

    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }

    public void RandomizeAppearance()
    {
        Sprite[] selectedBodySprites = Random.value > 0.5f ? bodySpritesDark : bodySpritesLight;
        bodySpriteRenderer.sprite = selectedBodySprites[Random.Range(0, selectedBodySprites.Length)];
        armSpriteRenderer.sprite = armSprites[Random.Range(0, armSprites.Length)];
        gloveSpriteRenderer.sprite = gloveSprites[Random.Range(0, gloveSprites.Length)];
        transform.localScale = new Vector3(
            isPlayer1 ? -baseScale.x : baseScale.x,
            baseScale.y,
            baseScale.z
        );        
    }

    public void SetInputEnabled(bool enabled)
    {
        isInputEnabled = enabled;
        Debug.Log($"[PlayerController] {playerTag} input enabled: {enabled}");
    }

}


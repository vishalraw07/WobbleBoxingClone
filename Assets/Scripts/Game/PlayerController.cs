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

    public AudioClip punchHitSFX; // Assign in Inspector
    public AudioClip jumpSFX;     // Assign in Inspector
    private AudioSource audioSource; // Reference to AudioSource component

    public float punchForce = 500f;
    public float jumpForce = 350f;
    public float horizontalJumpForce = 100f; // Added from BoxerController
    public float moveForce = 300f; // Added from BoxerController
    public float angularVelocity = 300f; // Added from BoxerController
    public string inputKey = "space";
    public string playerTag = "Player1";
    public bool isPlayer1 = true;
    public bool isAIControlled = false;
    private Rigidbody2D bodyRigidbody;

    [HideInInspector] public ShadowFollow shadow;
    public Vector3 FeetPosition => bodyRigidbody.position - new Vector2(transform.up.x, transform.up.y) * 0.55f;

    // Animator reference
    private Animator animator;
    private string currentAnimationState = "Idle";

    public GameObject hitIndicator;
    public GameObject DustHead;


    private bool isPunching = false;
    public bool isInputEnabled = true;
    private Vector3 baseScale = new Vector3(4f, 4f, 1f);
    public float swingSpeed = 2f;
    public float swingAngle = 15f;
    private float swingTime;
    private bool isGrounded;
    private float lastHitTime;
    private const float HIT_COOLDOWN = 0.5f;
    public GameObject hitEffectPrefab;
    private float nextAIActionTime;
    private bool punchInputTriggered;
    private float spawnTime;
    private const float initialGracePeriod = 1f;
    public ParticleSystem Dust;

    // Ground check parameters from BoxerController
    [Header("Ground Check")]
    [SerializeField] private float groundCheckOffset = 0.1f;
    [SerializeField] private float groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask groundLayerMask;

    [SerializeField] private Transform foot; // Reference to foot Transform for center of mass
    // Center of mass from BoxerController
    [SerializeField] private Vector2 centerMass = new Vector2(0f, -0.5f);
    private bool hasStoppedRotation;
    private bool isSlowingRotation;
    public BoxCollider2D gloveCollider;


    // AI difficulty parameters
    private float aiPunchForce;
    private float aiJumpForce;
    private float minAIActionDelay; // Minimum delay between AI actions
    private float maxAIActionDelay; // Maximum delay between AI actions

    // Punch direction based on player
    private Vector2 PunchDirection => isPlayer1 ? Vector2.right : Vector2.left;

    void Start()
    {
        bodyRigidbody = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (bodyRigidbody == null)
        {
            Debug.LogError($"[PlayerController] {playerTag} missing Rigidbody2D!");
            return;
        }
        if (animator == null)
        {
            Debug.LogError($"[PlayerController] {playerTag} missing Animator!");
            return;
        }
        if (audioSource == null)
        {
            Debug.LogError($"[PlayerController] {playerTag} missing AudioSource!");
            return;
        }
        if (punchHitSFX == null || jumpSFX == null)
        {
            Debug.LogError($"[PlayerController] {playerTag} missing audio clips!");
        }

        // Set AI difficulty parameters
        if (isAIControlled)
        {
            if (Bridge.OpponentId != null && Bridge.OpponentId.StartsWith("a9"))
            {
                // Easy AI
                aiPunchForce = punchForce * 0.7f;
                aiJumpForce = jumpForce * 0.7f;
                minAIActionDelay = 1.0f;
                maxAIActionDelay = 2.0f;
                Debug.Log($"[PlayerController] {playerTag} initialized as Easy AI (a9)");
            }
            else if (Bridge.OpponentId != null && Bridge.OpponentId.StartsWith("b9"))
            {
                // Hard AI
                aiPunchForce = punchForce * 1.2f;
                aiJumpForce = jumpForce * 1.2f;
                minAIActionDelay = 0.3f;
                maxAIActionDelay = 0.8f;
                Debug.Log($"[PlayerController] {playerTag} initialized as Hard AI (b9)");
            }
            else
            {
                // Default AI
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
        bodyRigidbody.constraints = RigidbodyConstraints2D.None; // Allow rotation, unlike original
        bodyRigidbody.linearVelocity = new Vector2(0, bodyRigidbody.linearVelocity.y);
        lastHitTime = Time.time - HIT_COOLDOWN;
        spawnTime = Time.time;
        if (isAIControlled)
            nextAIActionTime = Time.time + Random.Range(minAIActionDelay, maxAIActionDelay);

        Debug.Log($"[PlayerController] {playerTag} initialized, isAIControlled: {isAIControlled}, inputKey: {inputKey}, tag: {gameObject.tag}");

        DustHead.SetActive(false);
        hitIndicator.SetActive(false);
    }

    void Update()
    {
        // Ground Check (BoxerController style)
        UpdateGrounded();

        // Update animation state
        if (animator != null)
        {
            animator.Play(currentAnimationState);
        }

        bodyRigidbody.centerOfMass = centerMass;
        if (bodyRigidbody.IsSleeping())
            bodyRigidbody.WakeUp();

        if (bodyRigidbody.rotation > 50f || bodyRigidbody.rotation < -50f)
        {
            bodyRigidbody.angularVelocity *= -1f;
            bodyRigidbody.rotation = Mathf.Clamp(bodyRigidbody.rotation, -49.5f, 49.5f);
        }

       
        bodyRigidbody.angularDamping = 2f;
        gloveCollider.isTrigger = isPunching;
        if (!isInputEnabled || isAIControlled ) return;

        if (Input.GetKeyDown(inputKey))
        {
            punchInputTriggered = true;             
            Debug.Log($"[PlayerController] {playerTag} detected input: {inputKey}");
        }
         
    }

    void FixedUpdate()
    {
        if (!isInputEnabled) return;

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


    private void UpdateGrounded()
    {
        Vector3 origin = bodyRigidbody.position + Vector2.up * groundCheckOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector3.down, groundCheckDistance, groundLayerMask);
        isGrounded = hit.collider != null;
        Debug.Log($"[PlayerController] {playerTag} Ground Check: {hit.collider}");
    }

    private IEnumerator IStopRotation()
    {
        yield return new WaitForSeconds(2.5f);
        Debug.Log($"[PlayerController] {playerTag} Stopping rotation in IStopRotation");
        isSlowingRotation = true;
        while (bodyRigidbody.angularDamping < 2.5f)
        {
            yield return new WaitForEndOfFrame();
            bodyRigidbody.angularDamping += Time.deltaTime;
        }
        isSlowingRotation = false;

        yield return new WaitForSeconds(0.5f);
        bodyRigidbody.angularDamping = 0.05f;
        hasStoppedRotation = false;
    }

    private float CalculateDistanceToOpponent()
    {
        GameObject opponent = GameObject.FindGameObjectWithTag(isPlayer1 ? "Player2" : "Player1");
        if (opponent != null)
        {
            return Vector2.Distance(transform.position, opponent.transform.position);
        }
        return float.MaxValue;
    }

    private IEnumerator PunchAndJump(float randomFactor, Vector2 punchDirection)
    {
         isPunching = true;

        // Use AI-specific forces if AI-controlled
        float currentPunchForce = isAIControlled ? aiPunchForce : punchForce;
        float currentJumpForce = isAIControlled ? aiJumpForce : jumpForce;

        // --- BoxerController-style physics ---
        if (isGrounded)
        {
            currentAnimationState = "Punch";
            audioSource.PlayOneShot(jumpSFX);
            Dust.Play();
            // Determine direction for lunge (horizontalDir logic from BoxerController)
            Vector3 horizontalDir = -punchDirection * (bodyRigidbody.rotation > 0f ? 1f : -1f);
            if (transform.localScale.x > 0f)
            {
                horizontalDir *= -1f;
            }
            // Apply lunge and jump (BoxerController logic)
            bodyRigidbody.linearVelocity = (Vector3.up * currentJumpForce * randomFactor) + (horizontalDir * horizontalJumpForce);
            bodyRigidbody.angularVelocity = (Random.Range(0, 2) == 0 ? 1f : -1f) * 100f;
        }

        Vector3 originalScale = armSpriteRenderer.transform.localScale;
        armSpriteRenderer.transform.localScale = originalScale * 1.2f;
        gloveSpriteRenderer.transform.localScale = originalScale * 1.2f;

        yield return new WaitForSeconds(0.15f); // Shortened for faster, chaotic feel

        armSpriteRenderer.transform.localScale = originalScale;
        gloveSpriteRenderer.transform.localScale = originalScale;
        // Return to idle animation
        currentAnimationState = "Idle";
        isPunching = false;
    }
    public void EnableHitIndicator()
    {
        if (hitIndicator != null)
        {
            StartCoroutine(ShowHitIndicator());
            Debug.Log($"[playerController] {playerTag} enabled hit indicator");
        }
    }

    private IEnumerator ShowHitIndicator()
    {
        hitIndicator.SetActive(true);
        DustHead.SetActive(true);
        yield return new WaitForSeconds(1f); // Wait for 2 seconds
        DustHead.SetActive(false);
        hitIndicator.SetActive(false);
        Debug.Log($"[playerController] {playerTag} disabled hit indicator after 2 seconds");
    }
    /*
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (Time.time < spawnTime + initialGracePeriod)
        {
            Debug.Log($"[PlayerController] {playerTag} ignoring trigger during grace period with {collision.gameObject.name}");
            return;
        }

        if (Time.time - lastHitTime < HIT_COOLDOWN || !isInputEnabled) return;

        GameObject target = collision.gameObject;
        PlayerController targetController = target.GetComponentInParent<PlayerController>();
        if (targetController == null)
        {
            Debug.LogWarning($"[PlayerController] {playerTag} collided with {collision.gameObject.name}, but no PlayerController found in parent hierarchy");
            return;
        }
        string targetTag = targetController.playerTag;

        Debug.Log($"[PlayerController] {playerTag} triggered with {collision.gameObject.name} (tag: {collision.gameObject.tag}, effective tag: {targetTag})");

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
                    Debug.Log($"[PlayerController] {playerTag} played punch hit SFX");
                }
            }
            else
            {
                Debug.LogError("[PlayerController] GameplayManager not found!");
            }
        }
    }
    */
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            Debug.Log($"[PlayerController] {playerTag} grounded");
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
            Debug.Log($"[PlayerController] {playerTag} not grounded");
        }
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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position + (Vector3)(Quaternion.Euler(0, 0, bodyRigidbody ? bodyRigidbody.rotation : 0) * centerMass), 0.1f);

        // Draw ground check
        Gizmos.color = Color.blue;
        Vector2 origin = bodyRigidbody ? bodyRigidbody.position + Vector2.up * groundCheckOffset : (Vector2)transform.position + Vector2.up * groundCheckOffset;
        Gizmos.DrawLine(origin, origin + groundCheckDistance * Vector2.down);
    }
}
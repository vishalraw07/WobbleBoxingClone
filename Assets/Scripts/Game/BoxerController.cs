using Fusion;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

public class BoxerController : NetworkBehaviour
{
    public Rigidbody2D armRigidbody;
    public SpriteRenderer bodySpriteRenderer;
    public SpriteRenderer armSpriteRenderer;
    public SpriteRenderer gloveSpriteRenderer;
    public Sprite[] bodySprites;
    public Sprite[] armSprites;
    public Sprite[] gloveSprites;

    public AudioClip punchHitSFX; // Assign in Inspector
    public AudioClip jumpSFX;     // Assign in Inspector
    private AudioSource audioSource; // Reference to AudioSource component
    public ParticleSystem Dust;

    [HideInInspector] public ShadowFollow shadow;

    public Vector3 FeetPosition => bodyRigidbody.position - new Vector2(transform.up.x, transform.up.y) * 0.55f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckOffset;
    [SerializeField] private float groundCheckDistance;
    [SerializeField] private LayerMask groundLayerMask;

    public float punchForce = 500f;
    public float jumpForce = 350f;
    public float horizontalJumpForce = 100f;
    public float moveForce = 300f; // New force to move toward opponent
    public float angularVelocity = 300f; // New force to move toward opponent
    public string inputKey = "space"; // Set in Inspector
    public bool isPlayer1 = true; // Set in Inspector
    private bool isInputEnabled = true;
    public Rigidbody2D bodyRigidbody;
    // Add Animator reference
    private Animator animator;
    [Networked] public string PlayerTag { get; set; }
    [Networked] private NetworkBool IsPunching { get; set; }
    [Networked] private Vector3 NetworkedPosition { get; set; }
    [Networked] private Vector3 NetworkedLocalScale { get; set; }
    [Networked] private Quaternion NetworkedRotation { get; set; }
    [Networked] public NetworkBool IsInputEnabled { get; set; } = true;
    [Networked] private NetworkBool IsGrounded { get; set; }
    [Networked] private int BodySpriteIndex { get; set; }
    [Networked] private int ArmSpriteIndex { get; set; }
    [Networked] private int GloveSpriteIndex { get; set; }
    [Networked] public int PlayerNumber { get; set; }
    // Networked property for animation state
    [Networked] private string AnimationState { get; set; } = "Idle"; // Default to Idle
                                                                      // Reference to the Collider component
    public BoxCollider2D gloveCollider;
    [SerializeField] private Transform foot; // Reference to foot Transform for center of mass
    [SerializeField] private Vector2 centerMass;

    // Reference to the hit indicator object
    public GameObject hitIndicator;
    public GameObject DustHead;

    private Vector2 PunchDirection => PlayerTag == "Player1" ? Vector2.right : Vector2.left;

    public float lastHitTime;
    public const float HIT_COOLDOWN = 0.5f;
   
    private bool punchInputThisFrame;
    private bool isFacingRight = true;
    private bool hasStoppedRotation;
    private bool isSlowingRotation;
    // Local input buffer, similar to PlayerController
    private bool punchInputTriggered;

    public override void Spawned()
    {
        bodyRigidbody = GetComponent<Rigidbody2D>();
        // Get Animator component
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (bodyRigidbody == null)
        {
            Debug.LogError($"[BoxerController] {PlayerTag} missing Rigidbody2D!");
            return;
        }
        if (animator == null)
        {
            Debug.LogError($"[BoxerController] {PlayerTag} missing Animator!");
            return;
        }
        if (audioSource == null)
        {
            Debug.LogError($"[BoxerController] {PlayerTag} missing AudioSource!");
            return;
        }
        if (punchHitSFX == null || jumpSFX == null)
        {
            Debug.LogError($"[BoxerController] {PlayerTag} missing audio clips!");
        }

        if (HasStateAuthority)
        {
            NetworkedPosition = transform.position;
            NetworkedLocalScale = transform.localScale;
            isFacingRight = NetworkedLocalScale.x > 0;
            inputKey = PlayerTag == "Player1" ? "space" : "return";
            bodyRigidbody.constraints = RigidbodyConstraints2D.None;
            bodyRigidbody.linearVelocity = new Vector2(0, bodyRigidbody.linearVelocity.y);
            PlayerNumber = PlayerTag == "Player1" ? 1 : 2;
            AnimationState = "Idle"; // Initialize animation state
        }
        else
        {
            transform.position = NetworkedPosition;
            transform.localScale = NetworkedLocalScale;
            isFacingRight = NetworkedLocalScale.x > 0;
            inputKey = PlayerTag == "Player1" ? "space" : "return";
        }

        ApplyAppearance();
        // Apply initial animation state
        UpdateAnimation();
        gameObject.SetActive(true);
        // Ensure hitIndicator is disabled initially (optional if set in prefab)
        if (hitIndicator != null)
        {
            hitIndicator.SetActive(false);
        }
        DustHead.SetActive(false);
        Debug.Log($"[BoxerController] {PlayerTag} initialized at {transform.position}, scale: {transform.localScale}, inputKey: {inputKey}, facing {(isFacingRight ? "right" : "left")}, HasStateAuthority: {HasStateAuthority}, HasInputAuthority: {HasInputAuthority}");
    }

    void Update()
    {
        // Ground Check
        UpdateGrounded();

        if (HasInputAuthority && IsInputEnabled && !IsPunching)
        {
            if (Input.GetKeyDown(inputKey))
            {
                punchInputTriggered = true;
                Debug.Log($"[BoxerController] {PlayerTag} detected input: {inputKey}");
            }
        }
        if (HasInputAuthority)
        {
            bodyRigidbody.centerOfMass = centerMass;
            if (bodyRigidbody.IsSleeping())
                bodyRigidbody.WakeUp();

            if (bodyRigidbody.rotation > 50f || bodyRigidbody.rotation < -50f)
            {
                bodyRigidbody.angularVelocity *= -1f;
                bodyRigidbody.rotation = Mathf.Clamp(bodyRigidbody.rotation, -49.5f, 49.5f);
            }
                        
            bodyRigidbody.angularDamping = 2f;
        }

        if (HasInputAuthority && IsGrounded && !hasStoppedRotation)
        {
            hasStoppedRotation = true;
            StartCoroutine(nameof(IStopRoation));
        }

       gloveCollider.isTrigger = IsPunching;
        // Update animation state on all clients
        UpdateAnimation();
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsInputEnabled) return;

        if (HasInputAuthority)
        {
            // Process networked input from Connector
            if (Runner.TryGetInputForPlayer<NetworkInputData>(Object.InputAuthority, out var input) && input.punchPressed && !IsPunching)
            {
                Debug.Log($"[BoxerController] {PlayerTag} processing networked punch input");
                float randomFactor = Random.Range(0.8f, 1.2f);

                RPC_PunchAndJump(IsGrounded, randomFactor, PunchDirection);
                
                punchInputTriggered = false; // Clear local buffer
            }
        }
        if (HasStateAuthority)
        {
            if (IsGrounded && !IsPunching)
                bodyRigidbody.linearVelocity = new Vector2(0, bodyRigidbody.linearVelocity.y);

            NetworkedRotation = transform.rotation;
            NetworkedPosition = transform.position;
            NetworkedLocalScale = transform.localScale;
        }
        else
        {
            transform.position = NetworkedPosition;
            transform.rotation = NetworkedRotation;
            transform.localScale = NetworkedLocalScale;
        }
    }

    private void UpdateGrounded()
    {
        Vector3 origin = bodyRigidbody.position + Vector2.up * groundCheckOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector3.down, groundCheckDistance, groundLayerMask);
        Debug.Log($"Ground Check: {hit.collider}");
        IsGrounded = hit.collider != null;
        
    }

    private IEnumerator IStopRoation()
    {
        yield return new WaitForSeconds(2.5f);
        Debug.Log("Stopping rotation in IStopRotation");
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

    // Helper method to find opponent direction
    private Vector2 GetOpponentDirection()
    {
        string opponentTag = PlayerTag == "Player1" ? "Player2" : "Player1";
        var opponent = FindObjectsOfType<BoxerController>().FirstOrDefault(p => p.PlayerTag == opponentTag);
        if (opponent != null)
        {
            Vector2 direction = (opponent.transform.position - transform.position).normalized;
            return new Vector2(direction.x, 0); // Horizontal movement only
        }
        return PlayerTag == "Player1" ? Vector2.right : Vector2.left; // Fallback
    }

    // Provide access to punchInputTriggered for Connector
    public bool GetPunchInputTriggered()
    {
        return punchInputTriggered;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PunchAndJump(bool isGrounded, float randomFactor, Vector2 punchDirection)
    {
        Debug.Log($"IsGrounded: {isGrounded}, randomFActor: {randomFactor}, punchDirection: {punchDirection}");
        continuance = StartCoroutine(PunchAndJumpLogic(isGrounded, randomFactor, punchDirection));
        

    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayJumpSFX()
    {
        if (audioSource != null && jumpSFX != null)
        {
            audioSource.PlayOneShot(jumpSFX);
            Dust.Play();
            Debug.Log($"[BoxerController] {PlayerTag} played jump SFX");
        }
    }

    private Coroutine continuance;
    private IEnumerator PunchAndJumpLogic(bool isGrounded, float randomFactor, Vector2 punchDirection)
    {
        if (IsPunching) yield break;
        IsPunching = true;

        if (HasStateAuthority)
        {
            AnimationState = "Punch";
            RPC_PlayJumpSFX();

            StopCoroutine(nameof(IStopRoation));
            bodyRigidbody.angularDamping = 0.05f;
            hasStoppedRotation = false;
            if (isGrounded)
            {
                Vector3 horizontalDir = -punchDirection * (bodyRigidbody.rotation > 0f ? 1f : -1f);
                if (transform.localScale.x > 0f)
                {
                    horizontalDir *= -1f;
                }
                bodyRigidbody.linearVelocity = (Vector3.up * randomFactor * jumpForce) + (horizontalDir * horizontalJumpForce);
                bodyRigidbody.angularVelocity = (Random.Range(0, 2) == 0 ? 1f : -1f) * 100f;
            }
        }

        Vector3 originalScale = armSpriteRenderer.transform.localScale;
        armSpriteRenderer.transform.localScale = originalScale * 1.2f;
        gloveSpriteRenderer.transform.localScale = originalScale * 1.2f;

        yield return new WaitForSeconds(0.15f); // Shortened for faster, chaotic feel

        armSpriteRenderer.transform.localScale = originalScale;
        gloveSpriteRenderer.transform.localScale = originalScale;

        if (HasStateAuthority)
        {
            AnimationState = "Idle";
        }

        IsPunching = false;
        
        Debug.Log($"[BoxerController] {PlayerTag} completed punch");
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_OnHit(Vector3 hitPosition)
    {
        
        if (audioSource != null && punchHitSFX != null)
        {
            audioSource.PlayOneShot(punchHitSFX);
            Debug.Log($"[BoxerController] {PlayerTag} played punch hit SFX");
        }
        
    }

   [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_EnableHitIndicator()
    {
        if (hitIndicator != null)
        {
            StartCoroutine(ShowHitIndicator());
            Debug.Log($"[BoxerController] {PlayerTag} enabled hit indicator");
        }
    }

    private IEnumerator ShowHitIndicator()
    {
        hitIndicator.SetActive(true);
        DustHead.SetActive(true);
        yield return new WaitForSeconds(1f); // Wait for 2 seconds
        DustHead.SetActive(false);
        hitIndicator.SetActive(false);
        Debug.Log($"[BoxerController] {PlayerTag} disabled hit indicator after 2 seconds");
    }

    // Method to update animation state on all clients
    private void UpdateAnimation()
    {
        if (animator == null) return;

        // Set animator parameter based on networked AnimationState
        if (AnimationState == "Punch")
        {
            animator.Play("Punch");
        }
        else
        {
            animator.Play("Idle");
        }
    }

    private void ApplyAppearance()
    {
        if (bodySpriteRenderer != null && bodySprites.Length > 0)
            bodySpriteRenderer.sprite = bodySprites[BodySpriteIndex];
        if (armSpriteRenderer != null && armSprites.Length > 0)
            armSpriteRenderer.sprite = armSprites[ArmSpriteIndex];
        if (gloveSpriteRenderer != null && gloveSprites.Length > 0)
            gloveSpriteRenderer.sprite = gloveSprites[GloveSpriteIndex];
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            Debug.Log($"[BoxerController] {PlayerTag} grounded");
            if (transform.eulerAngles.z >= 0 && transform.eulerAngles.z <= 10 || transform.eulerAngles.z <= 350 && transform.eulerAngles.z >= 359)
            {
                Debug.Log($"Player Rotation : {transform.eulerAngles}");
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            Debug.Log($"[BoxerController] {PlayerTag} not grounded");
        }
    }

    public void RandomizeAppearance()
    {
        if (HasStateAuthority)
        {
            BodySpriteIndex = UnityEngine.Random.Range(0, bodySprites.Length);
            ArmSpriteIndex = UnityEngine.Random.Range(0, armSprites.Length);
            GloveSpriteIndex = UnityEngine.Random.Range(0, gloveSprites.Length);
            RPC_SyncAppearance(BodySpriteIndex, ArmSpriteIndex, GloveSpriteIndex);
        }
        Debug.Log($"[BoxerController] {PlayerTag} randomized appearance, indices: Body={BodySpriteIndex}, Arm={ArmSpriteIndex}, Glove={GloveSpriteIndex}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncAppearance(int bodyIndex, int armIndex, int gloveIndex)
    {
        BodySpriteIndex = bodyIndex;
        ArmSpriteIndex = armIndex;
        GloveSpriteIndex = gloveIndex;
        ApplyAppearance();
        Debug.Log($"[BoxerController] {PlayerTag} synced appearance, indices: Body={bodyIndex}, Arm={armIndex}, Glove={gloveIndex}");
    }

    public void SetInputEnabled(bool enabled)
    {
        IsInputEnabled = enabled;
        Debug.Log($"[BoxerController] {PlayerTag} input enabled: {enabled}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SetPositionAndScale(Vector3 position, Vector3 scale)
    {
        NetworkedPosition = position;
        NetworkedLocalScale = scale;
        transform.position = position;
        transform.localScale = scale;
        isFacingRight = scale.x > 0;
        inputKey = PlayerTag == "Player1" ? "space" : "return";
        Debug.Log($"[BoxerController] {PlayerTag} synced at {position}, scale: {scale}");
    }
    
    public void SetPositionAndScale(Vector3 position, Vector3 scale)
    {
        NetworkedPosition = position;
        NetworkedLocalScale = scale;
        transform.position = position;
        transform.localScale = scale;
        isFacingRight = scale.x > 0;
        inputKey = PlayerTag == "Player1" ? "space" : "return";
        Debug.Log($"[BoxerController] {PlayerTag} synced at {position}, scale: {scale}");
    }

    public void ResetRigidbodies()
    {
        if (bodyRigidbody != null)
        {
            bodyRigidbody.linearVelocity = Vector2.zero;
            bodyRigidbody.angularVelocity = 0f;
        }
        if (armRigidbody != null)
        {
            armRigidbody.linearVelocity = Vector2.zero;
            armRigidbody.angularVelocity = 0f;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position + transform.rotation * centerMass, 0.1f);

        //Draw Ground Check
        Gizmos.color = Color.blue;
        Vector2 origin = bodyRigidbody.position + Vector2.up * groundCheckOffset;
        Gizmos.DrawLine(origin, origin + groundCheckDistance * Vector2.down);
    }
}

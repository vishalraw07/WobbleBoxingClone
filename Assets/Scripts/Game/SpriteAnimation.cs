using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimation : MonoBehaviour
{
    [SerializeField] private Sprite[] sprites; // Array of sprites for animation
    [SerializeField] private float frameRate = 0.1f; // Time between frames in seconds

    private SpriteRenderer spriteRenderer;
    private int currentSpriteIndex = 0;
    private float timer = 0f;

    void Start()
    {
        // Get the SpriteRenderer component
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Check if sprites array is not empty
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning("Sprite array is empty. Please assign sprites in the Inspector.");
            enabled = false; // Disable the script if no sprites are assigned
        }
    }

    void Update()
    {
        // Update timer
        timer += Time.deltaTime;

        // Check if it's time to change to the next sprite
        if (timer >= frameRate)
        {
            // Move to the next sprite
            currentSpriteIndex = (currentSpriteIndex + 1) % sprites.Length;
            spriteRenderer.sprite = sprites[currentSpriteIndex];
            
            // Reset timer
            timer = 0f;
        }
    }
}
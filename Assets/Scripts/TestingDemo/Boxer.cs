using UnityEngine;
using System.Collections;

public class Boxer : MonoBehaviour
{
    [SerializeField] private Rigidbody2D myRb;
    [SerializeField] private Vector2 centerMass;
    [SerializeField] private float jumpForce;
    [SerializeField] private Transform foot;

    void Start()
    {
    }

    private bool isGrounded;

    void Update()
    {
        myRb.centerOfMass = centerMass;
        myRb.WakeUp();

        // Simple ground check using raycast
        // var raycast = Physics2D.Raycast(foot.position, Vector2.down, 0.1f);
        // isGrounded = raycast.collider != null && raycast.collider.CompareTag("Finish");
        // Debug.DrawRay(foot.position, Vector2.down, Color.red, 0.1f);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            myRb.AddForce(transform.up * jumpForce, ForceMode2D.Impulse);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position + transform.rotation * centerMass, 0.3f);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            Debug.Log($"[Boxer] Grounded - Position: { myRb.position}, Contacts: {collision.contactCount}, Collider: {collision.gameObject.name}");
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
            Debug.Log($"[Boxer] Not Grounded - Position: {myRb.position}");
        }
    }
}
using UnityEngine;

public interface IBoxer
{
    public const float HIT_COOLDOWN = 0.5f;
    public ShadowFollow Shadow { get; set; }
    public Vector3 FeetPosition { get; }
    public string PlayerTag { get; }
    public Collider2D GloveCollider { get; }
}

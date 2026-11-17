using UnityEngine;

public class ShortstuffRagdoll : MonoBehaviour
{
    // REFERENCES
    [SerializeField] private Rigidbody2D torso;
    [SerializeField] private Rigidbody2D[] bodyParts;

    [SerializeField] private float blastSpin = 250f;

    // SETUP

    private void Start()
    {
        Activate(Vector2.zero, new Vector2(5f, 10f), transform.position + Vector3.left
        );
    }

    public void Activate(Vector2 initialVelocity, Vector2 explosionImpulse, Vector2 explosionOrigin)
    {
        // Transfer the player's existing velocity
        foreach (Rigidbody2D part in bodyParts)
        {
            if (part != null)
            {
                part.linearVelocity = initialVelocity;
            }
        }

        // Apply the explosion force to the chest
        torso.AddForce(explosionImpulse, ForceMode2D.Impulse);

        // Choose a spin direction based on the explosion position
        float spinDirection = torso.position.x >= explosionOrigin.x ? -1f : 1f;

        // Rotate the chest
        torso.angularVelocity = spinDirection * blastSpin;
    }
}
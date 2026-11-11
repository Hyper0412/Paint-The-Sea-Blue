using UnityEngine;

// Put this on the claw. It just tells the arm what it touched.
// The claw needs: a Collider2D (Is Trigger ON) + a Rigidbody2D (Body Type: Kinematic).
public class ClawHand : MonoBehaviour
{
    public ClawArm arm;

    void OnTriggerEnter2D(Collider2D other)
    {
        arm.HandleHit(other);
    }
}

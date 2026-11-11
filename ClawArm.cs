using System;
using UnityEngine;

// The arm under the boat: rotates with the lever, shoots out on button press,
// grabs trash and brings it back to the boat.
public class ClawArm : MonoBehaviour
{
    [Header("References")]
    public ArmInputSource input;   // KeyboardMouseInput for now
    public Transform pivot;        // empty object at the arm's base; this is what rotates
    public Transform hand;         // the claw object, child of pivot
    public Transform grabPoint;    // optional: spot inside the claw where trash sits (child of hand)

    [Header("Visuals")]
    public SpriteRenderer bar;            // striped bar, child of pivot, Draw Mode = Tiled
    public SpriteRenderer clawRenderer;   // the claw's SpriteRenderer
    public Sprite clawOpen;
    public Sprite clawClosed;
    [Tooltip("Extra bar length so it tucks into the claw. Raise it until the gap disappears.")]
    public float barOverlap = 0.2f;

    [Header("Aiming (0 = straight down, + = right)")]
    public float minAngle = -85f;
    public float maxAngle = 85f;
    [Tooltip("Higher = snappier. Smooths out a jittery sensor later too.")]
    public float aimSmoothing = 12f;
    public bool invertLever = false;

    [Header("Launch")]
    public float restDistance = 0.6f;   // how far the claw sits from the pivot when idle
    public float maxReach = 6f;         // extra distance the claw can shoot out
    public float extendSpeed = 12f;
    public float retractSpeed = 9f;
    public float retractSpeedWithTrash = 5f;
    [Tooltip("How 'wide' the claw is when checking for hits. 0 = use the Hand's CircleCollider2D size.")]
    public float clawHitRadius = 0f;
    [Tooltip("Print everything the claw touches in the Console (for debugging). Turn off when done.")]
    public bool logHits = true;

    [Header("Limits")]
    [Tooltip("The claw turns back when it gets this close to the screen's left/right/bottom edge (0.05 = 5% of the screen)")]
    [Range(0f, 0.3f)] public float screenEdgeMargin = 0.05f;

    // Other scripts (score, water color, energy, QTE) will listen to these in later steps.
    public event Action<Transform> OnTrashDelivered;
    public event Action<Transform> OnHitFish;
    public event Action<Transform> OnHitTrappedAnimal;
    public event Action<Transform> OnHitSubmarine;

    enum State { Aiming, Extending, Retracting, Holding }
    State state = State.Aiming;

    float currentAngle;
    float currentLength;
    Transform carried;
    Camera cam;

    public bool IsBusy => state != State.Aiming;

    // Every ACTIVE boat in the scene, sorted left to right (player 1 = leftmost boat).
    // Switch a boat's GameObject off and it's simply not in the game.
    public static ClawArm[] FindAllInScene()
    {
        var all = FindObjectsByType<ClawArm>(FindObjectsSortMode.None);
        Array.Sort(all, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        return all;
    }

    void Start()
    {
        currentAngle = 0f;
        ApplyPose();
    }

    void Update()
    {
        switch (state)
        {
            case State.Aiming:
                float lever = input.GetLever01();
                if (invertLever) lever = 1f - lever;
                float target = Mathf.Lerp(minAngle, maxAngle, lever);
                currentAngle = Mathf.Lerp(currentAngle, target, aimSmoothing * Time.deltaTime);

                if (input.GetButtonDown())
                    state = State.Extending;
                break;

            case State.Extending:
                Vector2 from = hand.position;
                currentLength = Mathf.Min(currentLength + extendSpeed * Time.deltaTime, maxReach);
                ApplyPose();
                SweepForHits(from, hand.position);   // catches anything the claw passed over this frame

                if (state == State.Extending && IsNearScreenEdge(hand.position))
                    StartRetract(); // don't leave the screen

                if (state == State.Extending && currentLength >= maxReach)
                    StartRetract(); // missed, come back empty
                break;

            case State.Retracting:
                float speed = carried ? retractSpeedWithTrash : retractSpeed;
                currentLength -= speed * Time.deltaTime;
                if (currentLength <= 0f)
                {
                    currentLength = 0f;
                    FinishRetract();
                }
                break;
        }

        ApplyPose();
    }

    void ApplyPose()
    {
        pivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

        float dist = restDistance + currentLength;
        hand.localPosition = Vector3.down * dist;

        // Stretch the bar so it always reaches the back of the claw.
        // Tiled draw mode repeats the stripes instead of stretching them.
        if (bar != null)
        {
            Vector2 size = bar.size;
            size.x = (dist + barOverlap) / bar.transform.localScale.x;
            bar.size = size;
        }
    }

    // Checks the whole path the claw moved along this frame, so a fast claw
    // can't skip over thin trash like bottles.
    void SweepForHits(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.0001f) return;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(from, GetClawRadius(), delta / distance, distance);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); // nearest first

        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(hand)) continue; // ignore the claw itself
            HandleHit(h.collider);
            if (state != State.Extending) break; // grabbed or hit something, stop checking
        }
    }

    bool IsNearScreenEdge(Vector3 worldPos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(worldPos);
        return v.x < screenEdgeMargin || v.x > 1f - screenEdgeMargin || v.y < screenEdgeMargin;
    }

    float GetClawRadius()
    {
        if (clawHitRadius > 0f) return clawHitRadius;
        var circle = hand.GetComponent<CircleCollider2D>();
        if (circle != null)
            return circle.radius * Mathf.Max(Mathf.Abs(hand.lossyScale.x), Mathf.Abs(hand.lossyScale.y));
        return 0.3f;
    }

    // Called by RescueQTE when the rescue is finished (success or fail).
    public void ReleaseHold()
    {
        if (state == State.Holding) StartRetract();
    }

    void StartRetract()
    {
        if (clawRenderer != null && clawClosed != null)
            clawRenderer.sprite = clawClosed;
        state = State.Retracting;
    }

    // Called by ClawHand when the claw touches something.
    public void HandleHit(Collider2D other)
    {
        if (state != State.Extending) return; // only react on the way out

        if (logHits)
            Debug.Log($"Claw touched '{other.name}'  tag='{other.tag}'  layer={LayerMask.LayerToName(other.gameObject.layer)}", other);

        if (other.CompareTag("Trash"))
        {
            carried = other.transform;
            other.enabled = false;                 // can't be grabbed twice
            Transform holder = grabPoint != null ? grabPoint : hand;
            carried.SetParent(holder, true);
            carried.localPosition = Vector3.zero;  // snap into the claw
            StartRetract();
        }
        else if (other.CompareTag("Fish"))
        {
            Debug.Log("Hit a clean fish! (energy loss comes in a later step)");
            OnHitFish?.Invoke(other.transform);
            StartRetract();
        }
        else if (other.CompareTag("TrappedAnimal"))
        {
            if (OnHitTrappedAnimal != null)
            {
                // Hold on to the animal until the rescue QTE says we're done.
                if (clawRenderer != null && clawClosed != null) clawRenderer.sprite = clawClosed;
                state = State.Holding;
                OnHitTrappedAnimal.Invoke(other.transform);
            }
            else
            {
                StartRetract(); // no QTE in the scene, just bounce off
            }
        }
        else if (other.CompareTag("Submarine"))
        {
            OnHitSubmarine?.Invoke(other.transform);
            StartRetract();
        }
        else if (other.CompareTag("ClawBlocker"))
        {
            StartRetract(); // off-limits area (e.g. over the Energy bar)
        }
    }

    void FinishRetract()
    {
        if (carried != null)
        {
            OnTrashDelivered?.Invoke(carried);
            Debug.Log("Trash collected: " + carried.name);
            Destroy(carried.gameObject);
            carried = null;
        }
        if (clawRenderer != null && clawOpen != null)
            clawRenderer.sprite = clawOpen;
        state = State.Aiming;
    }
}
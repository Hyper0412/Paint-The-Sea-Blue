using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Random submarine attack during the trash phase:
// warning (red bar slides in, danger sign + hand/button flash) -> sub swims in on a lane
// -> drops trash in front of it (and more over time) -> hit it 3 times with the claw -> it swims away.
// Stops for good once the sea is clean, so it never interrupts the boss.
public class SubmarineEvent : MonoBehaviour
{
    [Header("Connections")]
    public TrashManager trashManager;
    [Tooltip("Filled automatically with every active boat. No need to drag anything in.")]
    public ClawArm[] arms;

    [Header("Where it appears")]
    [Tooltip("Empty objects where the sub stops. Add BOTH sides; only lanes on the side of an active boat are used.")]
    public Transform[] lanes;
    [Tooltip("How far off-screen it starts and leaves to (it comes in from the lane's side of the screen)")]
    public float offscreenDistance = 12f;

    [Header("Timing")]
    public float firstDelay = 25f;
    [Tooltip("Random wait (min, max seconds) after a sub leaves before the next one")]
    public Vector2 timeBetween = new Vector2(30f, 45f);
    [Tooltip("Max visits per game. 0 = no limit")]
    public int maxVisits = 0;
    [Tooltip("Random warning length (min, max seconds)")]
    public Vector2 warningTime = new Vector2(3f, 5f);

    [Header("Warning")]
    public GameObject warningRoot;       // parent of the red bar, danger sign and hand/button
    [Tooltip("ON: warning shows in the middle of the screen (players can't tell which side). OFF: above the lane.")]
    public bool warningAtScreenCenter = true;
    [Tooltip("Nudge from the screen center (or from the lane when the box above is off)")]
    public Vector3 warningOffset = Vector3.zero;
    public Transform warningBar;         // the red bar (slides in from the left, out to the right)
    public float barSlideDistance = 20f;
    public float barSlideTime = 0.4f;
    [Tooltip("Danger sign on the LEFT side of the warning")]
    public SpriteRenderer dangerSignLeft;
    [Tooltip("Danger sign on the RIGHT side of the warning")]
    public SpriteRenderer dangerSignRight;
    [Tooltip("Part of the warning where BOTH signs flash. After that, only the sub's side flashes. 0.5 = first half")]
    [Range(0f, 1f)] public float bothSidesPart = 0.5f;
    [Tooltip("How see-through a flashing sign is in its 'off' moment")]
    [Range(0f, 1f)] public float dimAlpha = 0.25f;
    [Tooltip("The sign on the side the sub is NOT coming from: 1 = fully visible (steady), 0 = hidden")]
    [Range(0f, 1f)] public float otherSideAlpha = 1f;
    public SpriteRenderer handButton;    // swaps between the frames below
    public Sprite[] handFrames;          // e.g. [0] hand above button, [1] hand pressing
    [Tooltip("Flashes per second")]
    public float flashSpeed = 3f;

    [Header("Submarine")]
    public Transform sub;                // tag "Submarine" + Collider2D (Is Trigger)
    public SpriteRenderer subRenderer;
    public Sprite subNormal;
    public Sprite subHit;                // the pink one
    [Tooltip("Tick if the sub drawing faces LEFT (nose on the left)")]
    public bool subArtFacesLeft = true;
    public float swimTime = 1.5f;
    public int hitsToKick = 3;
    public float hitFlashTime = 0.3f;
    public float hitShake = 0.2f;
    public float bobHeight = 0.1f;
    public float bobSpeed = 1.5f;

    [Header("Trash it drops")]
    [Tooltip("Child of the sub, placed just in front of its nose")]
    public Transform dropPoint;
    public float dropRadius = 1.5f;
    public int dropOnArrival = 3;
    [Tooltip("Drops 1 more trash this often while it stays. 0 = only on arrival")]
    public float dropInterval = 6f;

    [Header("Testing")]
    [Tooltip("Press to call a submarine right away")]
    public KeyCode testKey = KeyCode.N;

    Camera cam;
    // Own random generator, seeded from the clock, so the first side differs every game.
    readonly System.Random sideRandom = new System.Random(System.Environment.TickCount);
    Collider2D[] subColliders;
    Vector3 barHome;
    Vector3 subBaseScale;
    bool busy;
    bool present;
    bool stopped;
    int hits;
    int visits;
    float flashTimer;
    float shakeTimer;

    void Awake()
    {
        arms = ClawArm.FindAllInScene();
        cam = Camera.main;
        subColliders = sub.GetComponentsInChildren<Collider2D>();
        subBaseScale = sub.localScale;
        barHome = warningBar.localPosition;
        warningRoot.SetActive(false);
        sub.gameObject.SetActive(false);
    }

    void Start()
    {
        foreach (var arm in arms)
            if (arm != null) arm.OnHitSubmarine += HandleHit;
        trashManager.OnAllTrashCleared += StopEvents;
        StartCoroutine(MainLoop());
    }

    void OnDestroy()
    {
        foreach (var arm in arms)
            if (arm != null) arm.OnHitSubmarine -= HandleHit;
        if (trashManager != null) trashManager.OnAllTrashCleared -= StopEvents;
    }

    void Update()
    {
        if (Input.GetKeyDown(testKey) && !busy && !stopped) StartCoroutine(Visit());
    }

    IEnumerator MainLoop()
    {
        yield return new WaitForSeconds(firstDelay);
        while (!stopped && (maxVisits == 0 || visits < maxVisits))
        {
            while (busy) yield return null;          // a test visit may be running
            if (stopped) yield break;
            yield return Visit();
            yield return new WaitForSeconds(Random.Range(timeBetween.x, timeBetween.y));
        }
    }

    IEnumerator Visit()
    {
        busy = true;
        visits++;

        Transform lane = PickLane();
        bool fromLeft = lane.position.x < cam.transform.position.x;
        Vector3 inside = lane.position;
        Vector3 outside = inside + (fromLeft ? Vector3.left : Vector3.right) * offscreenDistance;

        // 1. Warning
        yield return ShowWarning(lane, fromLeft);
        if (stopped) { busy = false; yield break; }

        // 2. Swim in (facing the way it moves)
        hits = 0;
        SetSubColliders(false);
        Face(fromLeft ? 1 : -1);
        subRenderer.sprite = subNormal;
        sub.position = outside;
        sub.gameObject.SetActive(true);
        yield return MoveSub(outside, inside, swimTime);

        // 3. Stay, drop trash, wait for 3 hits
        present = true;
        SetSubColliders(true);
        DropTrash(dropOnArrival);
        float dropTimer = dropInterval;
        float bobT = 0f;

        while (hits < hitsToKick && !stopped)
        {
            bobT += Time.deltaTime;
            Vector3 bob = Vector3.up * Mathf.Sin(bobT * bobSpeed) * bobHeight;
            Vector3 shake = Vector3.zero;
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.deltaTime;
                shake = (Vector3)(Random.insideUnitCircle * hitShake * (shakeTimer / hitFlashTime));
            }
            sub.position = inside + bob + shake;

            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) subRenderer.sprite = subNormal;
            }

            if (dropInterval > 0f)
            {
                dropTimer -= Time.deltaTime;
                if (dropTimer <= 0f) { DropTrash(1); dropTimer = dropInterval; }
            }
            yield return null;
        }

        // 4. Kicked out (or sea is clean): swim away the way it came
        present = false;
        SetSubColliders(false);
        yield return new WaitForSeconds(hitFlashTime);
        subRenderer.sprite = subNormal;
        Face(fromLeft ? -1 : 1);
        yield return MoveSub(sub.position, outside, swimTime * 0.7f);
        sub.gameObject.SetActive(false);

        busy = false;
    }

    // Only uses lanes on a side of the screen where an active boat is.
    // Single player (yellow boat off) = only player 1's side. Co-op = both sides.
    Transform PickLane()
    {
        float camX = cam.transform.position.x;
        var valid = new List<Transform>();
        foreach (var lane in lanes)
        {
            if (lane == null) continue;
            bool laneOnLeft = lane.position.x < camX;
            foreach (var arm in arms)
            {
                if (arm != null && (arm.transform.position.x < camX) == laneOnLeft)
                {
                    valid.Add(lane);
                    break;
                }
            }
        }
        if (valid.Count == 0)
            foreach (var lane in lanes) if (lane != null) valid.Add(lane);
        return valid[sideRandom.Next(valid.Count)];
    }

    void HandleHit(Transform hit)
    {
        if (!present || !hit.IsChildOf(sub)) return;
        hits++;
        flashTimer = hitFlashTime;
        shakeTimer = hitFlashTime;
        if (subHit != null) subRenderer.sprite = subHit;
        Debug.Log($"Submarine hit! {hits}/{hitsToKick}");
    }

    void StopEvents()
    {
        stopped = true;   // a sub that's still here swims away on its own
    }

    IEnumerator ShowWarning(Transform lane, bool fromLeft)
    {
        Vector3 anchor = lane.position;
        if (warningAtScreenCenter)
            anchor = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        warningRoot.transform.position = anchor + warningOffset;
        warningRoot.SetActive(true);

        Vector3 left = barHome + Vector3.left * barSlideDistance;
        Vector3 right = barHome + Vector3.right * barSlideDistance;
        yield return SlideBar(left, barHome);

        float duration = Random.Range(warningTime.x, warningTime.y);
        for (float t = 0f; t < duration && !stopped; t += Time.deltaTime)
        {
            bool on = Mathf.Repeat(t * flashSpeed, 1f) < 0.5f;
            bool bothPhase = t < duration * bothSidesPart;
            float flashAlpha = on ? 1f : dimAlpha;

            // First both signs flash, then only the side the sub is coming from.
            SetSignAlpha(dangerSignLeft, (bothPhase || fromLeft) ? flashAlpha : otherSideAlpha);
            SetSignAlpha(dangerSignRight, (bothPhase || !fromLeft) ? flashAlpha : otherSideAlpha);
            if (handButton != null && handFrames.Length > 0)
                handButton.sprite = handFrames[on ? 0 : Mathf.Min(1, handFrames.Length - 1)];
            yield return null;
        }

        yield return SlideBar(barHome, right);
        warningRoot.SetActive(false);
        warningBar.localPosition = barHome;
    }

    void SetSignAlpha(SpriteRenderer sign, float alpha)
    {
        if (sign == null) return;
        Color c = sign.color;
        c.a = alpha;
        sign.color = c;
    }

    IEnumerator SlideBar(Vector3 from, Vector3 to)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / barSlideTime)
        {
            warningBar.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        warningBar.localPosition = to;
    }

    IEnumerator MoveSub(Vector3 from, Vector3 to, float duration)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            sub.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        sub.position = to;
    }

    // dir: 1 = moving right, -1 = moving left. Flips the whole sub (and its DropPoint).
    void Face(int dir)
    {
        bool movingRight = dir > 0;
        bool artFacesRight = !subArtFacesLeft;
        Vector3 s = subBaseScale;
        s.x = Mathf.Abs(s.x) * (movingRight == artFacesRight ? 1f : -1f);
        sub.localScale = s;
    }

    void DropTrash(int count)
    {
        Vector2 center = dropPoint != null ? (Vector2)dropPoint.position : (Vector2)sub.position;
        for (int i = 0; i < count; i++)
            trashManager.AddExtraTrash(center + Random.insideUnitCircle * dropRadius);
    }

    void SetSubColliders(bool on)
    {
        foreach (var c in subColliders) c.enabled = on;
    }
}
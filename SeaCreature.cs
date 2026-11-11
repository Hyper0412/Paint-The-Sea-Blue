using System.Collections;
using UnityEngine;

// Makes a fish or turtle swim back and forth, bob up and down, and turn around at the ends.
// Works for clean fish AND trapped animals (the QTE step will freeze trapped ones while you free them).
public class SeaCreature : MonoBehaviour
{
    [Header("Swimming")]
    public float speed = 1f;
    [Tooltip("How far left/right of its starting spot it swims (yellow line in Scene view)")]
    public float swimRange = 4f;
    [Tooltip("Tick if your drawing faces RIGHT. Untick if it faces left.")]
    public bool artFacesRight = true;
    public bool startMovingRight = true;

    [Header("Bobbing")]
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;

    [Header("When the claw bumps it")]
    public float startleSpeedMultiplier = 3f;
    public float startleTime = 0.8f;
    public Color startleColor = new Color(1f, 0.5f, 0.5f);

    [Tooltip("Turned off by the QTE so the animal holds still")]
    public bool canSwim = true;

    [Header("Trapped animals only")]
    [Tooltip("Optional: net/bag CHILD objects to hide when freed")]
    public GameObject[] trapVisuals;
    [Tooltip("Optional: a sprite WITHOUT the net to switch to when freed")]
    public Sprite freedSprite;
    public float freedSpeedMultiplier = 2.5f;

    public bool IsFreed { get; private set; }

    Vector3 startPos;
    int dir;
    float bobOffset;
    float baseScaleX;
    float startleTimer;
    bool tinted;
    SpriteRenderer[] renderers;
    Color[] originalColors;

    void Start()
    {
        startPos = transform.position;
        dir = startMovingRight ? 1 : -1;
        bobOffset = Random.value * 10f;               // so fish don't all bob in sync
        speed *= Random.Range(0.85f, 1.15f);          // small speed variety
        baseScaleX = Mathf.Abs(transform.localScale.x);

        renderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].color;

        FaceDirection();
    }

    void Update()
    {
        UpdateStartleTint();
        if (!canSwim) return;

        float currentSpeed = speed * (startleTimer > 0f ? startleSpeedMultiplier : 1f);
        Vector3 p = transform.position;
        p.x += dir * currentSpeed * Time.deltaTime;

        if (p.x > startPos.x + swimRange) { p.x = startPos.x + swimRange; dir = -1; FaceDirection(); }
        else if (p.x < startPos.x - swimRange) { p.x = startPos.x - swimRange; dir = 1; FaceDirection(); }

        p.y = startPos.y + Mathf.Sin(Time.time * bobSpeed + bobOffset) * bobHeight;
        transform.position = p;
    }

    void FaceDirection()
    {
        // Flipping the scale also flips children (nets, bags) and the collider.
        Vector3 s = transform.localScale;
        bool movingRight = dir > 0;
        s.x = baseScaleX * (movingRight == artFacesRight ? 1f : -1f);
        transform.localScale = s;
    }

    // Called when the claw hits a clean fish: flashes and darts off quickly.
    public void Startle()
    {
        startleTimer = startleTime;
    }

    // Called by RescueQTE on success: net comes off, it swims away and fades out.
    public void Free(float fadeTime)
    {
        if (IsFreed) return;
        IsFreed = true;
        StartCoroutine(FreeRoutine(fadeTime));
    }

    IEnumerator FreeRoutine(float fadeTime)
    {
        foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false; // can't be hit again
        foreach (var go in trapVisuals) if (go != null) go.SetActive(false);

        var mainRenderer = GetComponent<SpriteRenderer>();
        if (freedSprite != null && mainRenderer != null) mainRenderer.sprite = freedSprite;

        startleTimer = 0f;
        canSwim = true;
        speed *= freedSpeedMultiplier;

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(t / fadeTime);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = originalColors[i];
                c.a *= alpha;
                renderers[i].color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    void UpdateStartleTint()
    {
        if (startleTimer > 0f)
        {
            startleTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(startleTimer / startleTime);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].color = Color.Lerp(originalColors[i], startleColor, t);
            tinted = true;
        }
        else if (tinted)
        {
            for (int i = 0; i < renderers.Length; i++) renderers[i].color = originalColors[i];
            tinted = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? startPos : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center + Vector3.left * swimRange, center + Vector3.right * swimRange);
    }
}
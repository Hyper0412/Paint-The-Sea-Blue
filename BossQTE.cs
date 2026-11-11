using System;
using UnityEngine;

// Boss quick-time bar (1 or 2 players).
// A green zone wanders along the bar by itself. Each player's lever moves their own marker.
// HOLD the button while your marker is inside the zone to fill progress.
// 2 players: full speed only when BOTH are in the zone and holding; half speed with one.
// Fill it once per round; after all rounds, the boss is defeated.
public class BossQTE : MonoBehaviour
{
    [Header("Visuals")]
    public GameObject visual;          // child holding all bar parts (shown/hidden)
    public Transform background;       // the bar; its Scale X = bar width
    public Transform zone;             // green zone (moves by itself)
    [Tooltip("One marker per player: [0] = left boat (player 1), [1] = right boat (player 2)")]
    public Transform[] markers;
    public Transform progressFill;     // fills up while players hold in the zone
    [Tooltip("ON: the pink bar sits under the green zone and moves with it. OFF: it fills across the whole bar.")]
    public bool fillFollowsZone = true;
    [Tooltip("Optional: little dots/icons, one per round, that light up when a round is done")]
    public SpriteRenderer[] roundPips;
    public Color pipDoneColor = new Color(0.4f, 1f, 0.4f);
    public Color zoneIdleColor = new Color(0.2f, 0.8f, 0.3f);
    public Color zoneActiveColor = new Color(0.7f, 1f, 0.4f);

    [Header("Rounds")]
    public int rounds = 4;
    public float pauseBetweenRounds = 1f;

    [Header("Difficulty")]
    [Range(0.1f, 0.6f)] public float startZoneSize = 0.3f;
    [Tooltip("Zone gets this much smaller every round")]
    public float zoneShrinkPerRound = 0.04f;
    [Tooltip("How fast the zone wanders around")]
    public float startZoneSpeed = 0.4f;
    public float zoneSpeedUpPerRound = 0.15f;
    [Tooltip("Progress per second when ALL players hold in the zone (0.35 = about 3 seconds)")]
    public float fillRate = 0.35f;
    [Tooltip("Progress lost per second when nobody is filling")]
    public float drainRate = 0.2f;
    [Tooltip("Smooths the markers so a jittery sensor doesn't shake them")]
    public float markerSmoothing = 15f;

    public event Action<int> OnRoundComplete;   // passes how many rounds are done
    public event Action OnAllRoundsComplete;

    ArmInputSource[] inputs;
    float[] markerPos;
    SpriteRenderer zoneRenderer;
    Color[] pipStartColors;
    bool running;
    int round;
    float progress;
    float zoneSize, zoneSpeed;
    float noiseT, noiseSeed;
    float barWidth;
    float pauseTimer;
    float currentZoneCenter = 0.5f;

    void Awake()
    {
        // Hide the bar FIRST, so it's hidden even if a slot below is missing.
        if (visual != null) visual.SetActive(false);
        else Debug.LogError("BossQTE: the 'Visual' slot is empty!", this);

        if (background == null || zone == null || markers == null || markers.Length == 0)
            Debug.LogError("BossQTE: fill in Background, Zone and at least one Marker!", this);

        if (zone != null) zoneRenderer = zone.GetComponent<SpriteRenderer>();

        pipStartColors = new Color[roundPips.Length];
        for (int i = 0; i < roundPips.Length; i++)
            if (roundPips[i] != null) pipStartColors[i] = roundPips[i].color;
    }

    public void Begin(ArmInputSource[] sources)
    {
        inputs = sources;
        markerPos = new float[inputs.Length];
        for (int i = 0; i < markerPos.Length; i++) markerPos[i] = 0.5f;

        // Show one marker per player, hide the rest.
        for (int i = 0; i < markers.Length; i++)
            if (markers[i] != null) markers[i].gameObject.SetActive(i < inputs.Length);

        barWidth = background.localScale.x;
        round = 0;
        for (int i = 0; i < roundPips.Length; i++)
            if (roundPips[i] != null) roundPips[i].color = pipStartColors[i];

        SetupRound();
        running = true;
        visual.SetActive(true);
    }

    void SetupRound()
    {
        zoneSize = Mathf.Max(0.1f, startZoneSize - zoneShrinkPerRound * round);
        zoneSpeed = startZoneSpeed + zoneSpeedUpPerRound * round;
        noiseSeed = UnityEngine.Random.Range(0f, 100f);
        noiseT = 0f;
        progress = 0f;
        pauseTimer = pauseBetweenRounds;
        UpdateFill();
    }

    void Update()
    {
        if (!running) return;

        // Markers always follow the levers, even during the pause, so players can get ready.
        int players = Mathf.Min(inputs.Length, markers.Length);
        for (int i = 0; i < players; i++)
        {
            markerPos[i] = Mathf.Lerp(markerPos[i], inputs[i].GetLever01(), markerSmoothing * Time.deltaTime);
            if (markers[i] != null) SetX(markers[i], markerPos[i]);
        }

        if (pauseTimer > 0f) { pauseTimer -= Time.deltaTime; }
        else noiseT += Time.deltaTime * zoneSpeed;

        // Zone wanders smoothly using Perlin noise.
        float n = Mathf.PerlinNoise(noiseT, noiseSeed);
        n = Mathf.Clamp01(Mathf.InverseLerp(0.2f, 0.8f, n));
        float half = zoneSize * 0.5f;
        float zoneCenter = Mathf.Lerp(half, 1f - half, n);

        Vector3 zs = zone.localScale;
        zs.x = zoneSize * barWidth;
        zone.localScale = zs;
        SetX(zone, zoneCenter);
        currentZoneCenter = zoneCenter;
        UpdateFill();

        if (pauseTimer > 0f || players == 0) return;

        // Count how many players are holding inside the zone.
        int filling = 0;
        for (int i = 0; i < players; i++)
        {
            bool inZone = Mathf.Abs(markerPos[i] - zoneCenter) <= half;
            if (inZone && inputs[i].GetButton()) filling++;
        }

        if (filling > 0) progress += fillRate * filling / players * Time.deltaTime;
        else progress -= drainRate * Time.deltaTime;
        progress = Mathf.Clamp01(progress);
        UpdateFill();

        if (zoneRenderer != null) zoneRenderer.color = filling > 0 ? zoneActiveColor : zoneIdleColor;

        if (progress >= 1f) CompleteRound();
    }

    void CompleteRound()
    {
        if (round < roundPips.Length && roundPips[round] != null) roundPips[round].color = pipDoneColor;
        round++;
        OnRoundComplete?.Invoke(round);

        if (round >= rounds)
        {
            running = false;
            visual.SetActive(false);
            OnAllRoundsComplete?.Invoke();
        }
        else
        {
            SetupRound();
        }
    }

    // Grows the fill from a LEFT edge (works with Unity's built-in Square sprite).
    // Follow mode: left edge = the green zone's left edge, full = the zone's width.
    void UpdateFill()
    {
        if (progressFill == null) return;

        float width = fillFollowsZone ? zoneSize : 1f;                          // in 0..1 bar units
        float left = fillFollowsZone ? currentZoneCenter - zoneSize * 0.5f : 0f;
        float filled = progress * width;

        Vector3 s = progressFill.localScale;
        s.x = filled * barWidth;
        progressFill.localScale = s;

        Vector3 p = progressFill.localPosition;
        p.x = (left + filled * 0.5f - 0.5f) * barWidth;
        progressFill.localPosition = p;
    }

    // Places an object along the bar: 0 = left end, 1 = right end.
    void SetX(Transform t, float pos01)
    {
        Vector3 p = t.localPosition;
        p.x = (pos01 - 0.5f) * barWidth;
        t.localPosition = p;
    }
}
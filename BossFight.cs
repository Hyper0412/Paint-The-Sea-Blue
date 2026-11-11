using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs the boss phase:
// all trash cleared -> claws stop -> camera glides up -> boss rises from behind the sea
// -> boss QTE (4 rounds) -> boss sinks -> OnBossDefeated (win screen comes next).
public class BossFight : MonoBehaviour
{
    [Header("Connections")]
    public TrashManager trashManager;
    [Tooltip("Filled automatically with every active boat. Each boat's player gets a marker on the boss bar.")]
    public ClawArm[] arms;
    public BossQTE qte;

    [Header("Camera")]
    public Camera cam;                  // leave empty = Main Camera
    public float cameraTargetY = 5f;
    public float cameraMoveTime = 2f;

    [Header("Boss")]
    public Transform boss;              // place it HIDDEN behind the water in the scene
    public float riseHeight = 4f;       // how far it rises up
    public float riseTime = 2f;
    public float sinkTime = 2f;
    public float hitShakeAmount = 0.25f;
    public float hitShakeTime = 0.4f;

    [Header("Defeated look")]
    [Tooltip("Leave empty to use the SpriteRenderer on the Boss object")]
    public SpriteRenderer bossRenderer;
    [Tooltip("The pink 'beaten' boss sprite, shown after the last QTE round")]
    public Sprite bossDefeatedSprite;
    [Tooltip("Seconds the pink boss stays up before sinking")]
    public float pauseBeforeSink = 1.2f;

    [Header("Testing")]
    [Tooltip("Press this to jump straight to the boss while testing")]
    public KeyCode testStartKey = KeyCode.B;

    public event Action OnBossIntroStarted;   // camera starts moving up (for sound/music)
    public event Action OnBossAppeared;       // boss has risen, the bar starts
    public event Action OnBossDefeated;

    Vector3 bossHiddenPos;
    Vector3 bossShownPos;
    bool started;

    void Awake()
    {
        arms = ClawArm.FindAllInScene();
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (bossRenderer == null) bossRenderer = boss.GetComponent<SpriteRenderer>();
        bossHiddenPos = boss.position;
        bossShownPos = bossHiddenPos + Vector3.up * riseHeight;

        trashManager.OnAllTrashCleared += StartBoss;
        qte.OnRoundComplete += HandleRoundComplete;
        qte.OnAllRoundsComplete += HandleDefeated;
    }

    void OnDestroy()
    {
        if (trashManager != null) trashManager.OnAllTrashCleared -= StartBoss;
        if (qte != null)
        {
            qte.OnRoundComplete -= HandleRoundComplete;
            qte.OnAllRoundsComplete -= HandleDefeated;
        }
    }

    void Update()
    {
        if (!started && Input.GetKeyDown(testStartKey)) StartBoss();
    }

    public void StartBoss()
    {
        if (started) return;
        started = true;
        StartCoroutine(BossIntro());
    }

    IEnumerator BossIntro()
    {
        // Let any claw that's still out finish coming back, then switch the claws off.
        foreach (var arm in arms)
            while (arm != null && arm.IsBusy) yield return null;
        foreach (var arm in arms)
            if (arm != null) arm.enabled = false;

        OnBossIntroStarted?.Invoke();
        yield return MoveCamera(cameraTargetY, cameraMoveTime);
        yield return MoveBoss(bossHiddenPos, bossShownPos, riseTime);

        // Every player's controller joins the boss bar (left boat = marker 1, right boat = marker 2).
        var inputs = new List<ArmInputSource>();
        foreach (var arm in arms)
            if (arm != null && arm.input != null) inputs.Add(arm.input);
        qte.Begin(inputs.ToArray());
        OnBossAppeared?.Invoke();
    }

    void HandleRoundComplete(int roundsDone)
    {
        Debug.Log($"Boss hit! {roundsDone}/{qte.rounds}");
        StartCoroutine(ShakeBoss());
    }

    void HandleDefeated()
    {
        // Turn pink right on the final hit.
        if (bossRenderer != null && bossDefeatedSprite != null)
            bossRenderer.sprite = bossDefeatedSprite;
        StartCoroutine(BossOutro());
    }

    IEnumerator BossOutro()
    {
        yield return new WaitForSeconds(hitShakeTime + pauseBeforeSink);
        yield return MoveBoss(bossShownPos, bossHiddenPos, sinkTime);
        Debug.Log("Boss defeated! (win screen comes next)");
        OnBossDefeated?.Invoke();
    }

    IEnumerator MoveCamera(float targetY, float duration)
    {
        Vector3 start = cam.transform.position;
        Vector3 end = new Vector3(start.x, targetY, start.z);
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            cam.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        cam.transform.position = end;
    }

    IEnumerator MoveBoss(Vector3 from, Vector3 to, float duration)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            boss.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        boss.position = to;
    }

    IEnumerator ShakeBoss()
    {
        for (float t = 0f; t < hitShakeTime; t += Time.deltaTime)
        {
            float strength = hitShakeAmount * (1f - t / hitShakeTime);
            boss.position = bossShownPos + (Vector3)(UnityEngine.Random.insideUnitCircle * strength);
            yield return null;
        }
        boss.position = bossShownPos;
    }
}
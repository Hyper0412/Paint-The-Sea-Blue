using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Shows the endings:
//  END1 (good end)  -> after the boss is defeated
//  END2 (So close)  -> when the energy runs out
// Each screen is a full-screen UI Image with a CanvasGroup, so it can fade in.
// Restart for now: after a few seconds, any player's button reloads the game.
// (The full game loop will replace this restart later.)
public class EndScreens : MonoBehaviour
{
    [Header("Connections")]
    public BossFight bossFight;
    public EnergyBar energyBar;

    [Header("Screens (UI Image + CanvasGroup)")]
    public CanvasGroup goodEnd;   // END1: Nice job
    public CanvasGroup badEnd;    // END2: ...So close
    public float fadeTime = 1f;
    [Tooltip("Short pause after the last fish hit before END2 fades in")]
    public float delayBeforeBadEnd = 0.8f;

    [Header("Temporary restart")]
    public bool restartOnButton = true;
    [Tooltip("Seconds after the ending before a button press restarts (so nobody skips it by accident)")]
    public float restartAllowedAfter = 3f;

    public event Action<bool> OnEndingShown;   // true = good end (for the game loop later)

    ClawArm[] arms;
    bool ended;
    bool canRestart;

    void Awake()
    {
        arms = ClawArm.FindAllInScene();
        Hide(goodEnd);
        Hide(badEnd);
    }

    void Start()
    {
        if (bossFight != null) bossFight.OnBossDefeated += ShowGoodEnd;
        if (energyBar != null) energyBar.OnEnergyEmpty += ShowBadEnd;
    }

    void OnDestroy()
    {
        if (bossFight != null) bossFight.OnBossDefeated -= ShowGoodEnd;
        if (energyBar != null) energyBar.OnEnergyEmpty -= ShowBadEnd;
    }

    void ShowGoodEnd() => StartCoroutine(ShowEnding(goodEnd, true, 0f));
    void ShowBadEnd()  => StartCoroutine(ShowEnding(badEnd, false, delayBeforeBadEnd));

    IEnumerator ShowEnding(CanvasGroup screen, bool good, float delay)
    {
        if (ended) yield break;   // only one ending per game
        ended = true;

        foreach (var arm in arms)
            if (arm != null) arm.enabled = false;   // claws stop

        yield return new WaitForSeconds(delay);

        screen.gameObject.SetActive(true);
        for (float t = 0f; t < 1f; t += Time.deltaTime / fadeTime)
        {
            screen.alpha = t;
            yield return null;
        }
        screen.alpha = 1f;
        Debug.Log(good ? "Good end!" : "So close...");
        OnEndingShown?.Invoke(good);

        yield return new WaitForSeconds(restartAllowedAfter);
        canRestart = true;
    }

    void Update()
    {
        if (!canRestart || !restartOnButton) return;

        foreach (var arm in arms)
        {
            if (arm != null && arm.input != null && arm.input.GetButtonDown())
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
        }
    }

    void Hide(CanvasGroup screen)
    {
        if (screen == null) return;
        screen.alpha = 0f;
        screen.gameObject.SetActive(false);
    }
}

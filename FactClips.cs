using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// After an ending (good or bad): wait 4-6 s -> play a RANDOM fun fact clip.
// Idle loop: when a clip ends, wait (10 s). Nobody touched the console? -> next random clip.
// Someone presses a button (during a clip or the wait) -> back to the game.
// Never plays the same clip twice in a row (even across restarts).
[RequireComponent(typeof(VideoPlayer))]
public class FactClips : MonoBehaviour
{
    [Header("Connections")]
    public EndScreens endScreens;

    [Header("Clips")]
    [Tooltip("Your fact videos (fact1 ... fact7)")]
    public VideoClip[] clips;

    [Header("Screen")]
    [Tooltip("Full-screen RawImage on the Canvas (below END1/END2 in the Hierarchy, so it draws on top)")]
    public RawImage screen;
    [Tooltip("Optional: CanvasGroup on the RawImage, for fading")]
    public CanvasGroup screenGroup;
    public float fadeTime = 0.6f;

    [Header("Timing")]
    [Tooltip("Random wait (min, max seconds) on the ending screen before the clip starts")]
    public Vector2 waitAfterEnding = new Vector2(4f, 6f);
    [Tooltip("After a clip ends: seconds to wait for a touch before the next clip plays")]
    public float idleBeforeNextClip = 10f;
    [Tooltip("Ignore button presses for this long after a clip starts (so a leftover press doesn't skip it)")]
    public float touchGracePeriod = 1f;
    [Tooltip("Safety: move on if a clip runs longer than this (seconds)")]
    public float maxClipLength = 60f;

    [Header("Testing")]
    [Tooltip("Press to play a random clip right away")]
    public KeyCode testKey = KeyCode.F;

    static int lastClip = -1;   // survives scene reloads, so the next game picks a different clip

    VideoPlayer player;
    RenderTexture texture;
    ArmInputSource[] inputs;   // every player's controller = "the console"
    bool playing;
    bool finished;

    void Awake()
    {
        player = GetComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.renderMode = VideoRenderMode.RenderTexture;

        texture = new RenderTexture(1920, 1080, 0);
        player.targetTexture = texture;
        if (screen != null) screen.texture = texture;

        player.loopPointReached += _ => finished = true;
        player.errorReceived += (_, msg) => { Debug.LogError("Fact clip error: " + msg); finished = true; };

        ShowScreen(false, 0f);
    }

    void Start()
    {
        var arms = ClawArm.FindAllInScene();
        inputs = new ArmInputSource[arms.Length];
        for (int i = 0; i < arms.Length; i++) inputs[i] = arms[i].input;

        if (endScreens != null)
        {
            endScreens.restartOnButton = false;   // clips handle the restart now
            endScreens.OnEndingShown += HandleEnding;
        }
    }

    void OnDestroy()
    {
        if (endScreens != null) endScreens.OnEndingShown -= HandleEnding;
        if (texture != null) texture.Release();
    }

    void Update()
    {
        if (!playing && Input.GetKeyDown(testKey)) StartCoroutine(PlayClip(0f));
    }

    void HandleEnding(bool goodEnd)
    {
        float wait = Random.Range(waitAfterEnding.x, waitAfterEnding.y);
        StartCoroutine(PlayClip(wait));
    }

    IEnumerator PlayClip(float wait)
    {
        if (playing || clips.Length == 0) yield break;
        playing = true;

        yield return new WaitForSeconds(wait);

        bool firstClip = true;
        while (true)   // idle loop: keeps going until someone touches the console
        {
            player.clip = clips[PickClip()];
            finished = false;
            player.Prepare();   // the last frame of the previous clip stays on screen meanwhile
            while (!player.isPrepared && !finished) yield return null;

            finished = false;
            player.Play();
            if (firstClip)
            {
                yield return Fade(0f, 1f);   // only the first clip fades in over the end screen
                firstClip = false;
            }

            // Watch the clip. A touch skips straight back to the game.
            float t = 0f;
            while (!finished && t < maxClipLength)
            {
                t += Time.deltaTime;
                if (t > touchGracePeriod && AnyoneTouched()) { yield return BackToGame(); yield break; }
                yield return null;
            }

            // Clip ended: the last frame stays on screen while we wait for a touch.
            for (float w = 0f; w < idleBeforeNextClip; w += Time.deltaTime)
            {
                if (AnyoneTouched()) { yield return BackToGame(); yield break; }
                yield return null;
            }

            // Nobody came: go straight to another clip.
            // No fade, so the end screen never shows; the last frame stays up while the next one loads.
        }
    }

    bool AnyoneTouched()
    {
        foreach (var input in inputs)
            if (input != null && input.GetButtonDown()) return true;
        return false;
    }

    IEnumerator BackToGame()
    {
        yield return Fade(1f, 0f);
        player.Stop();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);   // back to the start
    }

    int PickClip()
    {
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == lastClip)
            i = (i + Random.Range(1, clips.Length)) % clips.Length;   // any other one
        lastClip = i;
        return i;
    }

    IEnumerator Fade(float from, float to)
    {
        ShowScreen(true, from);
        for (float t = 0f; t < 1f; t += Time.deltaTime / fadeTime)
        {
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetAlpha(to);
    }

    void ShowScreen(bool on, float alpha)
    {
        if (screen == null) return;
        screen.gameObject.SetActive(on);
        SetAlpha(alpha);
    }

    void SetAlpha(float a)
    {
        if (screenGroup != null) screenGroup.alpha = a;
        else if (screen != null)
        {
            Color c = screen.color;
            c.a = a;
            screen.color = c;
        }
        player.SetDirectAudioVolume(0, a);   // sound fades with the picture
    }
}
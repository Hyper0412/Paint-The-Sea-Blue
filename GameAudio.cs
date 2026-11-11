using System.Collections;
using UnityEngine;

// All the game's music and sound effects in one place.
// Music: game BGM loops -> fades out when the boss comes -> boss theme loops during the fight
//        -> all music fades out at an ending (so the fact clips can be heard).
// SFX:   random pickup sound per trash, turtle escape when an animal is freed, boss sounds.
// Just drag the sound files into the slots; leave any slot empty to skip that sound.
public class GameAudio : MonoBehaviour
{
    [Header("Connections")]
    public BossFight bossFight;
    public EnergyBar energyBar;

    [Header("Music (loops)")]
    public AudioClip gameMusic;   // game_bgm
    public AudioClip bossMusic;   // boss_bgm_bossfighttheme
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    public float musicFadeTime = 1.5f;

    [Header("Sound effects")]
    [Tooltip("One is picked at random each time trash is collected")]
    public AudioClip[] pickupSounds;   // game_sfx_pickupitem 1, 2, 3
    public AudioClip animalFreedSound; // game_sfx_turtleescape
    [Tooltip("Plays as the camera starts moving up to the boss")]
    public AudioClip bossIntroSound;   // boss_sfx_bossfightstart
    [Tooltip("Plays when the boss has risen and the fight begins")]
    public AudioClip bossAppearSound;  // boss_sfx_bossfightstart1
    [Tooltip("Plays when the boss is beaten (turns pink)")]
    public AudioClip bossDefeatedSound; // boss_sfx_bosssound
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Small random pitch change on pickups so repeats don't sound robotic")]
    [Range(0f, 0.3f)] public float pickupPitchVariation = 0.08f;

    AudioSource musicSource;
    AudioSource sfxSource;
    AudioSource pickupSource;   // separate, so pitch changes don't affect other sounds
    Coroutine musicRoutine;

    void Awake()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        pickupSource = gameObject.AddComponent<AudioSource>();
        pickupSource.playOnAwake = false;
    }

    void Start()
    {
        foreach (var arm in ClawArm.FindAllInScene())
            arm.OnTrashDelivered += _ => PlayPickup();

        foreach (var qte in FindObjectsByType<RescueQTE>(FindObjectsSortMode.None))
            qte.OnAnimalFreed += _ => PlaySfx(animalFreedSound);

        if (bossFight != null)
        {
            bossFight.OnBossIntroStarted += () =>
            {
                PlaySfx(bossIntroSound);
                SwitchMusic(null);           // game music fades out during the intro
            };
            bossFight.OnBossAppeared += () =>
            {
                PlaySfx(bossAppearSound);
                SwitchMusic(bossMusic);      // boss theme fades in
            };
            bossFight.OnBossDefeated += () => SwitchMusic(null);
        }

        var bossQte = FindAnyObjectByType<BossQTE>();
        if (bossQte != null) bossQte.OnAllRoundsComplete += () => PlaySfx(bossDefeatedSound);

        if (energyBar != null) energyBar.OnEnergyEmpty += () => SwitchMusic(null);

        SwitchMusic(gameMusic);
    }

    void PlayPickup()
    {
        if (pickupSounds == null || pickupSounds.Length == 0) return;
        var clip = pickupSounds[Random.Range(0, pickupSounds.Length)];
        if (clip == null) return;
        pickupSource.pitch = 1f + Random.Range(-pickupPitchVariation, pickupPitchVariation);
        pickupSource.PlayOneShot(clip, sfxVolume);
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // Fades the current music out, then the new one in. null = just fade out.
    void SwitchMusic(AudioClip next)
    {
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(FadeTo(next));
    }

    IEnumerator FadeTo(AudioClip next)
    {
        float start = musicSource.volume;
        if (musicSource.isPlaying)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / musicFadeTime)
            {
                musicSource.volume = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
            musicSource.Stop();
        }

        if (next == null) yield break;

        musicSource.clip = next;
        musicSource.volume = 0f;
        musicSource.Play();
        for (float t = 0f; t < 1f; t += Time.deltaTime / musicFadeTime)
        {
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t);
            yield return null;
        }
        musicSource.volume = musicVolume;
    }
}

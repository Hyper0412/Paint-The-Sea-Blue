using System;
using UnityEngine;
using UnityEngine.UI;

// Shared team energy. Hitting a clean fish costs energy; at 0 it's game over.
// The bar can be a UI Image (Filled) OR a SpriteRenderer in the scene. Fill in ONE of the two.
public class EnergyBar : MonoBehaviour
{
    [Header("Energy")]
    public float maxEnergy = 100f;
    public float damagePerFishHit = 20f;

    [Header("Connections")]
    [Tooltip("Filled automatically with every active boat. No need to drag anything in.")]
    public ClawArm[] arms;

    [Header("Bar visuals (use ONE)")]
    [Tooltip("UI Image with Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left")]
    public Image fillImage;
    [Tooltip("OR a SpriteRenderer bar in the scene whose sprite pivot is on the LEFT")]
    public SpriteRenderer fillSprite;
    [Tooltip("How fast the bar slides down (portion of the bar per second)")]
    public float drainSpeed = 1.5f;

    [Header("Hit feedback")]
    public Color hitFlashColor = new Color(1f, 0.35f, 0.35f);
    public float flashTime = 0.3f;

    public event Action OnEnergyEmpty;   // used for the game over screen later

    public float Energy { get; private set; }

    float shown = 1f;           // what the bar shows (animates toward the real value)
    Vector3 spriteFullScale;
    Color normalColor;
    float flashTimer;
    bool empty;

    void Awake()
    {
        arms = ClawArm.FindAllInScene();
    }

    void Start()
    {
        Energy = maxEnergy;
        if (fillSprite != null) spriteFullScale = fillSprite.transform.localScale;
        normalColor = GetColor();

        foreach (var arm in arms) arm.OnHitFish += HandleFishHit;
        ApplyFill();
    }

    void OnDestroy()
    {
        foreach (var arm in arms)
            if (arm != null) arm.OnHitFish -= HandleFishHit;
    }

    void HandleFishHit(Transform fish)
    {
        if (empty) return;

        Energy = Mathf.Max(0f, Energy - damagePerFishHit);
        flashTimer = flashTime;

        var creature = fish.GetComponentInParent<SeaCreature>();
        if (creature != null) creature.Startle();

        Debug.Log($"Energy: {Energy}/{maxEnergy}");

        if (Energy <= 0f)
        {
            empty = true;
            Debug.Log("Out of energy! (game over screen comes later)");
            OnEnergyEmpty?.Invoke();
        }
    }

    void Update()
    {
        shown = Mathf.MoveTowards(shown, Energy / maxEnergy, drainSpeed * Time.deltaTime);
        ApplyFill();

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            SetColor(Color.Lerp(normalColor, hitFlashColor, Mathf.Clamp01(flashTimer / flashTime)));
            if (flashTimer <= 0f) SetColor(normalColor);
        }
    }

    void ApplyFill()
    {
        if (fillImage != null) fillImage.fillAmount = shown;
        if (fillSprite != null)
        {
            Vector3 s = spriteFullScale;
            s.x *= shown;
            fillSprite.transform.localScale = s;
        }
    }

    Color GetColor()
    {
        if (fillImage != null) return fillImage.color;
        if (fillSprite != null) return fillSprite.color;
        return Color.white;
    }

    void SetColor(Color c)
    {
        if (fillImage != null) fillImage.color = c;
        if (fillSprite != null) fillSprite.color = c;
    }
}
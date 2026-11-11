using UnityEngine;

// Fades the dirty water layers out one by one, revealing the clear water underneath.
// The clear water sprite stays at the lowest Order in Layer and is NOT in this list.
public class WaterClarity : MonoBehaviour
{
    [Tooltip("Dirty water layers, from DIRTIEST (highest Order in Layer) to least dirty. Don't include the clear one.")]
    public SpriteRenderer[] dirtyLayers;

    [Tooltip("How fast the water catches up after collecting trash")]
    public float fadeSpeed = 0.5f;

    [Tooltip("How much of the trash must be collected for FULLY clear water. 0.6 = clear at 60%.")]
    [Range(0.3f, 1f)] public float fullyClearAt = 0.7f;

    float target;   // where we want to be (0..1)
    float current;  // what's shown right now

    public void SetCleanliness(float value)
    {
        target = Mathf.Clamp01(value / fullyClearAt);
    }

    void Start()
    {
        Apply();
    }

    void Update()
    {
        current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);
        Apply();
    }

    void Apply()
    {
        int n = dirtyLayers.Length;
        for (int i = 0; i < n; i++)
        {
            // Layer 0 fades during the first 1/n of cleaning, layer 1 during the next 1/n, etc.
            float alpha = 1f - Mathf.Clamp01(current * n - i);
            Color c = dirtyLayers[i].color;
            c.a = alpha;
            dirtyLayers[i].color = c;
        }
    }
}
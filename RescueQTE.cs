using System;
using UnityEngine;

// Quick-time slider for freeing trapped animals.
// A marker slides back and forth; press the button while it's inside the green zone.
// Success: animal is freed and fades away. Miss / time out: claw lets go, try again.
public class RescueQTE : MonoBehaviour
{
    [Header("Connections")]
    public ClawArm arm;               // the boat this QTE belongs to (one QTE per boat)

    [Header("Visuals")]
    public GameObject visual;         // child holding the slider parts (shown/hidden)
    public Transform background;      // the bar; its Scale X = slider width
    public Transform zone;            // green "hit" area
    public Transform marker;          // the moving line
    public Vector3 offsetAboveAnimal = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Keeps the slider at least this far inside the screen edges")]
    public float screenPadding = 0.3f;

    [Header("Difficulty")]
    [Tooltip("Back-and-forth trips per second")]
    public float markerSpeed = 0.8f;
    [Tooltip("Green zone size as a part of the bar (0.25 = a quarter)")]
    [Range(0.05f, 0.6f)] public float zoneSize = 0.25f;
    public float timeLimit = 4f;

    [Header("Freed animal")]
    public float fadeTime = 1.5f;

    public event Action<SeaCreature> OnAnimalFreed;   // for score later

    SeaCreature animal;
    Transform target;
    bool active;
    float barWidth;
    float zoneCenter;   // 0..1 along the bar
    float t;
    float timer;

    void Start()
    {
        visual.SetActive(false);
        arm.OnHitTrappedAnimal += Begin;
    }

    void OnDestroy()
    {
        if (arm != null) arm.OnHitTrappedAnimal -= Begin;
    }

    void Begin(Transform hit)
    {
        animal = hit.GetComponentInParent<SeaCreature>();
        target = animal != null ? animal.transform : hit;
        if (animal != null) animal.canSwim = false;   // hold still during the rescue

        barWidth = background.localScale.x;
        zoneCenter = UnityEngine.Random.Range(zoneSize * 0.5f, 1f - zoneSize * 0.5f);

        Vector3 zs = zone.localScale;
        zs.x = zoneSize * barWidth;
        zone.localScale = zs;
        zone.localPosition = new Vector3((zoneCenter - 0.5f) * barWidth, 0f, zone.localPosition.z);

        t = UnityEngine.Random.value; // random starting spot for the marker
        timer = timeLimit;
        active = true;
        visual.SetActive(true);
        Debug.Log("Rescue! Press the button in the green zone.");
    }

    void Update()
    {
        if (!active) return;

        if (target == null) { End(false); return; }   // animal disappeared somehow
        transform.position = KeepOnScreen(target.position + offsetAboveAnimal);

        t += Time.deltaTime * markerSpeed;
        float pos01 = Mathf.PingPong(t, 1f);
        marker.localPosition = new Vector3((pos01 - 0.5f) * barWidth, 0f, marker.localPosition.z);

        timer -= Time.deltaTime;

        if (arm.input.GetButtonDown())
        {
            bool inZone = Mathf.Abs(pos01 - zoneCenter) <= zoneSize * 0.5f;
            End(inZone);
        }
        else if (timer <= 0f)
        {
            Debug.Log("Too slow!");
            End(false);
        }
    }

    // Pushes the slider back inside the camera view if the animal is near an edge.
    Vector3 KeepOnScreen(Vector3 pos)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return pos;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float barHalfX = Mathf.Abs(background.lossyScale.x) * 0.5f + screenPadding;
        float barHalfY = Mathf.Abs(background.lossyScale.y) * 0.5f + screenPadding;
        Vector3 c = cam.transform.position;

        pos.x = Mathf.Clamp(pos.x, c.x - halfW + barHalfX, c.x + halfW - barHalfX);
        pos.y = Mathf.Clamp(pos.y, c.y - halfH + barHalfY, c.y + halfH - barHalfY);
        return pos;
    }

    void End(bool success)
    {
        active = false;
        visual.SetActive(false);
        arm.ReleaseHold();

        if (animal == null) return;

        if (success)
        {
            Debug.Log("Freed!");
            animal.Free(fadeTime);
            OnAnimalFreed?.Invoke(animal);
        }
        else
        {
            Debug.Log("Missed! Try again.");
            animal.canSwim = true;
        }
        animal = null;
    }
}
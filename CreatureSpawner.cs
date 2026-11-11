using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spawns sea animals (e.g. trapped turtles and fish) the same way trash spawns:
// some at the start, and when one is gone (freed + faded), another may appear later.
// Works for clean fish too: make a second spawner with clean fish prefabs.
public class CreatureSpawner : MonoBehaviour
{
    [Header("What to spawn")]
    [Tooltip("Animal prefabs: tag (TrappedAnimal or Fish) + Collider2D (Is Trigger) + SeaCreature")]
    public GameObject[] prefabs;

    [Header("Amount")]
    [Tooltip("Animals in the sea when the game starts")]
    public int startCount = 3;
    [Tooltip("How many come back per animal freed. 0.5 = every 2nd one. 1 = always replaced.")]
    [Range(0f, 1f)] public float respawnRatio = 0.5f;
    [Tooltip("Random wait (min, max seconds) before a new animal appears")]
    public Vector2 respawnDelay = new Vector2(5f, 10f);
    [Tooltip("Never more than this many at once. 0 = no limit")]
    public int maxAlive = 4;

    [Header("Spawn area (cyan box in Scene view)")]
    public Vector2 areaSize = new Vector2(16f, 5f);
    [Tooltip("Keeps animals from spawning on top of trash or each other")]
    public float minSpacing = 1.5f;

    [Header("Optional")]
    [Tooltip("Drag TrashManager here: no new animals once the sea is clean (boss time)")]
    public TrashManager trashManager;

    readonly List<GameObject> alive = new List<GameObject>();
    float credit;

    bool Stopped => trashManager != null && trashManager.IsCleared;

    void Start()
    {
        for (int i = 0; i < startCount; i++) Spawn();
    }

    void Update()
    {
        // An animal that was freed destroys itself after fading; notice it here.
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null)
            {
                alive.RemoveAt(i);
                OnAnimalGone();
            }
        }
    }

    void OnAnimalGone()
    {
        if (Stopped) return;
        credit += respawnRatio;
        if (credit >= 0.9999f)
        {
            credit -= 1f;
            StartCoroutine(SpawnLater());
        }
    }

    IEnumerator SpawnLater()
    {
        yield return new WaitForSeconds(Random.Range(respawnDelay.x, respawnDelay.y));
        if (Stopped) yield break;
        if (maxAlive > 0 && alive.Count >= maxAlive) yield break;
        Spawn();
    }

    void Spawn()
    {
        if (prefabs.Length == 0) return;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];

        // Keep the animal's whole swim path inside the box.
        float margin = 0f;
        var prefabCreature = prefab.GetComponent<SeaCreature>();
        if (prefabCreature != null) margin = prefabCreature.swimRange;

        Vector2 pos = RandomPoint(margin);
        for (int tries = 0; tries < 30; tries++)
        {
            if (Physics2D.OverlapCircle(pos, minSpacing * 0.5f) == null) break;
            pos = RandomPoint(margin);
        }

        GameObject go = Instantiate(prefab, pos, prefab.transform.rotation, transform);
        var creature = go.GetComponent<SeaCreature>();
        if (creature != null) creature.startMovingRight = Random.value < 0.5f;
        alive.Add(go);
    }

    Vector2 RandomPoint(float marginX)
    {
        Vector2 half = areaSize * 0.5f;
        float halfX = Mathf.Max(0f, half.x - marginX);
        return (Vector2)transform.position + new Vector2(
            Random.Range(-halfX, halfX),
            Random.Range(-half.y, half.y));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, areaSize);
    }
}
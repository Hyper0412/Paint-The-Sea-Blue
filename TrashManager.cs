using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// "Trickle" spawning: the sea starts with some trash. Each time a piece is collected,
// part of it "comes back" later somewhere else (e.g. ratio 0.5 = every 2nd piece respawns).
// So the sea slowly empties, and when every piece that will ever exist is collected, you win.
public class TrashManager : MonoBehaviour
{
    [Header("What to spawn")]
    public GameObject[] trashPrefabs;   // bottle, bag, teddy bear... (tag "Trash" + Collider2D)

    [Header("Amount")]
    [Tooltip("Trash in the sea when the game starts")]
    public int startCount = 10;
    [Tooltip("How much comes back per piece collected. 0.5 = every 2nd piece respawns. 0 = nothing respawns.")]
    [Range(0f, 0.95f)] public float respawnRatio = 0.5f;
    [Tooltip("Random wait (min, max seconds) before a respawned piece appears")]
    public Vector2 respawnDelay = new Vector2(3f, 6f);

    [Header("Spawn area (yellow box in Scene view)")]
    public Vector2 areaSize = new Vector2(16f, 5f);
    [Tooltip("Keeps trash from spawning on top of each other or on animals")]
    public float minSpacing = 1.2f;

    [Header("Connections")]
    [Tooltip("Filled automatically with every active boat. No need to drag anything in.")]
    public ClawArm[] arms;
    public WaterClarity water;

    public event Action OnAllTrashCleared;   // used for the win screen later

    readonly List<Transform> activeTrash = new List<Transform>();
    readonly HashSet<Transform> extraTrash = new HashSet<Transform>();   // dropped by the submarine
    int totalTrash;   // every piece that will ever exist this game
    int collected;
    float credit;     // builds up with each pickup; at 1 a new piece is spawned
    bool cleared;

    // 0 = dirtiest, 1 = everything collected
    public float Cleanliness => totalTrash == 0 ? 0f : (float)collected / totalTrash;
    public int TrashLeft => totalTrash - collected;   // handy for a UI counter later
    public bool IsCleared => cleared;

    void Awake()
    {
        arms = ClawArm.FindAllInScene();
    }

    void Start()
    {
        totalTrash = CalculateTotal();
        foreach (var arm in arms) arm.OnTrashDelivered += HandleCollected;

        for (int i = 0; i < startCount; i++) SpawnOne();
        Debug.Log($"Trash this game: {totalTrash} ({startCount} at start, the rest respawns)");
    }

    void OnDestroy()
    {
        foreach (var arm in arms)
            if (arm != null) arm.OnTrashDelivered -= HandleCollected;
    }

    void HandleCollected(Transform trash)
    {
        if (cleared) return;

        activeTrash.Remove(trash);
        bool wasExtra = extraTrash.Remove(trash);
        collected++;
        if (water != null) water.SetCleanliness(Cleanliness);

        // Submarine trash never respawns, so the total stays exact.
        if (!wasExtra)
        {
            credit += respawnRatio;
            if (credit >= 0.9999f)
            {
                credit -= 1f;
                StartCoroutine(SpawnAfterDelay());
            }
        }

        if (collected >= totalTrash)
        {
            cleared = true;
            Debug.Log("The sea is clean!");
            OnAllTrashCleared?.Invoke();
        }
    }

    // Plays the same "credit" math ahead of time to know exactly how many pieces
    // will exist in total, so the water reaches fully clear on the very last one.
    int CalculateTotal()
    {
        int total = startCount;
        int pending = startCount;
        float c = 0f;
        while (pending > 0)
        {
            pending--;
            c += respawnRatio;
            if (c >= 0.9999f)
            {
                c -= 1f;
                total++;
                pending++;
            }
        }
        return total;
    }

    IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(respawnDelay.x, respawnDelay.y));
        SpawnOne();
    }

    void SpawnOne()
    {
        Vector2 pos = RandomPoint();
        for (int tries = 0; tries < 30; tries++)
        {
            if (IsFree(pos)) break;
            pos = RandomPoint();
        }

        SpawnAt(pos);
    }

    Transform SpawnAt(Vector2 pos)
    {
        GameObject prefab = trashPrefabs[UnityEngine.Random.Range(0, trashPrefabs.Length)];
        Quaternion tilt = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-25f, 25f));
        Transform t = Instantiate(prefab, pos, tilt, transform).transform;
        activeTrash.Add(t);
        return t;
    }

    // Called by the submarine: adds trash on top of the normal amount.
    // The water gets a little dirtier again, since there's more to clean.
    public void AddExtraTrash(Vector2 pos)
    {
        if (cleared) return;
        totalTrash++;
        extraTrash.Add(SpawnAt(pos));
        if (water != null) water.SetCleanliness(Cleanliness);
    }

    bool IsFree(Vector2 pos)
    {
        foreach (var t in activeTrash)
            if (t != null && Vector2.Distance(t.position, pos) < minSpacing) return false;
        return Physics2D.OverlapCircle(pos, minSpacing * 0.5f) == null;
    }

    Vector2 RandomPoint()
    {
        Vector2 half = areaSize * 0.5f;
        return (Vector2)transform.position + new Vector2(
            UnityEngine.Random.Range(-half.x, half.x),
            UnityEngine.Random.Range(-half.y, half.y));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, areaSize);
    }
}
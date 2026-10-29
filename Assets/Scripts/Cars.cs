using System.Collections.Generic;
using UnityEngine;

// Auto's rijden over de wegen (rechts houden), draaien aan het eind om of nemen een aansluitende weg
public class Cars : MonoBehaviour
{
    class Car { public Transform root; public RoadPath path; public float s, speed; public int dir = 1; public float yaw; }
    readonly List<Car> list = new List<Car>();
    public int Count => list.Count;
    float spawnT;

    Car Spawn(RoadPath p)
    {
        var c = new Car { path = p, s = Random.value * Nav.Length(p), dir = Random.value < 0.5f ? 1 : -1, speed = 6f + Random.value * 4f };
        var root = new GameObject("Auto").transform; root.SetParent(transform, false); c.root = root;
        LowPolyCar.Build(root);   // een van de zeven low-poly modellen van de webversie
        Place(c, 0f); return c;
    }

    void Place(Car c, float dt)
    {
        Vector2 d; Vector2 p = Nav.At(c.path, c.s, out d); Vector2 fwd = d * c.dir, right = new Vector2(fwd.y, -fwd.x);
        Vector2 q = p + right * 1.25f; c.root.position = new Vector3(q.x, c.path.Level * RoadRules.LAYER_H + 0.15f, q.y);
        float target = Mathf.Atan2(fwd.x, fwd.y) * Mathf.Rad2Deg; c.yaw = dt <= 0f ? target : Mathf.LerpAngle(c.yaw, target, Mathf.Min(1f, dt * 8f));
        c.root.rotation = Quaternion.Euler(0, c.yaw, 0);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        var valid = new List<RoadPath>(); foreach (var p in City.Paths) if (p.P.Count >= 2) valid.Add(p);
        int target = Mathf.Clamp(Mathf.RoundToInt(City.RoadLength() / 90f), 0, 14); spawnT -= dt;
        if (valid.Count > 0 && list.Count < target && spawnT <= 0f) { list.Add(Spawn(valid[Random.Range(0, valid.Count)])); spawnT = 0.7f; }
        while (list.Count > target || (valid.Count == 0 && list.Count > 0)) { Destroy(list[list.Count - 1].root.gameObject); list.RemoveAt(list.Count - 1); }
        foreach (var c in list)
        {
            if (!City.Paths.Contains(c.path)) { Vector2 at = new Vector2(c.root.position.x, c.root.position.z); if (!Nav.Rehome(ref c.path, ref c.s, at)) { c.path = valid[Random.Range(0, valid.Count)]; c.s = 0f; } }
            float len = Nav.Length(c.path); c.s += c.dir * c.speed * dt;
            if (c.s < 0f || c.s > len)
            {
                Vector2 at = Nav.At(c.path, Mathf.Clamp(c.s, 0f, len), out _);
                if (!Nav.Hop(ref c.path, ref c.s, ref c.dir, at, false)) { c.dir = -c.dir; c.s = Mathf.Clamp(c.s, 0f, len); }
            }
            Place(c, dt);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The War Table in the middle of the Great Hall: a map of an unknown land with every
/// arena floating above it as a miniature island (baked by MiniatureBaker). While the
/// players pick wizards it's the hall's centrepiece; once everyone's ready the camera
/// moves in and the stick points at an island (by screen direction). The chosen island
/// rises into a beam of light; A dives into it.
/// </summary>
public class WarTable : MonoBehaviour
{
    [Tooltip("Where the islands float from: the land's surface")]
    public Transform surface;
    [Tooltip("Side of each island, in hall units")]
    public float islandSize = 1.45f;
    [Tooltip("Base float height over the land")]
    public float floatHeight = 0.75f;

    public int Selected { get; private set; }
    public int Count => islands.Count;
    public bool Focused { get; private set; }

    class Island
    {
        public HallCatalog.MapEntry entry;
        public Transform root;      // bobbing holder
        public Transform model;
        public Vector3 home;        // local, over its spot
        public LineRenderer beam;
        public LineRenderer mark;   // ring on the land
        public Light light;
        public float lift;
        public float scale = 1f;    // model scale that makes it islandSize wide
    }

    readonly List<Island> islands = new();
    HallCatalog catalog;
    float nextMove;
    int heldDir = -1;

    public void Init(HallCatalog catalog)
    {
        this.catalog = catalog;
        if (surface == null) surface = transform;
        for (int i = 0; i < catalog.maps.Length; i++) islands.Add(Build(catalog.maps[i], i));
        Selected = 0;
    }

    Island Build(HallCatalog.MapEntry e, int i)
    {
        var isl = new Island { entry = e };
        var root = new GameObject($"Island {e.displayName}").transform;
        root.SetParent(surface, false);
        // tablePos is in screen-aligned table units: x = screen right, y = screen up (along the floor)
        // the land is already turned to the screen: its local x is screen-right, z screen-up
        Vector3 spot = new Vector3(e.tablePos.x, 0f, e.tablePos.y);
        isl.home = spot + Vector3.up * (floatHeight + e.height);
        root.localPosition = isl.home;
        root.rotation = Quaternion.identity;   // the arena faces the way it does in a match
        isl.root = root;

        if (e.miniature != null)
        {
            var m = Instantiate(e.miniature, root);
            var mini = m.GetComponent<MapMiniature>();
            float size = mini != null && mini.size > 0f ? mini.size : 30f;
            isl.scale = islandSize / size;
            m.transform.localScale = Vector3.one * isl.scale;
            m.transform.localPosition = Vector3.zero;
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            isl.model = m.transform;
        }

        // the light that holds it up, and its mark on the land
        isl.beam = GlowLine.Make(root, "Beam", 2, 0.07f, HallFx.RingMaterial());
        isl.beam.useWorldSpace = false;
        isl.mark = HallFx.RuneRing(surface, $"Mark {e.displayName}", islandSize * 0.42f, 0.03f, 6);
        isl.mark.transform.localPosition = spot + Vector3.up * 0.02f;
        isl.light = HallFx.PointLight(root, "Glow", Vector3.up * 0.4f, e.glow, 0f, 2.5f);
        return isl;
    }

    public Transform IslandTransform(int i) => i >= 0 && i < islands.Count ? islands[i].root : null;
    public HallCatalog.MapEntry Entry(int i) => islands[Mathf.Clamp(i, 0, islands.Count - 1)].entry;

    /// Top centre of island i in world space (for name plates)
    public Vector3 Top(int i)
    {
        var isl = islands[Mathf.Clamp(i, 0, islands.Count - 1)];
        return isl.root.position + Vector3.up * (islandSize * 0.6f);
    }

    public void SetFocus(bool on)
    {
        Focused = on;
        heldDir = -1;
    }

    public void Select(int i)
    {
        if (i < 0 || i >= islands.Count || i == Selected) return;
        Selected = i;
        AbilityKit.Shockwave(islands[i].root.position + Vector3.down * 0.05f, islandSize * 0.8f, islands[i].entry.glow, 0.3f);
    }

    /// Stick/d-pad: jump to the island lying most in that screen direction
    public void Tick(Gamepad pad, Camera cam)
    {
        if (pad == null || islands.Count == 0) return;
        Vector2 v = pad.leftStick.ReadValue();
        if (v.magnitude < 0.5f) v = pad.dpad.ReadValue();
        if (v.magnitude < 0.5f) { heldDir = -1; return; }
        int dir = Mathf.RoundToInt(Mathf.Atan2(v.y, v.x) / (Mathf.PI / 4f)) & 7;
        float now = Time.unscaledTime;
        if (dir == heldDir && now < nextMove) return;
        nextMove = now + (dir == heldDir ? 0.22f : 0.35f);
        heldDir = dir;

        int best = Pick(v.normalized, cam);
        if (best >= 0)
        {
            Select(best);
            Rumble.Swap(pad);
        }
    }

    int Pick(Vector2 want, Camera cam)
    {
        Vector2 from = cam.WorldToViewportPoint(islands[Selected].root.position);
        float aspect = cam.aspect;
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < islands.Count; i++)
        {
            if (i == Selected) continue;
            Vector2 to = cam.WorldToViewportPoint(islands[i].root.position);
            Vector2 d = to - from;
            d.x *= aspect;
            float dist = d.magnitude;
            if (dist < 0.0001f) continue;
            float cos = Vector2.Dot(d / dist, want);
            if (cos < 0.35f) continue;   // within ~70° of the stick
            float score = dist * (1f + 2.5f * (1f - cos));
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < islands.Count; i++)
        {
            var isl = islands[i];
            bool sel = Focused && i == Selected;
            isl.lift = Mathf.MoveTowards(isl.lift, sel ? 1f : 0f, Time.deltaTime * 3f);
            float e = isl.lift * isl.lift * (3f - 2f * isl.lift);
            float bob = Mathf.Sin(t * 0.9f + i * 1.7f) * 0.06f;
            isl.root.localPosition = isl.home + Vector3.up * (bob + e * 0.45f);
            if (isl.model != null) isl.model.localScale = Vector3.one * isl.scale * (1f + 0.18f * e);

            // beam from the land up to the island's underside
            float under = (floatHeight + isl.entry.height + bob + e * 0.45f);
            isl.beam.SetPosition(0, Vector3.down * under);
            isl.beam.SetPosition(1, Vector3.down * islandSize * 0.25f);
            float a = Focused ? (sel ? 0.55f + 0.15f * Mathf.Sin(t * 5f) : 0.1f) : 0.12f + 0.05f * Mathf.Sin(t + i);
            GlowLine.SetColor(isl.beam, isl.entry.glow, a);
            isl.beam.widthMultiplier = sel ? 1.8f : 1f;
            GlowLine.SetColor(isl.mark, isl.entry.glow, sel ? 0.9f : Focused ? 0.25f : 0.35f);
            isl.mark.transform.Rotate(Vector3.up, (sel ? 60f : 10f) * Time.deltaTime, Space.World);
            isl.light.intensity = sel ? 1.0f : Focused ? 0.15f : 0.35f;
        }
    }

    /// The chosen island flares; returns its centre for the camera to dive into
    public Vector3 Launch()
    {
        var isl = islands[Selected];
        AbilityKit.Shockwave(isl.root.position, islandSize * 1.4f, isl.entry.glow, 0.6f);
        isl.light.intensity = 6f;
        return isl.root.position;
    }
}

using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class ScrollingTrackCamera : MonoBehaviour
{
    [Header("Players (tags: Player1..Player4)")]
    public bool autoFindPlayers = true;

    [Header("Track via Markers")]
    public Transform startMarker;                 // REQUIRED
    public Transform endMarker;                   // REQUIRED
    [Tooltip("Flatten the track direction to XZ so the camera slides on the ground plane.")]
    public bool projectTrackToGround = true;

    [Tooltip("Extra meters beyond the end marker the camera may travel (optional).")]
    public float endPadding = 0f;

    [Tooltip("Meters to lead ahead of the players along the track (optional).")]
    public float leadAhead = 0f;

    [Header("Height")]
    [Tooltip("Keep camera Y locked to its start height (plus offset).")]
    public bool preserveStartHeight = true;
    public float heightOffset = 0f;   // tweak if you want higher/lower than start

    [Header("Smoothing")]
    [Tooltip("Higher = snappier, lower = floatier (exp smoothing).")]
    public float followLag = 8f;

    [Header("Zoom (Perspective only)")]
    public bool enableZoom = true;
    public float minFOV = 35f, maxFOV = 60f;
    public float spreadToFOV = 0.25f;
    public float zoomLag = 6f;

    [Header("Gizmos")]
    public Color trackColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    // Internals
    private Camera _cam;
    private Vector3 _origin;        // start marker position (keeps its real Y)
    private Vector3 _dir;           // normalized track direction (flattened if opted)
    private float _trackLength;     // distance from start to end along _dir
    private Vector3 _lateralOffset; // preserves sideways offset (no Y)
    private float _startY;

    // ---------- Unity ----------
    void OnEnable() { _cam = GetComponent<Camera>(); RebuildTrack(); }
    void Start()    { RebuildTrack(); }
    void Update()   { if (!Application.isPlaying) RebuildTrack(); }

    void LateUpdate()
    {
        if (!ValidateMarkers()) return;
        if (autoFindPlayers) _ = Players;
        if (_players.Count == 0) return;

        Vector3 avg = AveragePlayerPos();

        // Project average player position onto the track
        float t0 = Vector3.Dot(avg - _origin, _dir) + leadAhead;
        float t  = Mathf.Clamp(t0, 0f, _trackLength + Mathf.Max(0f, endPadding));

        // Slide along the track, then add sideways/height handling
        Vector3 desired = _origin + _dir * t + _lateralOffset;

        if (preserveStartHeight)
            desired.y = _startY + heightOffset;

        float alpha = 1f - Mathf.Exp(-followLag * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, alpha);

        // Optional spread-based zoom
        if (enableZoom && _cam && !_cam.orthographic)
        {
            float spread = FurthestPairDistance();
            float targetFov = Mathf.Clamp(minFOV + spread * spreadToFOV, minFOV, maxFOV);
            float zAlpha = 1f - Mathf.Exp(-zoomLag * Time.deltaTime);
            _cam.fieldOfView = Vector3.Lerp(new Vector3(_cam.fieldOfView,0,0),
                                            new Vector3(targetFov,0,0),
                                            zAlpha).x;
        }
    }

    // ---------- Players ----------
    private readonly List<Transform> _players = new List<Transform>(4);
    private float _refreshTimer;

    private List<Transform> Players
    {
        get
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.2f;
                if (autoFindPlayers) RefreshPlayers();
            }
            return _players;
        }
    }

    private void RefreshPlayers()
    {
        _players.Clear();
        for (int i = 1; i <= 4; i++)
        {
            var go = GameObject.FindGameObjectWithTag("Player" + i);
            if (go) _players.Add(go.transform);
        }
#if UNITY_EDITOR
        if (_players.Count == 0)
            Debug.LogWarning("[ScrollingTrackCamera] No players found with tags Player1..Player4.");
#endif
    }

    private Vector3 AveragePlayerPos()
    {
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var p in _players) { if (!p) continue; sum += p.position; n++; }
        return n > 0 ? sum / n : transform.position;
    }

    private float FurthestPairDistance()
    {
        float maxD = 0f;
        for (int i = 0; i < _players.Count; i++)
        {
            var a = _players[i]; if (!a) continue;
            for (int j = i + 1; j < _players.Count; j++)
            {
                var b = _players[j]; if (!b) continue;
                float d = Vector3.Distance(a.position, b.position);
                if (d > maxD) maxD = d;
            }
        }
        return maxD;
    }

    // ---------- Track ----------
    private bool ValidateMarkers() => startMarker && endMarker;

    private void RebuildTrack()
    {
        if (!startMarker || !endMarker) return;

        if (_cam == null) _cam = GetComponent<Camera>();
        if (preserveStartHeight) _startY = transform.position.y;

        // Keep origin at the start marker (real Y)
        _origin = startMarker.position;

        // Direction from start to end; optionally flatten to ground for isometric tracks
        Vector3 dir = endMarker.position - startMarker.position;
        if (projectTrackToGround)
            dir = Vector3.ProjectOnPlane(dir, Vector3.up);

        if (dir.sqrMagnitude < 1e-6f)
        {
            _dir = Vector3.forward;
            _trackLength = 0f;
        }
        else
        {
            _dir = dir.normalized;
            _trackLength = dir.magnitude;
        }

        // Compute sideways offset (remove any component along the track; drop Y)
        Vector3 camDelta = transform.position - _origin;
        float tCam = Vector3.Dot(camDelta, _dir);
        Vector3 camOnTrack = _origin + _dir * tCam;
        _lateralOffset = Vector3.ProjectOnPlane(transform.position - camOnTrack, _dir);
        _lateralOffset.y = 0f; // height handled by preserveStartHeight
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!startMarker || !endMarker) return;

        Vector3 a = startMarker.position;
        Vector3 dir = endMarker.position - startMarker.position;
        if (projectTrackToGround) dir = Vector3.ProjectOnPlane(dir, Vector3.up);
        if (dir.sqrMagnitude < 1e-6f) return;

        Vector3 nDir = dir.normalized;
        float len = dir.magnitude;

        Gizmos.color = trackColor;
        Gizmos.DrawLine(a, a + nDir * (len + Mathf.Max(0f, endPadding)));
        Gizmos.DrawSphere(a, 0.5f);
        Gizmos.DrawSphere(a + nDir * len, 0.5f);
    }
#endif
}

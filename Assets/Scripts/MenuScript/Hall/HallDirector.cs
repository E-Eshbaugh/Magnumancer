using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the Great Hall, the 3D menu between the title screen and a match:
///  1. Hall — anyone presses X to join; their wizard arrives on a free dais and they
///     pick wizard, runes and loadout at their own station (HallStation), all at once.
///  2. Once everyone in is ready, A marches the party to the War Table (the camera moves
///     in on the map of floating arena miniatures). LB/RB picks the mode, the stick picks
///     an island, A dives into it and loads the arena; B goes back to the hall.
/// Results go to DataManager exactly as the old book menu left them. Coming back from a
/// match, everyone's pad, wizard and loadout are restored, already standing ready.
/// </summary>
public class HallDirector : MonoBehaviour
{
    public HallCatalog catalog;
    public HallCamera hallCamera;
    public WarTable table;
    public HallStation[] stations = new HallStation[4];

    public enum Phase { Hall, ToTable, Table, Launch }
    public Phase CurrentPhase { get; private set; } = Phase.Hall;
    public IReadOnlyList<HallStation> Stations => stations;
    public int SelectedMode { get; private set; }

    Canvas ui;
    CanvasGroup cardGroup;
    Image fade;
    RectTransform statusBar;
    Text statusText;
    HallUI.Prompts statusPrompts;
    RectTransform mapPanel;
    Text mapTitle, mapFlavor, modeName, modeDesc, plate;
    RectTransform plateRoot;
    HallUI.Prompts mapPrompts;
    bool allReadyLastFrame;

    public static HallDirector Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        if (DataManager.Instance == null) new GameObject("DataManager").AddComponent<DataManager>();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        ui = HallUI.Canvas("HallUI", 10);
        var cards = HallUI.Rect(ui.transform, "Cards");
        HallUI.Stretch(cards);
        cardGroup = cards.gameObject.AddComponent<CanvasGroup>();

        Vector3 tableCenter = table != null ? table.transform.position : Vector3.zero;
        foreach (var s in stations)
        {
            if (s == null) continue;
            Vector3 toStation = (s.transform.position - tableCenter);
            toStation.y = 0f;
            Vector3 edge = tableCenter + toStation.normalized * 3.2f;
            edge.y = s.transform.position.y;
            s.Init(this, catalog, cards, edge);
        }
        if (table != null) table.Init(catalog);

        BuildStatus();
        BuildMapUI();

        fade = HallUI.Box(ui.transform, "Fade", Color.black);
        HallUI.Stretch(fade.rectTransform);
        fade.transform.SetAsLastSibling();
        StartCoroutine(Fade(1f, 0f, 0.6f));

        hallCamera.CutToHall();
        SelectedMode = Mathf.Clamp(DataManager.Instance.SelectedMode, 0, Mathf.Max(0, catalog.modes.Length - 1));
        if (table != null) table.ShowMode(WavesMode);
        RestoreLastParty();
        if (table != null && DataManager.Instance.SelectedMap >= 0 && DataManager.Instance.SelectedMap < table.Count)
            table.Select(DataManager.Instance.SelectedMap);
    }

    // ---------- the party ----------

    /// Players coming back from a match keep their pad, wizard, runes and guns
    void RestoreLastParty()
    {
        var dm = DataManager.Instance;
        if (dm.Inputs == null || dm.Wizards == null) return;
        int n = Mathf.Min(dm.NumPlayers, dm.Inputs.Length, stations.Length);
        int slot = 0;
        for (int i = 0; i < n; i++)
        {
            var pad = dm.GetPad(i);
            var wiz = dm.GetWizard(i);
            if (pad == null || !pad.added || wiz == null) continue;
            int w = System.Array.IndexOf(catalog.wizards, wiz);
            if (w < 0) continue;
            stations[slot].Restore(pad, w, dm.GetActiveRune(i), dm.GetPassiveRune(i), dm.GetLoadout(i), slot * 0.25f);
            slot++;
        }
    }

    HallStation StationOf(Gamepad pad)
    {
        foreach (var s in stations) if (s != null && s.Joined && s.Pad == pad) return s;
        return null;
    }

    int JoinedCount { get { int n = 0; foreach (var s in stations) if (s != null && s.Joined) n++; return n; } }

    bool AllReady
    {
        get
        {
            int n = 0;
            foreach (var s in stations)
            {
                if (s == null || !s.Joined) continue;
                if (!s.IsReady) return false;
                n++;
            }
            return n > 0;
        }
    }

    /// A wizard nobody else is standing as, starting from the station's own default
    int SuggestWizard(HallStation st)
    {
        int count = catalog.wizards.Length;
        int start = Mathf.Clamp(st.index * 2, 0, count - 1);
        for (int k = 0; k < count; k++)
        {
            int w = (start + k) % count;
            bool taken = false;
            foreach (var s in stations) if (s != null && s != st && s.Joined && s.WizardIndex == w) taken = true;
            if (!taken) return w;
        }
        return start;
    }

    public void RequestLeave(HallStation st)
    {
        if (st == null || !st.Joined) return;
        st.Leave();
    }

    // ---------- loop ----------

    void Update()
    {
        switch (CurrentPhase)
        {
            case Phase.Hall: HallUpdate(); break;
            case Phase.Table: TableUpdate(); break;
        }
        UpdateStatus();
        UpdateMapUI();
        // the cards step aside at the War Table
        float want = CurrentPhase == Phase.Hall ? 1f : 0f;
        cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, want, Time.unscaledDeltaTime * 4f);
    }

    void HallUpdate()
    {
        // join: X on any pad that isn't in yet
        foreach (var pad in Gamepad.all)
        {
            if (!pad.buttonWest.wasPressedThisFrame || StationOf(pad) != null) continue;
            HallStation free = null;
            foreach (var s in stations) if (s != null && !s.Joined) { free = s; break; }
            if (free == null) break;
            free.Join(pad, SuggestWizard(free));
        }

        foreach (var s in stations) if (s != null) s.Tick();

        // nobody in and someone presses B: back to the title
        if (JoinedCount == 0)
            foreach (var pad in Gamepad.all)
                if (pad.buttonEast.wasPressedThisFrame) { StartCoroutine(LoadScene(MenuScenes.Title)); return; }

        // everyone ready: A or Start from any of them heads to the War Table
        bool ready = AllReady;
        if (ready && allReadyLastFrame)
        {
            foreach (var s in stations)
            {
                if (s == null || !s.Joined || s.LastChangeFrame == Time.frameCount) continue;
                if (s.Pad.buttonSouth.wasPressedThisFrame || s.Pad.startButton.wasPressedThisFrame)
                {
                    GoToTable();
                    break;
                }
            }
        }
        allReadyLastFrame = ready;
    }

    void GoToTable()
    {
        CurrentPhase = Phase.ToTable;
        foreach (var s in stations) if (s != null && s.Joined) Rumble.Play(s.Pad, 0.3f, 0.6f, 0.25f);
        hallCamera.ToTable();
        table.SetFocus(true);
        StartCoroutine(After(1.0f, () => { if (CurrentPhase == Phase.ToTable) CurrentPhase = Phase.Table; }));
    }

    void BackToHall()
    {
        CurrentPhase = Phase.Hall;
        allReadyLastFrame = false;
        table.SetFocus(false);
        hallCamera.ToHall();
    }

    void TableUpdate()
    {
        foreach (var s in stations)
        {
            if (s == null || !s.Joined || s.Pad == null) continue;
            var p = s.Pad;
            table.Tick(p, hallCamera.Cam);
            if (p.rightShoulder.wasPressedThisFrame) SetMode(SelectedMode + 1, p);
            if (p.leftShoulder.wasPressedThisFrame) SetMode(SelectedMode - 1, p);
            if (p.buttonSouth.wasPressedThisFrame || p.startButton.wasPressedThisFrame) { Launch(); return; }
            if (p.buttonEast.wasPressedThisFrame) { BackToHall(); return; }
        }
        // if everyone left (pads unplugged) fall back to the hall
        if (JoinedCount == 0) BackToHall();
    }

    /// The chosen mode is fought against the horde (Waves): only Waves maps are offered
    bool WavesMode => catalog.modes.Length > 0 && catalog.modes[Mathf.Clamp(SelectedMode, 0, catalog.modes.Length - 1)].waves;

    void SetMode(int m, Gamepad pad)
    {
        int n = Mathf.Max(1, catalog.modes.Length);
        SelectedMode = (m + n) % n;
        if (table != null) table.ShowMode(WavesMode);   // PvP arenas or the Waves maps
        Rumble.Swap(pad);
        foreach (var s in stations) if (s != null) s.Refresh();   // comp hints read the mode
    }

    // ---------- launch ----------

    void Launch()
    {
        CurrentPhase = Phase.Launch;
        Commit();
        Vector3 center = table.Launch();
        foreach (var s in stations) if (s != null && s.Joined) Rumble.Play(s.Pad, 0.7f, 1f, 0.5f);
        hallCamera.MoveTo(center, 0.35f, 1.1f);
        StartCoroutine(LoadScene(table.Entry(table.Selected).scene, 0.75f, catalog.maps[table.Selected].glow));
    }

    /// Writes the party into DataManager the way the arenas read it
    public void Commit()
    {
        var dm = DataManager.Instance;
        var joined = new List<HallStation>();
        foreach (var s in stations) if (s != null && s.Joined) joined.Add(s);

        var devices = new List<InputDevice>();
        foreach (var s in joined) devices.Add(s.Pad);
        dm.SetInputs(devices);
        dm.InitPlayers(joined.Count);
        for (int i = 0; i < joined.Count; i++)
        {
            var s = joined[i];
            dm.SetWizard(i, s.Wizard);
            dm.SetRunes(i, s.ActiveRune, s.PassiveRune);
            dm.SetLoadout(i, s.GameLoadout());
        }
        if (joined.Count > 0) dm.SetMasterDevice(joined[0].Pad);
        dm.SelectedMode = SelectedMode;
        dm.SelectedMap = table != null ? table.Selected : 0;
    }

    IEnumerator LoadScene(string scene, float delay = 0f, Color? flash = null)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (flash.HasValue) fade.color = new Color(flash.Value.r, flash.Value.g, flash.Value.b, 0f);
        yield return Fade(0f, 1f, 0.35f);
        if (flash.HasValue) yield return Fade(1f, 1f, 0.05f, Color.black);
        SceneManager.LoadScene(scene);
    }

    IEnumerator Fade(float from, float to, float time, Color? color = null)
    {
        if (color.HasValue) fade.color = color.Value;
        var c = fade.color;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, t / time);
            fade.color = c;
            yield return null;
        }
        c.a = to;
        fade.color = c;
        fade.raycastTarget = false;
    }

    IEnumerator After(float s, System.Action a)
    {
        yield return new WaitForSecondsRealtime(s);
        a();
    }

    // ---------- shared UI ----------

    void BuildStatus()
    {
        statusBar = HallUI.Rect(ui.transform, "Status");
        statusBar.anchorMin = statusBar.anchorMax = new Vector2(0.5f, 0f);
        statusBar.pivot = new Vector2(0.5f, 0f);
        statusBar.anchoredPosition = new Vector2(0f, 26f);
        statusBar.sizeDelta = new Vector2(600f, 60f);
        statusText = HallUI.Label(statusBar, "Text", catalog.titleFont, 3, HallUI.Ink, TextAnchor.UpperCenter);
        statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
        statusPrompts = new HallUI.Prompts(statusBar, catalog.bodyFont, 2);
    }

    void UpdateStatus()
    {
        bool hall = CurrentPhase == Phase.Hall;
        statusBar.gameObject.SetActive(hall);
        if (!hall) return;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
        string text;
        statusPrompts.Begin();
        if (JoinedCount == 0)
        {
            text = "THE GREAT HALL";
            statusPrompts.Add(catalog.btnX, "Join", HallUI.Ink);
            statusPrompts.Add(catalog.btnB, "Title", HallUI.Dim);
        }
        else if (AllReady)
        {
            text = HallUI.Tint("ALL READY", Color.Lerp(HallUI.Ink, new Color(1f, 0.85f, 0.4f), pulse));
            statusPrompts.Add(catalog.btnA, "March to the War Table", HallUI.Ink);
            if (JoinedCount < stations.Length) statusPrompts.Add(catalog.btnX, "Join", HallUI.Dim);
        }
        else
        {
            int ready = 0;
            foreach (var s in stations) if (s != null && s.IsReady) ready++;
            text = $"{ready}/{JoinedCount} READY";
            if (JoinedCount < stations.Length) statusPrompts.Add(catalog.btnX, "Join", HallUI.Dim);
        }
        HallUI.Set(statusText, text, -300f, 0f, 600f);
        statusText.rectTransform.anchorMin = statusText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        statusText.rectTransform.pivot = new Vector2(0.5f, 1f);
        statusText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        float w = statusPrompts.End(20f);
        var pr = statusPrompts.Root;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f);
        pr.pivot = new Vector2(0.5f, 1f);
        pr.anchoredPosition = new Vector2(0f, -32f);
        pr.sizeDelta = new Vector2(w, 30f);
        // Prompts lays itself out from its left edge
        pr.pivot = new Vector2(0f, 1f);
        pr.anchoredPosition = new Vector2(-w * 0.5f, -32f);
    }

    void BuildMapUI()
    {
        mapPanel = HallUI.Rect(ui.transform, "WarTableUI");
        HallUI.Stretch(mapPanel);

        // mode selector, top centre
        var top = HallUI.Panel(mapPanel, "Mode", new Color(0.55f, 0.85f, 1f), out _, out _);
        top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f);
        top.pivot = new Vector2(0.5f, 1f);
        top.anchoredPosition = new Vector2(0f, -22f);
        top.sizeDelta = new Vector2(760f, 132f);
        mapTitle = HallUI.Label(top, "Title", catalog.titleFont, 2, HallUI.Dim, TextAnchor.UpperCenter);
        modeName = HallUI.Label(top, "ModeName", catalog.titleFont, 4, HallUI.Ink, TextAnchor.UpperCenter);
        modeDesc = HallUI.Label(top, "ModeDesc", catalog.bodyFont, 2, HallUI.Dim, TextAnchor.UpperCenter);
        var lb = HallUI.Icon(top, "LB", catalog.btnLB, 40f); HallUI.Place(lb.rectTransform, 30f, 40f, 46f, 34f);
        var rb = HallUI.Icon(top, "RB", catalog.btnRB, 40f); HallUI.Place(rb.rectTransform, 760f - 76f, 40f, 46f, 34f);

        // the chosen map, bottom centre
        var bottom = HallUI.Panel(mapPanel, "Map", new Color(0.55f, 0.85f, 1f), out _, out _);
        bottom.anchorMin = bottom.anchorMax = new Vector2(0.5f, 0f);
        bottom.pivot = new Vector2(0.5f, 0f);
        bottom.anchoredPosition = new Vector2(0f, 22f);
        bottom.sizeDelta = new Vector2(900f, 150f);
        mapFlavor = HallUI.Label(bottom, "Flavor", catalog.bodyFont, 2, HallUI.Ink, TextAnchor.UpperCenter);
        mapPrompts = new HallUI.Prompts(bottom, catalog.bodyFont, 2);

        // name plate over the selected island
        plateRoot = HallUI.Rect(mapPanel, "Plate");
        plateRoot.sizeDelta = new Vector2(600f, 60f);
        plate = HallUI.Label(plateRoot, "Name", catalog.titleFont, 3, HallUI.Ink, TextAnchor.LowerCenter);
        plate.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    void UpdateMapUI()
    {
        bool on = CurrentPhase == Phase.Table || CurrentPhase == Phase.ToTable;
        mapPanel.gameObject.SetActive(on);
        if (!on || table == null || table.Count == 0) return;

        var mode = catalog.modes.Length > 0 ? catalog.modes[SelectedMode] : null;
        HallUI.Set(mapTitle, "THE WAR TABLE", 0f, 14f, 760f);
        HallUI.Set(modeName, mode != null ? mode.name.ToUpperInvariant() : "", 0f, 38f, 760f);
        HallUI.Set(modeDesc, mode != null ? mode.description : "", 60f, 84f, 640f);

        var e = table.Entry(table.Selected);
        HallUI.Set(mapFlavor, $"{HallUI.Tint(e.displayName.ToUpperInvariant(), e.glow)}\n{e.flavor}", 40f, 16f, 820f);
        mapPrompts.Begin();
        mapPrompts.Add(catalog.btnStick, "Choose", HallUI.Ink);
        mapPrompts.Add(catalog.btnA, "Fight here", HallUI.Ink);
        mapPrompts.Add(catalog.btnB, "Back to the hall", HallUI.Dim);
        float w = mapPrompts.End(20f);
        HallUI.Place(mapPrompts.Root, (900f - w) * 0.5f, 108f, w, 30f);

        // plate floats over the chosen island
        Vector3 vp = hallCamera.Viewport(table.Top(table.Selected));
        var canvasRect = (RectTransform)ui.transform;
        plateRoot.anchorMin = plateRoot.anchorMax = Vector2.zero;
        plateRoot.pivot = new Vector2(0.5f, 0f);
        plateRoot.anchoredPosition = new Vector2(vp.x * canvasRect.rect.width, vp.y * canvasRect.rect.height + 8f);
        HallUI.Set(plate, e.displayName.ToUpperInvariant(), 0f, 0f, 200f);
        plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        plate.rectTransform.pivot = new Vector2(0.5f, 0f);
        plate.rectTransform.anchoredPosition = Vector2.zero;
        plate.color = Color.Lerp(HallUI.Ink, e.glow, 0.35f);
    }
}

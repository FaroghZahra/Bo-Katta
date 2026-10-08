using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class StoryPage
{
    public string title;
    [TextArea(2, 5)] public string text;
    public Sprite picture;

    public StoryPage() { }
    public StoryPage(string title, string text) { this.title = title; this.text = text; }
}

/// <summary>
/// Tells Noor's story: storybook intro/outro cards, per-level victory cards with kite-paper confetti,
/// Noor's speech bubbles at each beat, and the bottom objective line.
/// Builds its own overlay UI at runtime; all words live in the Inspector.
/// </summary>
public class StoryTeller : MonoBehaviour
{
    public static StoryTeller Instance { get; private set; }

    [Header("References")]
    public CharacterMovement noor;
    [Tooltip("Bottom-of-screen objective line (the existing 'Msgs' text).")]
    public TMP_Text objectiveText;
    [Tooltip("Rounded sprite used for cards and bubbles (Unity's built-in UISprite works well).")]
    public Sprite panelSprite;
    public TMP_FontAsset font;
    [Tooltip("Sprites thrown as confetti on victory cards (kite-paper scraps).")]
    public Sprite[] confettiSprites;

    [Header("Look")]
    public Color cardColor   = new Color(0.98f, 0.93f, 0.82f);
    public Color borderColor = new Color(0.93f, 0.62f, 0.13f);
    public Color titleColor  = new Color(0.78f, 0.20f, 0.35f);
    public Color bodyColor   = new Color(0.24f, 0.16f, 0.10f);
    public float charsPerSecond = 55f;

    [Header("Intro")]
    public bool playIntro = true;
    public StoryPage[] introPages =
    {
        new StoryPage("Lahore, the night before Basant",
            "Every spring the whole city climbs onto its rooftops, and the sky fills with a thousand patangs — paper kites dancing on the wind."),
        new StoryPage("Meet Noor",
            "Noor lives in Androon Shehr, the old Walled City, with her Dada — the finest patang-saaz (kite maker) in all the galis."),
        new StoryPage("Uh-oh.",
            "Last night a storm tore Dada's favourite kite, Guddi, to pieces — and blew her scraps all over the mohalla."),
        new StoryPage("Dada's secret",
            "“A kite is just paper and patience, puttar,” Dada smiles. “Find every scrap. And don't forget my golden charkhi of dor.”"),
        new StoryPage("How to play",
            "Noor walks on her own. When she stops, tap the pots, blinds and doors in her way to uncover hidden kite paper — then tap the paper to collect it."),
    };
    public string startLine = "Guddi, I'm coming for you!";
    public string startObjective = "Follow Noor through the galis of the Walled City";

    [Header("Noor's lines")]
    [Tooltip("Said at each stop point, in order.")]
    [TextArea] public string[] stopLines =
    {
        "Hmm… kite paper LOVES hiding. Behind pots, under blinds — I'm checking everything!",
        "The old tower! Bricks, bamboo, a pigeon cage… somebody hid my scraps up here.",
        "Wazir Khan's courtyard! Shh — the pigeons are sitting on something…",
        "All aboard! Next stop: the sky.",
    };
    [Tooltip("Objective line shown at each stop point, in order.")]
    public string[] stopObjectives =
    {
        "Tap the pots, the blind and the door to find kite paper — and Dada's golden charkhi",
        "Search the tower: tap the door, the bricks, the bamboo and the cage",
        "Tap the pigeons' kite paper — more will appear!",
        "Sail the Ravi!",
    };
    [TextArea] public string[] collectLines =
    {
        "One more scrap for Guddi!",
        "Shabash, Noor!",
        "Found you, sneaky scrap!",
        "Ooh — perfect colour for a kite tail.",
        "Dada will love this one.",
        "Paper and patience… mostly paper!",
        "Into the pocket you go!",
    };
    [TextArea] public string goldLine = "Dada's golden charkhi! Now Guddi has dor to fly on.";
    [TextArea] public string missingGoldLine = "All the paper… but where's Dada's golden charkhi?";
    [Tooltip("Said after each level's reward. Use | to split into several bubbles.")]
    [TextArea] public string[] levelCompleteLines =
    {
        "The bridge holds! Lei-glue and kite paper — Dada calls that jugaar.|Arre! A kabootar, all tangled in manjha. Hang on, little one!",
        "Free as a kite! Shabash, kabootar — show me the way!",
        "The kishti is mended! Hop in, Noor — the Ravi is waiting.",
    };
    [Tooltip("Objective after each level completes.")]
    public string[] levelCompleteObjectives =
    {
        "Keep following Noor",
        "Follow the kabootar to the courtyard",
        "Walk Noor to the kishti",
    };

    [Header("Victory cards (one per level)")]
    public StoryPage[] victoryPages =
    {
        new StoryPage("Shabash!", "The cracked kashi tile is whole again, and the planks are folded into a bridge. One step closer to Guddi!"),
        new StoryPage("Wah, Noor!", "Guddi's paper is mended and her wings are open. She's almost ready to fly!"),
        new StoryPage("The kishti is whole!", "Every crack sealed in turquoise glaze. Grab the charkhi — the river is calling."),
    };
    public float victoryAutoClose = 4f;

    [Header("Outro")]
    public StoryPage[] outroPages =
    {
        new StoryPage("On the Ravi",
            "As the sun sinks behind the Badshahi Mosque, Noor lets out the dor — and Guddi leaps into the sky."),
        new StoryPage("BO KATA!",
            "Higher than every kite over Lahore! From a thousand rooftops the whole city cheers: “Bo kata!”"),
        new StoryPage("Paper and patience",
            "Back home, Dada laughs. “I told you, puttar.”\n\nHappy Basant! — The End"),
        new StoryPage("Words from Lahore",
            "<b>Basant</b> spring kite festival  •  <b>patang</b> kite  •  <b>dor</b> kite string  •  <b>charkhi</b> spool  •  <b>manjha</b> glass-coated string  •  <b>kabootar</b> pigeon  •  <b>kashi-kari</b> glazed tile work  •  <b>Bo kata!</b> “Cut!” — the cheer when a kite is cut loose"),
    };

    // ---- runtime UI ----
    private RectTransform _canvasRect;
    private GameObject _cardRoot;
    private RectTransform _card;
    private Image _cardPicture;
    private TextMeshProUGUI _cardTitle, _cardBody, _cardHint;
    private RectTransform _bubble;
    private TextMeshProUGUI _bubbleText;
    private RectTransform _confettiLayer;
    private Coroutine _bubbleRoutine;
    private int _stopIndex;
    private int _lastCollectLine = -1;
    private bool _cardOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (noor == null) noor = FindObjectOfType<CharacterMovement>();
        if (noor != null && playIntro) noor.waitForStoryIntro = true;
        BuildUI();
    }

    private void OnEnable()
    {
        GameEvents.OnBreakpointReached += HandleStop;
        GameEvents.OnItemCollected     += HandleCollected;
        GameEvents.OnLevelProgression  += HandleLevelComplete;
    }

    private void OnDisable()
    {
        GameEvents.OnBreakpointReached -= HandleStop;
        GameEvents.OnItemCollected     -= HandleCollected;
        GameEvents.OnLevelProgression  -= HandleLevelComplete;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DOTween.Kill(this);
    }

    private IEnumerator Start()
    {
        if (!playIntro) { SetObjective(startObjective); yield break; }
        SetObjective("");
        yield return ShowPages(introPages, autoClose: 0f);
        SetObjective(startObjective);
        noor?.ResumeMoving();
        yield return new WaitForSeconds(0.4f);
        Say(startLine);
    }

    // ------------------------------------------------------------------ public API

    public IEnumerator ShowVictory(int level)
    {
        int i = Mathf.Clamp(level - 1, 0, victoryPages.Length - 1);
        if (victoryPages.Length == 0) yield break;
        Confetti();
        yield return ShowPages(new[] { victoryPages[i] }, victoryAutoClose);
    }

    public IEnumerator PlayOutro()
    {
        HideBubble();
        SetObjective("");
        Confetti();
        yield return ShowPages(outroPages, autoClose: 0f);
    }

    /// <summary>
    /// Shows a line in Noor's bubble. Story lines queue behind each other; quick reactions
    /// (interrupt = true) replace whatever is showing but keep the queued story lines.
    /// </summary>
    public void Say(string line, float seconds = 0f, bool interrupt = false)
    {
        if (string.IsNullOrEmpty(line) || _bubble == null) return;
        var parts = line.Split('|');
        if (interrupt)
        {
            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            for (int i = parts.Length - 1; i >= 0; i--) _queue.Insert(0, (parts[i], seconds));
            _bubbleRoutine = StartCoroutine(SayRoutine());
            return;
        }
        foreach (var p in parts) _queue.Add((p, seconds));
        if (_bubbleRoutine == null) _bubbleRoutine = StartCoroutine(SayRoutine());
    }

    private readonly System.Collections.Generic.List<(string line, float seconds)> _queue =
        new System.Collections.Generic.List<(string, float)>();

    // ------------------------------------------------------------------ event hooks

    private void HandleStop()
    {
        int i = _stopIndex++;
        if (i < stopObjectives.Length) SetObjective(stopObjectives[i]);
        if (i < stopLines.Length) Say(stopLines[i]);
    }

    private void HandleCollected(bool wasGold, int collected, int total, bool goldCollected)
    {
        if (wasGold) { Say(goldLine, 0f, interrupt: true); return; }
        if (collected >= total && !goldCollected) { Say(missingGoldLine, 0f, interrupt: true); return; }
        if (collectLines.Length == 0) return;
        int pick = Random.Range(0, collectLines.Length);
        if (pick == _lastCollectLine) pick = (pick + 1) % collectLines.Length;
        _lastCollectLine = pick;
        Say(collectLines[pick], 1.8f, interrupt: true);
    }

    private void HandleLevelComplete(int level)
    {
        int i = level - 1;
        if (i >= 0 && i < levelCompleteObjectives.Length) SetObjective(levelCompleteObjectives[i]);
        if (i >= 0 && i < levelCompleteLines.Length) StartCoroutine(SayLater(levelCompleteLines[i], 1.2f));
    }

    private IEnumerator SayLater(string line, float delay)
    {
        yield return new WaitForSeconds(delay);
        Say(line);
    }

    private void SetObjective(string text)
    {
        if (objectiveText == null) return;
        objectiveText.text = text;
        objectiveText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    // ------------------------------------------------------------------ cards

    private IEnumerator ShowPages(StoryPage[] pages, float autoClose)
    {
        if (pages == null || pages.Length == 0) yield break;
        HideBubble();
        _cardOpen = true;
        _cardRoot.SetActive(true);
        GameObject objectiveRoot = objectiveText != null ? objectiveText.transform.parent.gameObject : null;
        bool objectiveWasShown = objectiveRoot != null && objectiveRoot.activeSelf;
        objectiveRoot?.SetActive(false);
        var dim = _cardRoot.GetComponent<Image>();
        dim.DOFade(0.72f, 0.3f).From(0f).SetTarget(this);

        for (int p = 0; p < pages.Length; p++)
        {
            var page = pages[p];
            _cardTitle.text = page.title;
            _cardBody.text = page.text;
            _cardPicture.sprite = page.picture;
            _cardPicture.gameObject.SetActive(page.picture != null);
            LayoutCard(page.picture != null);
            _cardHint.text = autoClose > 0f ? "" : (p < pages.Length - 1 ? "Tap to continue ›" : "Tap to begin ›");
            if (autoClose > 0f && p == pages.Length - 1) _cardHint.text = "";

            _card.localScale = Vector3.one * 0.85f;
            _card.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetTarget(this);
            _card.localRotation = Quaternion.Euler(0, 0, p % 2 == 0 ? -1.5f : 1.5f);

            // Typewriter; a tap finishes the text, the next tap turns the page.
            _cardBody.maxVisibleCharacters = 0;
            _cardBody.ForceMeshUpdate();
            int total = _cardBody.textInfo.characterCount;
            float shown = 0f;
            yield return null;
            while (shown < total)
            {
                if (Tapped()) { shown = total; break; }
                shown += charsPerSecond * Time.deltaTime;
                _cardBody.maxVisibleCharacters = Mathf.Min(total, (int)shown);
                yield return null;
            }
            _cardBody.maxVisibleCharacters = total;
            yield return null;

            float t = 0f;
            while (true)
            {
                if (Tapped()) break;
                if (autoClose > 0f && (t += Time.deltaTime) >= autoClose) break;
                yield return null;
            }
            yield return _card.DOScale(0.9f, 0.12f).SetTarget(this).WaitForCompletion();
        }

        yield return dim.DOFade(0f, 0.25f).SetTarget(this).WaitForCompletion();
        _cardRoot.SetActive(false);
        _cardOpen = false;
        if (objectiveWasShown) objectiveRoot.SetActive(true);
        yield return null;   // swallow the closing tap so it can't hit the world
    }

    private void LayoutCard(bool hasPicture)
    {
        float textLeft = hasPicture ? 560f : 70f;
        SetRect(_cardTitle.rectTransform, new Vector2(textLeft, -56f), new Vector2(1400f - textLeft - 70f, 76f));
        SetRect(_cardBody.rectTransform, new Vector2(textLeft, -160f), new Vector2(1400f - textLeft - 70f, 380f));
    }

    private bool _tapRequested;

    /// <summary>Same as a player tap: finishes the typewriter or turns the page (also usable from a UI Button).</summary>
    public void Advance() => _tapRequested = true;

    private bool Tapped()
    {
        if (_tapRequested) { _tapRequested = false; return true; }
        return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
               (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    // ------------------------------------------------------------------ bubble

    private IEnumerator SayRoutine()
    {
        while (_queue.Count > 0)
        {
            var (raw, seconds) = _queue[0];
            _queue.RemoveAt(0);
            string line = raw.Trim();
            if (line.Length == 0) continue;
            while (_cardOpen || MinigameOpen()) yield return null;

            _bubbleText.text = line;
            Vector2 size = _bubbleText.GetPreferredValues(line, 520f, 0f);
            size.x = Mathf.Min(size.x, 520f);
            _bubbleText.rectTransform.sizeDelta = size;
            _bubble.sizeDelta = size + new Vector2(48f, 32f);
            _bubble.gameObject.SetActive(true);
            _bubble.localScale = Vector3.zero;
            _bubble.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetTarget(this);

            float hold = seconds > 0f ? seconds : Mathf.Clamp(1.4f + line.Length * 0.045f, 2f, 5f);
            float t = 0f;
            while (t < hold && !_cardOpen && !MinigameOpen()) { t += Time.deltaTime; yield return null; }
            yield return _bubble.DOScale(0f, 0.15f).SetEase(Ease.InBack).SetTarget(this).WaitForCompletion();
            _bubble.gameObject.SetActive(false);
        }
        _bubbleRoutine = null;
    }

    private void HideBubble()
    {
        if (_bubbleRoutine != null) { StopCoroutine(_bubbleRoutine); _bubbleRoutine = null; }
        if (_bubble != null) _bubble.gameObject.SetActive(false);
        _queue.Clear();
    }

    private static bool MinigameOpen() => SceneManager.GetSceneByName("MiniGameScene").isLoaded;

    private void LateUpdate()
    {
        if (_bubble == null || !_bubble.gameObject.activeSelf || noor == null) return;
        var sr = noor.characterSprite;
        var cam = FindSceneCamera();
        if (cam == null) return;
        Vector3 head = sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0f) : noor.transform.position;
        Vector2 screen = cam.WorldToScreenPoint(head);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local);
        Vector2 half = _canvasRect.rect.size * 0.5f;
        Vector2 b = _bubble.sizeDelta;
        local.y += b.y * 0.5f + 28f;
        local.x = Mathf.Clamp(local.x, -half.x + b.x * 0.5f + 16f, half.x - b.x * 0.5f - 16f);
        local.y = Mathf.Clamp(local.y, -half.y + b.y * 0.5f + 16f, half.y - b.y * 0.5f - 130f);
        _bubble.anchoredPosition = local;
    }

    private Camera _cam;
    private Camera FindSceneCamera()
    {
        if (_cam != null) return _cam;
        foreach (var c in FindObjectsOfType<Camera>())
            if (c.gameObject.scene == gameObject.scene) { _cam = c; break; }
        return _cam;
    }

    // ------------------------------------------------------------------ confetti

    private void Confetti()
    {
        if (confettiSprites == null || confettiSprites.Length == 0) return;
        Vector2 half = _canvasRect.rect.size * 0.5f;
        for (int i = 0; i < 40; i++)
        {
            var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform; rt.SetParent(_confettiLayer, false);
            var img = go.GetComponent<Image>();
            img.sprite = confettiSprites[Random.Range(0, confettiSprites.Length)];
            img.preserveAspect = true; img.raycastTarget = false;
            rt.sizeDelta = Vector2.one * Random.Range(40f, 80f);
            rt.anchoredPosition = new Vector2(Random.Range(-half.x, half.x), half.y + Random.Range(20f, 300f));
            float dur = Random.Range(2.2f, 3.8f);
            rt.DOLocalMoveY(-half.y - 100f, dur).SetEase(Ease.InQuad).SetTarget(this);
            rt.DOLocalMoveX(rt.localPosition.x + Random.Range(-160f, 160f), dur).SetEase(Ease.InOutSine).SetTarget(this);
            rt.DOLocalRotate(new Vector3(0, 0, Random.Range(-540f, 540f)), dur, RotateMode.FastBeyond360).SetTarget(this)
              .OnComplete(() => Destroy(go));
        }
    }

    // ------------------------------------------------------------------ UI construction

    private void BuildUI()
    {
        var canvasGo = new GameObject("StoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasRect = (RectTransform)canvasGo.transform;

        // Speech bubble
        _bubble = NewImage("Bubble", _canvasRect, cardColor).rectTransform;
        _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, 0.5f);
        var bubbleBorder = _bubble.gameObject.AddComponent<Outline>();
        bubbleBorder.effectColor = borderColor; bubbleBorder.effectDistance = new Vector2(3, -3);
        var tail = NewImage("Tail", _bubble, cardColor).rectTransform;
        tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 0f);
        tail.sizeDelta = new Vector2(26, 26); tail.anchoredPosition = new Vector2(0, -4); tail.localRotation = Quaternion.Euler(0, 0, 45);
        tail.GetComponent<Image>().type = Image.Type.Simple;
        _bubbleText = NewText("Text", _bubble, 30, bodyColor, TextAlignmentOptions.Center);
        _bubbleText.rectTransform.anchorMin = _bubbleText.rectTransform.anchorMax = _bubbleText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _bubbleText.rectTransform.anchoredPosition = Vector2.zero;
        _bubble.gameObject.SetActive(false);

        // Card (full-screen dim + card)
        var dim = NewImage("StoryCard", _canvasRect, new Color(0.05f, 0.03f, 0.08f, 0.72f));
        dim.sprite = null;
        Stretch(dim.rectTransform);
        _cardRoot = dim.gameObject;

        var border = NewImage("Border", dim.rectTransform, borderColor).rectTransform;
        border.sizeDelta = new Vector2(1412, 632);
        _card = border;
        var inner = NewImage("Card", border, cardColor).rectTransform;
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(6, 6); inner.offsetMax = new Vector2(-6, -6);

        _cardPicture = NewImage("Picture", inner, Color.white);
        _cardPicture.sprite = null; _cardPicture.type = Image.Type.Simple; _cardPicture.preserveAspect = true;
        var pr = _cardPicture.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f); pr.pivot = new Vector2(0f, 0.5f);
        pr.anchoredPosition = new Vector2(50, 0); pr.sizeDelta = new Vector2(470, 520);

        _cardTitle = NewText("Title", inner, 54, titleColor, TextAlignmentOptions.TopLeft);
        _cardTitle.fontStyle = FontStyles.Bold;
        _cardTitle.enableWordWrapping = false;
        _cardTitle.enableAutoSizing = true; _cardTitle.fontSizeMin = 34; _cardTitle.fontSizeMax = 54;
        _cardBody = NewText("Body", inner, 38, bodyColor, TextAlignmentOptions.TopLeft);
        _cardBody.lineSpacing = 8f;
        _cardHint = NewText("Hint", inner, 26, new Color(0.55f, 0.40f, 0.20f), TextAlignmentOptions.BottomRight);
        _cardHint.fontStyle = FontStyles.Italic;
        var hr = _cardHint.rectTransform;
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 0f);
        hr.anchoredPosition = new Vector2(-40, 26); hr.sizeDelta = new Vector2(500, 40);
        _cardRoot.SetActive(false);

        // Confetti on top of everything
        var cl = new GameObject("Confetti", typeof(RectTransform));
        _confettiLayer = (RectTransform)cl.transform; _confettiLayer.SetParent(_canvasRect, false);
        Stretch(_confettiLayer);
    }

    private Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = panelSprite; img.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color; img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color; t.alignment = align;
        t.enableWordWrapping = true; t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        return t;
    }

    private static void SetRect(RectTransform rt, Vector2 pos, Vector2 size) { rt.anchoredPosition = pos; rt.sizeDelta = size; }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}

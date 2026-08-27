using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Seed picker shown when the player uses an empty plot.
///
/// Skinned with the sprite sets in Game/Sprites/Buttons. The hierarchy is built in code
/// so there is no fragile prefab wiring, but every sprite and font comes from a serialized
/// reference rather than a runtime path lookup.
/// </summary>
public class SeedMenu : MonoBehaviour
{
    public static SeedMenu Instance { get; private set; }

    /// <summary>True while the menu is up, so gameplay input can ignore Interact.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Content")]
    public CropDef[] crops;

    [Header("Skin - panel")]
    public Sprite panelSprite;
    public Sprite titleSprite;
    public Sprite innerSprite;
    public Color panelTint = new Color(0.16f, 0.18f, 0.26f, 0.98f);
    public Color innerTint = new Color(0.10f, 0.12f, 0.18f, 0.95f);

    [Header("Skin - buttons (one per crop, cycled)")]
    public ButtonSkin[] cropSkins;
    public ButtonSkin cancelSkin;

    [Header("Text")]
    public TMP_FontAsset font;
    public Color titleColor = new Color(1f, 0.97f, 0.88f);
    public Color buttonTextColor = new Color(1f, 0.98f, 0.92f);
    public Color coinColor = new Color(1f, 0.84f, 0.36f);
    [Tooltip("SDF outline so one text colour reads on every button colour.")]
    public Color outlineColor = new Color(0.08f, 0.07f, 0.12f, 1f);
    [Range(0f, 1f)] public float outlineWidth = 0.2f;

    GameObject _backdrop;
    TextMeshProUGUI _coinLabel;
    SoilPlot _target;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
        Close();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsOpen = false;
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("SeedMenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;   // sane in both portrait and landscape

        _backdrop = NewUI("Backdrop", canvasGo.transform, typeof(Image));
        var dim = _backdrop.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(_backdrop.GetComponent<RectTransform>());

        var panel = NewUI("Panel", _backdrop.transform,
            typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        Skin(panel.GetComponent<Image>(), panelSprite, panelTint);

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 28, 32);
        layout.spacing = 14f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;

        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(640f, 0f);

        var title = NewUI("TitleBar", panel.transform, typeof(Image), typeof(LayoutElement));
        Skin(title.GetComponent<Image>(), titleSprite, Color.white);
        title.GetComponent<LayoutElement>().preferredHeight = 96f;
        AddCenteredText(title.transform, "PLANT WHAT?", 46f, titleColor);

        var inner = NewUI("CoinBar", panel.transform, typeof(Image), typeof(LayoutElement));
        Skin(inner.GetComponent<Image>(), innerSprite, innerTint);
        inner.GetComponent<LayoutElement>().preferredHeight = 66f;
        _coinLabel = AddCenteredText(inner.transform, "", 34f, coinColor);

        if (crops != null)
        {
            for (int i = 0; i < crops.Length; i++)
            {
                var crop = crops[i];
                if (crop == null) continue;
                var skin = PickSkin(cropSkins, i);
                string label = string.Format("{0}   {1}c   {2}s",
                    crop.displayName.ToUpperInvariant(), crop.seedCost, Mathf.RoundToInt(crop.growSeconds));
                MakeButton(panel.transform, label, skin, () => Choose(crop));
            }
        }

        MakeButton(panel.transform, "CANCEL", cancelSkin, Close);
    }

    static ButtonSkin PickSkin(ButtonSkin[] arr, int i)
    {
        if (arr == null || arr.Length == 0) return null;
        return arr[i % arr.Length];
    }

    static GameObject NewUI(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, comps);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Skin(Image img, Sprite sprite, Color tint)
    {
        img.color = tint;
        if (sprite == null) return;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
    }

    TextMeshProUGUI AddCenteredText(Transform parent, string content, float size, Color color)
    {
        var go = NewUI("Text", parent, typeof(TextMeshProUGUI));
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;   // labels never need to catch clicks
        Stretch(go.GetComponent<RectTransform>());
        return t;
    }

    void MakeButton(Transform parent, string label, ButtonSkin skin, UnityEngine.Events.UnityAction onClick)
    {
        var go = NewUI("Button", parent, typeof(Image), typeof(Button), typeof(LayoutElement));

        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();

        if (skin != null && skin.IsValid) skin.ApplyTo(btn, img);
        else img.color = new Color(0.3f, 0.34f, 0.44f);

        go.GetComponent<LayoutElement>().preferredHeight = 110f;   // well above the 88px touch minimum
        btn.onClick.AddListener(onClick);

        // nudged up: these sprites carry a thicker bottom lip
        var txt = AddCenteredText(go.transform, label, 36f, buttonTextColor);
        var trt = txt.GetComponent<RectTransform>();
        trt.offsetMin = new Vector2(0f, 10f);
        trt.offsetMax = new Vector2(0f, -2f);

        // SDF outline, so a single text colour reads on gold, navy, red and white alike
        txt.outlineColor = outlineColor;
        txt.outlineWidth = outlineWidth;
    }

    public void Open(SoilPlot plot)
    {
        _target = plot;
        _backdrop.SetActive(true);
        IsOpen = true;
        RefreshCoins();
    }

    public void Close()
    {
        _target = null;
        if (_backdrop != null) _backdrop.SetActive(false);
        IsOpen = false;
    }

    void RefreshCoins()
    {
        if (_coinLabel != null) _coinLabel.text = Inventory.Coins + " COINS";
    }

    void Choose(CropDef crop)
    {
        if (_target == null) { Close(); return; }

        if (_target.TryPlant(crop))
            Close();
        else if (_coinLabel != null)
            _coinLabel.text = "NOT ENOUGH - " + Inventory.Coins + " COINS";
    }
}

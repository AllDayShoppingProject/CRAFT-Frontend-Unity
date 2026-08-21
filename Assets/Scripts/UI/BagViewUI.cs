using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// 가방 상세 보기(SCENE_03_PRODUCT_POPUP) 사이드 패널. Figma 시안(1120 × 1800)을 그대로 옮겼고,
/// 제품 6종이 구조가 같아 프리팹을 나누지 않고 이 패널 하나를 데이터로 채운다.
/// 프리팹으로 구우면 그 구조를 쓰고, 없으면 BuildHierarchy()가 런타임에 만든다.
/// 버튼 콜백은 직렬화되지 않으므로 어느 경우든 WireEvents()에서 매번 다시 연결한다.
public class BagViewUI : MonoBehaviour {

    public static BagViewUI Instance { get; private set; }

    // ---------------------------------------------------------------- 시안 수치

    /// 시안 높이 1800을 패널 높이 1080에 맞춘 배율(폭 1120 × 0.6 = 672px → 화면의 35%).
    /// 아래 상수는 시안 px로 적고 S()로 변환해서 쓴다.
    private const float DESIGN_SCALE = 0.6f;

    public const float PANEL_WIDTH_RATIO = 0.35f;

    private const float PAD_LEFT = 64f;
    private const float PAD_RIGHT = 72f;
    private const float PAD_TOP = 78f;

    private const float NAME_SIZE = 42f;
    private const float PRICE_SIZE = 34f;
    private const float LABEL_SIZE = 26f;
    private const float SECTION_SIZE = 26f;
    private const float BODY_SIZE = 22f;
    private const float DETAIL_SIZE = 26f;
    private const float BUTTON_LABEL_SIZE = 24f;

    /// 마름모 한 개가 차지하는 사각형(대각선 길이)
    private const float SWATCH_BOX = 46f;
    private const float SWATCH_GAP = 26f;

    private const float BUTTON_HEIGHT = 95f;
    private const float BUTTON_GAP = 16f;
    private const float BUTTON_RADIUS = 12f;

    /// 피팅 모드의 키 선택 칩 (150 · 160 · …)
    private const float HEIGHT_CHIP_WIDTH = 82f;
    private const float HEIGHT_CHIP_HEIGHT = 58f;
    private const float HEIGHT_CHIP_GAP = 20f;

    private const float CLOSE_BOX = 48f;

    /// 시안 px → 캔버스 px
    private static float S(float designPixels) => designPixels * DESIGN_SCALE;

    // ---------------------------------------------------------------- 팔레트

    private static readonly Color PanelBg   = Color.white;
    private static readonly Color Ink       = new Color(0.10f, 0.10f, 0.10f, 1f);
    private static readonly Color InkMuted  = new Color(0.42f, 0.42f, 0.44f, 1f);
    private static readonly Color ButtonBg  = new Color(0.14f, 0.14f, 0.14f, 1f);
    private static readonly Color ButtonInk = Color.white;

    [Header("자동 생성/프리팹 참조 (직접 건드릴 필요 없음)")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform sidePanel;

    /// 상세정보를 펼치면 패널 높이를 넘으므로, 패널이 아니라 이 스크롤 내용물 안에 요소를 쌓는다.
    [SerializeField] private RectTransform panelContent;
    [SerializeField] private ScrollRect panelScroll;

    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI colorNameText;
    [SerializeField] private TextMeshProUGUI sizeText;
    [SerializeField] private RectTransform swatchRow;

    [SerializeField] private Button detailHeaderButton;
    [SerializeField] private RectTransform detailChevron;
    [SerializeField] private GameObject detailBody;
    [SerializeField] private TextMeshProUGUI detailText;

    // 예전 이름(sizeButton)으로 구워둔 프리팹이 있어도 참조가 끊기지 않도록
    [FormerlySerializedAs("sizeButton")]
    [SerializeField] private Button tryOnButton;
    [SerializeField] private Button preregButton;
    [SerializeField] private Button closeButton;

    [SerializeField] private GameObject preregForm;
    [SerializeField] private ScrollRect preregScroll;
    [SerializeField] private RawImage preregShot;
    [SerializeField] private TextMeshProUGUI preregNameText;
    [SerializeField] private TextMeshProUGUI preregPriceText;
    [SerializeField] private TextMeshProUGUI preregColorText;
    [SerializeField] private TextMeshProUGUI preregSizeText;
    [SerializeField] private TMP_InputField nameField;
    [SerializeField] private TMP_InputField phoneField;
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private Toggle consentToggle;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button cancelButton;

    [SerializeField] private GameObject preregDone;
    [SerializeField] private RawImage doneShot;
    private AspectRatioFitter doneShotFitter;
    [SerializeField] private TextMeshProUGUI doneNameText;
    [SerializeField] private TextMeshProUGUI donePriceText;
    [SerializeField] private TextMeshProUGUI doneColorText;
    [SerializeField] private TextMeshProUGUI doneSizeText;
    [SerializeField] private Button doneButton;

    /// 피팅 모드에서 자리를 비켜주는 요소들(그 자리에 키 선택이 들어간다).
    [SerializeField] private RectTransform gapDetail;
    [SerializeField] private RectTransform buttonRow;

    [SerializeField] private GameObject fittingSection;
    [SerializeField] private TextMeshProUGUI heightText;
    [SerializeField] private RectTransform heightRow;
    [SerializeField] private Button fittingDoneButton;

    [SerializeField] private GameObject toastObject;
    [SerializeField] private TextMeshProUGUI toastText;

    private Coroutine toastRoutine;

    /// 마름모 칩 하나가 쓰는 이미지 세 장. 선택 상태에 따라 크기/색만 바꾼다.
    private class Swatch {
        public string code;
        public Image ring;
        public Image gap;
        public Image fill;
        public Color color;
        public bool light;      // 흰색 계열이라 평소에도 테두리가 필요한지
    }

    private readonly List<Swatch> swatches = new List<Swatch>();

    /// 키 선택 칩 하나. 평소엔 숫자만, 선택되면 얇은 사각 테두리가 생긴다.
    private class HeightChip {
        public int height;
        public Image box;
        public TextMeshProUGUI label;
    }

    private readonly List<HeightChip> heightChips = new List<HeightChip>();

    private ProductData currentProduct;
    private Transform currentBagRoot;
    private ProductColorOption currentColor;

    /// 피팅 모드에서 고른 아바타 키(cm)
    private int currentHeight = TryOnController.DEFAULT_HEIGHT;

    private bool fittingMode;

    /// 상세정보(헤리티지) 아코디언을 펼친 시각. -1이면 접혀 있는 상태.
    private float detailExpandStartTime = -1f;

    private Action<ProductColorOption> onColorChanged;
    private Action onTryOnRequested;
    private Action onCloseRequested;
    private Action<int> onHeightChanged;
    private Action onFittingEndRequested;

    public bool IsFittingMode => fittingMode;

    public bool IsVisible => root != null && root.activeSelf;

    /// 현재 선택된 컬러. Show()에서 자동 선택되므로 콜백만 기다리면 놓칠 수 있어 열어둔다.
    public ProductColorOption CurrentColor => currentColor;

    private bool swatchesLocked;//시착모드에서 색상칩 비활성화용

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        UIFactory.Font = ResolveKoreanFont();
        EnsureEventSystem();

        // 예전 레이아웃으로 구운 프리팹은 새로 생긴 참조가 비어 Show()에서 터진다. 통째로 다시 만든다.
        if (root != null && (sizeText == null || detailText == null
                             || panelContent == null || emailField == null
                             || preregDone == null || fittingSection == null)) {
            Debug.LogWarning("[BagViewUI] 예전 레이아웃 프리팹이 감지되어 UI를 다시 만듭니다. " +
                             "Tools > 프리팹 굽기 로 다시 구우면 이 경고가 사라집니다.");
            Destroy(root);
            root = null;
        }

        if (root == null) BuildHierarchy();

        // 프리팹에는 구울 당시 폰트가 박혀 있어 한글이 네모로 나온다. 실행할 때마다 다시 입힌다.
        ApplyFontToAllText();
        WireEvents();

        root.SetActive(false);
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    /// 한글 TMP 폰트를 찾는다. 없으면 기본 폰트로 폴백한다(한글이 네모로 보일 수 있음).
    private TMP_FontAsset ResolveKoreanFont() {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/KoreanSDF");
        if (font != null) return font;

        if (TMP_Settings.defaultFontAsset != null) {
            Debug.LogWarning("[BagViewUI] 한글 TMP 폰트 에셋이 없어 기본 폰트를 사용합니다. " +
                             "메뉴의 Tools > 한글 TMP 폰트 생성 을 한 번 실행해 주세요.");
            return TMP_Settings.defaultFontAsset;
        }

        Debug.LogError("[BagViewUI] TMP 폰트가 없습니다. Window > TextMeshPro > Import TMP Essential Resources 를 먼저 실행하세요.");
        return null;
    }

    private void ApplyFontToAllText() {
        if (UIFactory.Font == null) return;

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) {
            text.font = UIFactory.Font;
        }

        foreach (TMP_InputField field in GetComponentsInChildren<TMP_InputField>(true)) {
            field.fontAsset = UIFactory.Font;
        }
    }

    private void EnsureEventSystem() {
        if (EventSystem.current != null) return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    // ---------------------------------------------------------------- 구조 생성

    /// 에디터의 프리팹 굽기 도구에서도 호출한다.
    public void BuildHierarchy() {
        if (UIFactory.Font == null) UIFactory.Font = ResolveKoreanFont();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        if (GetComponent<CanvasScaler>() == null) {
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        root = UIFactory.CreateRect("BagViewRoot", transform).gameObject;
        UIFactory.Stretch(root.GetComponent<RectTransform>());

        BuildSidePanel();
        BuildCloseButton();
        BuildPreregForm();
        BuildPreregDone();
        BuildToast();
    }

    private void BuildSidePanel() {
        Image panelImage = UIFactory.CreateImage("SidePanel", root.transform, PanelBg);
        sidePanel = panelImage.rectTransform;
        sidePanel.anchorMin = new Vector2(1f - PANEL_WIDTH_RATIO, 0f);
        sidePanel.anchorMax = new Vector2(1f, 1f);
        sidePanel.offsetMin = Vector2.zero;
        sidePanel.offsetMax = Vector2.zero;

        // 상세정보를 펼치면 패널 높이를 넘으므로(명세서: 길이 초과 시 스크롤뷰) 패널을 스크롤 틀로 쓴다.
        RectTransform viewport = UIFactory.CreateRect("Viewport", sidePanel);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        panelContent = UIFactory.CreateRect("Content", viewport);
        panelContent.anchorMin = new Vector2(0f, 1f);
        panelContent.anchorMax = new Vector2(1f, 1f);
        panelContent.pivot = new Vector2(0.5f, 1f);
        panelContent.anchoredPosition = Vector2.zero;
        panelContent.sizeDelta = Vector2.zero;

        // 시안 수치를 그대로 옮기려고 spacing은 0으로 두고 간격은 스페이서로 명시한다.
        UIFactory.AddVerticalLayout(panelContent,
            new RectOffset((int)S(PAD_LEFT), (int)S(PAD_RIGHT), (int)S(PAD_TOP), (int)S(60f)), 0f);

        // 내용 높이에 맞춰 Content가 늘어나야 스크롤 범위가 잡힌다
        ContentSizeFitter fitter = panelContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        panelScroll = sidePanel.gameObject.AddComponent<ScrollRect>();
        panelScroll.viewport = viewport;
        panelScroll.content = panelContent;
        panelScroll.horizontal = false;
        panelScroll.vertical = true;
        panelScroll.movementType = ScrollRect.MovementType.Clamped;
        panelScroll.scrollSensitivity = 30f;

        nameText = UIFactory.CreateText("Name", panelContent, "", S(NAME_SIZE), FontStyles.Normal, Ink);

        Spacer("GapName", 40f);
        priceText = UIFactory.CreateText("Price", panelContent, "", S(PRICE_SIZE), FontStyles.Normal, Ink);

        Spacer("GapPrice", 30f);
        colorNameText = UIFactory.CreateText("ColorName", panelContent, "", S(LABEL_SIZE), FontStyles.Normal, Ink);

        Spacer("GapColor", 22f);
        swatchRow = UIFactory.CreateRect("Swatches", panelContent);
        UIFactory.AddHorizontalLayout(swatchRow, S(SWATCH_GAP));
        UIFactory.SetHeight(swatchRow, S(SWATCH_BOX));

        Spacer("GapSwatch", 24f);
        sizeText = UIFactory.CreateText("Size", panelContent, "", S(LABEL_SIZE), FontStyles.Normal, Ink);

        Spacer("GapSize", 56f);
        BuildDetailSection();

        gapDetail = Spacer("GapDetail", 48f);
        BuildButtonRow();

        // 피팅 모드에서만 켜지는 구역. 상세정보·하단 버튼과 자리를 맞바꾼다.
        BuildFittingSection();
    }

    /// "제품 상세정보 ∧" 헤더 + 접었다 펴는 본문
    private void BuildDetailSection() {
        RectTransform header = UIFactory.CreateRect("DetailHeader", panelContent);
        UIFactory.SetHeight(header, S(52f));

        // 헤더 전체를 클릭 영역으로 쓰려면 투명 이미지가 있어야 레이캐스트가 잡힌다
        Image hit = header.gameObject.AddComponent<Image>();
        hit.color = Color.clear;

        detailHeaderButton = header.gameObject.AddComponent<Button>();
        detailHeaderButton.targetGraphic = hit;
        detailHeaderButton.transition = Selectable.Transition.None;

        TextMeshProUGUI title = UIFactory.CreateText("Title", header, "제품 상세정보",
            S(SECTION_SIZE), FontStyles.Bold, Ink);
        UIFactory.Stretch(title.rectTransform);
        title.alignment = TextAlignmentOptions.MidlineLeft;

        detailChevron = BuildChevron(header);

        detailBody = UIFactory.CreateRect("DetailBody", panelContent).gameObject;
        UIFactory.AddVerticalLayout(detailBody.transform, new RectOffset(0, 0, (int)S(24f), 0), 0f);

        // 줄바꿈·글머리표(•)·리치 텍스트 태그가 Seed.detailedInfo 문자열에 그대로 들어 있다.
        detailText = UIFactory.CreateText("DetailText", detailBody.transform, "",
            S(DETAIL_SIZE), FontStyles.Normal, Ink);
        detailText.richText = true;
        detailText.lineSpacing = 18f;
        detailText.paragraphSpacing = 12f;
    }

    /// 글리프(∧, ▲)는 한글 폰트에 없으면 네모로 나와서, 꺾쇠를 이미지 두 장으로 만든다.
    private RectTransform BuildChevron(RectTransform parent) {
        RectTransform chevron = UIFactory.CreateRect("Chevron", parent);
        chevron.anchorMin = new Vector2(1f, 0.5f);
        chevron.anchorMax = new Vector2(1f, 0.5f);
        chevron.pivot = new Vector2(1f, 0.5f);
        chevron.anchoredPosition = Vector2.zero;
        chevron.sizeDelta = new Vector2(S(34f), S(20f));

        CreateChevronBar(chevron, -45f, -S(8f));
        CreateChevronBar(chevron, 45f, S(8f));

        return chevron;
    }

    private void CreateChevronBar(RectTransform parent, float angle, float offsetX) {
        Image bar = UIFactory.CreateImage("Bar", parent, Ink);
        bar.raycastTarget = false;

        RectTransform rect = bar.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(S(24f), S(2.5f));
        rect.anchoredPosition = new Vector2(offsetX, 0f);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void BuildButtonRow() {
        RectTransform row = UIFactory.CreateRect("ButtonRow", panelContent);
        buttonRow = row;
        UIFactory.SetHeight(row, S(BUTTON_HEIGHT));

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = S(BUTTON_GAP);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        // 시안에서 왼쪽 버튼이 더 좁다 (대략 35 : 65)
        tryOnButton = CreateFlatButton("TryOnButton", row, "피팅 시작하기", 0.35f);
        preregButton = CreateFlatButton("PreregButton", row, "사전 예약하기", 0.65f);
    }

    // ---------------------------------------------------------------- 피팅 구역

    /// 시안의 "Height : 170cm + 키 칩 + 피팅 끝내기". 상세정보·하단 버튼 자리를 물려받는다.
    /// 상단(이름·가격·색상·스와치·사이즈)이 상세 보기와 같아 별도 캔버스로 나누지 않는다.
    private void BuildFittingSection() {
        RectTransform section = UIFactory.CreateRect("FittingSection", panelContent);
        UIFactory.AddVerticalLayout(section, new RectOffset(0, 0, 0, 0), 0f);

        heightText = UIFactory.CreateText("Height", section, "", S(LABEL_SIZE), FontStyles.Normal, Ink);

        SectionSpacer(section, "GapHeightLabel", 26f);

        heightRow = UIFactory.CreateRect("HeightRow", section);
        UIFactory.AddHorizontalLayout(heightRow, S(HEIGHT_CHIP_GAP));
        UIFactory.SetHeight(heightRow, S(HEIGHT_CHIP_HEIGHT));

        // 시안에서 키 줄과 버튼 사이가 눈에 띄게 벌어져 있다
        SectionSpacer(section, "GapHeightRow", 96f);

        fittingDoneButton = CreateFlatButton("FittingDoneButton", section, "피팅 끝내기", 1f);

        LayoutElement doneLayout = fittingDoneButton.GetComponent<LayoutElement>();
        doneLayout.minHeight = doneLayout.preferredHeight = S(BUTTON_HEIGHT);

        fittingSection = section.gameObject;
        fittingSection.SetActive(false);
    }

    /// Spacer()는 panelContent 전용이라, 임의 부모용으로 따로 둔다.
    private void SectionSpacer(Transform parent, string name, float designHeight) {
        RectTransform spacer = UIFactory.CreateRect(name, parent);
        UIFactory.SetHeight(spacer, S(designHeight));
    }

    /// 허용 신장 목록은 ProjectConfig 한 곳에서만 관리한다(여기에 숫자를 박으면 어긋난다).
    private void BuildHeightChips() {
        for (int i = heightRow.childCount - 1; i >= 0; i--) {
            Destroy(heightRow.GetChild(i).gameObject);
        }
        heightChips.Clear();

        foreach (int option in ProjectConfig.AllowedHeights) {
            heightChips.Add(CreateHeightChip(option));
        }
    }

    private HeightChip CreateHeightChip(int height) {
        // 테두리 이미지가 곧 클릭 판정 영역이다. 평소엔 투명하지만 레이캐스트는 받는다.
        Image box = UIFactory.CreateImage($"Height_{height}", heightRow, Color.clear);
        box.rectTransform.sizeDelta = new Vector2(S(HEIGHT_CHIP_WIDTH), S(HEIGHT_CHIP_HEIGHT));
        box.sprite = OutlineSprite();
        box.type = Image.Type.Sliced;

        TextMeshProUGUI label = UIFactory.CreateText("Label", box.rectTransform, height.ToString(),
            S(LABEL_SIZE), FontStyles.Normal, Ink);
        UIFactory.Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;

        Button button = box.gameObject.AddComponent<Button>();
        button.targetGraphic = box;
        button.transition = Selectable.Transition.None;

        int captured = height;
        button.onClick.AddListener(() => SelectHeight(captured));

        var chip = new HeightChip { height = height, box = box, label = label };
        ApplyHeightChipState(chip, false);

        return chip;
    }

    private void ApplyHeightChipState(HeightChip chip, bool selected) {
        // 시안에서 글자색은 그대로고 선택된 것만 테두리가 생긴다
        chip.box.color = selected ? Ink : Color.clear;
    }

    private Button CreateFlatButton(string name, Transform parent, string label, float weight) {
        Button button = UIFactory.CreateButton(name, parent, label, ButtonBg, ButtonInk, null);

        Image background = button.GetComponent<Image>();
        background.sprite = RoundedSprite();
        background.type = Image.Type.Sliced;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        text.fontSize = S(BUTTON_LABEL_SIZE);
        text.fontStyle = FontStyles.Bold;

        // 뒤에서 높이를 또 지정하는 자리가 있어, 두 번 붙이지 말고 반드시 재사용해야 한다.
        LayoutElement element = button.GetComponent<LayoutElement>();
        if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = weight;

        return button;
    }

    /// 우상단 나가기 버튼. 상세 보기에서 자유 이동으로 돌아가는 유일한 방법이다.
    /// 피팅 모드에서는 EnterFittingMode()가 숨겨서 [피팅 끝내기]로만 나가게 한다.
    private void BuildCloseButton() {
        closeButton = UIFactory.CreateButton("CloseButton", sidePanel, "×", Color.clear, Ink, null);
        closeButton.transition = Selectable.Transition.None;

        RectTransform rect = closeButton.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-S(40f), -S(34f));
        rect.sizeDelta = new Vector2(S(CLOSE_BOX), S(CLOSE_BOX));

        // 나중에 패널에 레이아웃 그룹이 붙어도 시안 위치가 흔들리지 않도록 흐름에서 뺀다.
        closeButton.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        TextMeshProUGUI label = closeButton.GetComponentInChildren<TextMeshProUGUI>();
        label.fontSize = S(44f);
        label.fontStyle = FontStyles.Normal;
    }

    // ---------------------------------------------------------------- 둥근 모서리 스프라이트

    private static Sprite roundedSprite;
    private static Sprite circleSprite;
    private static Sprite outlineSprite;

    /// 선택된 키 칩의 사각 테두리. 9-slice로 구워서 칩 크기가 바뀌어도 두께가 일정하다.
    private static Sprite OutlineSprite() {
        if (outlineSprite != null) return outlineSprite;

        const int border = 2;
        int size = border * 2 + 2;   // 가운데 2px이 늘어나는 구간

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                bool edge = x < border || y < border
                         || x >= size - border || y >= size - border;

                pixels[y * size + x] = new Color32(255, 255, 255, edge ? (byte)255 : (byte)0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        outlineSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));

        return outlineSprite;
    }

    /// 동의 체크용 정원. 9-slice가 아니라 통째로 늘어나야 해서 RoundedSprite와 따로 굽는다.
    private static Sprite CircleSprite() {
        if (circleSprite != null) return circleSprite;

        const int size = 128;
        float radius = size * 0.5f;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;

                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return circleSprite;
    }

    /// 버튼 모서리용 9-slice 스프라이트. 에셋 없이 굽고, 버튼 크기가 달라져도 반경이 유지된다.
    private static Sprite RoundedSprite() {
        if (roundedSprite != null) return roundedSprite;

        int radius = Mathf.Max(2, Mathf.RoundToInt(S(BUTTON_RADIUS)));
        int size = radius * 2 + 2;   // 가운데 2px은 늘어나는 구간

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x < radius ? radius - x - 0.5f : (x >= size - radius ? x - (size - radius) + 0.5f : 0f);
                float dy = y < radius ? radius - y - 0.5f : (y >= size - radius ? y - (size - radius) + 0.5f : 0f);

                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // 경계 1px을 부드럽게 깎아 계단 현상을 줄인다
                float alpha = Mathf.Clamp01(radius - distance);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));

        return roundedSprite;
    }

    private RectTransform Spacer(string name, float designHeight) {
        RectTransform spacer = UIFactory.CreateRect(name, panelContent);
        UIFactory.SetHeight(spacer, S(designHeight));
        return spacer;
    }

    private void BuildPreregForm() {
        Image formImage = UIFactory.CreateImage("PreregForm", root.transform, PanelBg);
        RectTransform form = formImage.rectTransform;
        form.anchorMin = new Vector2(1f - PANEL_WIDTH_RATIO, 0f);
        form.anchorMax = new Vector2(1f, 1f);
        form.offsetMin = Vector2.zero;
        form.offsetMax = Vector2.zero;

        // 동의서 문구까지 들어가면 패널 높이를 넘겨 상세 보기와 같은 스크롤 구조를 쓴다.
        RectTransform viewport = UIFactory.CreateRect("Viewport", form);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = UIFactory.CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        UIFactory.AddVerticalLayout(content,
            new RectOffset((int)S(PAD_LEFT), (int)S(PAD_RIGHT), (int)S(PAD_TOP), (int)S(60f)), 0f);

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        preregScroll = form.gameObject.AddComponent<ScrollRect>();
        preregScroll.viewport = viewport;
        preregScroll.content = content;
        preregScroll.horizontal = false;
        preregScroll.vertical = true;
        preregScroll.movementType = ScrollRect.MovementType.Clamped;
        preregScroll.scrollSensitivity = 30f;

        UIFactory.CreateText("FormTitle", content, "사전 예약", S(SECTION_SIZE), FontStyles.Bold, Ink);

        FormSpacer(content, "GapTitle", 60f);
        BuildPreregProductCard(content);

        FormSpacer(content, "GapCard", 90f);
        UIFactory.CreateText("ContactTitle", content, "연락처 정보 입력",
            S(SECTION_SIZE), FontStyles.Bold, Ink);

        FormSpacer(content, "GapContact", 46f);
        nameField  = BuildUnderlineField(content, "NameField", "성명",
            TMP_InputField.ContentType.Standard, 20);
        phoneField = BuildUnderlineField(content, "PhoneField", "전화번호",
            TMP_InputField.ContentType.IntegerNumber, 11);
        emailField = BuildUnderlineField(content, "EmailField", "이메일",
            TMP_InputField.ContentType.EmailAddress, 60);
        Debug.Log(nameField.text);

        FormSpacer(content, "GapFields", 56f);
        BuildConsentSection(content);

        FormSpacer(content, "GapConsent", 70f);
        submitButton = CreateFlatButton("SubmitButton", content, "사전 예약 등록 하기", 1f);
        submitButton.interactable = false;

        // CreateFlatButton이 이미 LayoutElement를 붙여뒀으므로 재사용한다
        LayoutElement submitLayout = submitButton.GetComponent<LayoutElement>();
        submitLayout.minHeight = submitLayout.preferredHeight = S(BUTTON_HEIGHT);

        FormSpacer(content, "GapSubmit", 20f);
        cancelButton = UIFactory.CreateButton("CancelButton", content, "취소", Color.clear, InkMuted, null);
        UIFactory.SetHeight(cancelButton, S(64f));

        preregForm = form.gameObject;
        preregForm.SetActive(false);
    }

    private void BuildPreregProductCard(RectTransform parent) {
        RectTransform card = UIFactory.CreateRect("ProductCard", parent);
        UIFactory.SetHeight(card, S(300f));

        HorizontalLayoutGroup layout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = S(40f);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;

        // 사진은 3D 모델을 그 자리에서 찍어 넣는다 (ProductShot 참고)
        preregShot = UIFactory.CreateRawImage("Shot", card, null);
        preregShot.raycastTarget = false;

        LayoutElement shotLayout = preregShot.gameObject.AddComponent<LayoutElement>();
        shotLayout.minWidth = shotLayout.preferredWidth = S(300f);
        shotLayout.flexibleWidth = 0f;

        RectTransform info = UIFactory.CreateRect("Info", card);
        UIFactory.AddVerticalLayout(info, new RectOffset(0, 0, (int)S(28f), 0), 0f);
        info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        preregNameText  = UIFactory.CreateText("Name", info, "", S(NAME_SIZE * 0.8f), FontStyles.Normal, Ink);
        FormSpacer(info, "GapName", 26f);
        preregPriceText = UIFactory.CreateText("Price", info, "", S(PRICE_SIZE), FontStyles.Normal, Ink);
        FormSpacer(info, "GapPrice", 22f);
        preregColorText = UIFactory.CreateText("Color", info, "", S(LABEL_SIZE), FontStyles.Normal, Ink);
        FormSpacer(info, "GapColor", 18f);
        preregSizeText  = UIFactory.CreateText("Size", info, "", S(LABEL_SIZE), FontStyles.Normal, Ink);
    }

    /// 시안의 입력칸은 상자가 아니라 밑줄 하나다.
    private TMP_InputField BuildUnderlineField(RectTransform parent, string name, string placeholder,
                                               TMP_InputField.ContentType contentType, int characterLimit) {
        RectTransform group = UIFactory.CreateRect($"{name}Group", parent);
        UIFactory.SetHeight(group, S(110f));

        TMP_InputField field = UIFactory.CreateInputField(name, group, placeholder,
                                                          contentType, characterLimit);
        field.gameObject.AddComponent<WebGLSupport.WebGLInput>();
        RectTransform rect = field.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -S(72f));
        rect.offsetMax = Vector2.zero;

        // UIFactory 기본값은 어두운 패널용 흰 글자라, 흰 배경에서는 상자를 지우고 색을 바꿔야 보인다.
        if (field.targetGraphic is Image background) background.color = Color.clear;

        if (field.textComponent != null) {
            field.textComponent.color = Ink;
            field.textComponent.fontSize = S(LABEL_SIZE);
        }

        if (field.placeholder is TextMeshProUGUI hint) {
            hint.color = new Color(0f, 0f, 0f, 0.55f);
            hint.fontSize = S(LABEL_SIZE);
        }

        Image underline = UIFactory.CreateImage("Underline", group, new Color(0f, 0f, 0f, 0.35f));
        RectTransform line = underline.rectTransform;
        line.anchorMin = new Vector2(0f, 1f);
        line.anchorMax = new Vector2(1f, 1f);
        line.pivot = new Vector2(0.5f, 1f);
        line.offsetMin = new Vector2(0f, -S(74f));
        line.offsetMax = new Vector2(0f, -S(72f));
        underline.raycastTarget = false;

        return field;
    }

    private void BuildConsentSection(RectTransform parent) {
        UIFactory.CreateText("ConsentTitle", parent, "개인정보 수집 동의서  (필수)",
            S(SECTION_SIZE), FontStyles.Bold, Ink);

        FormSpacer(parent, "GapConsentTitle", 28f);

        TextMeshProUGUI terms = UIFactory.CreateText("ConsentBody", parent,
            "*수집 항목 : 성명, 이메일, 전화번호, 사전등록 내역\n" +
            "*이용 목적 : MCM 제품 출시 유관 데이터 활용 및 사전 예약의 목적\n" +
            "*개인 정보의 보유 및 이용기간 : 다른 법령에서 정함이 없는 한, 개인정보의 수집 목적이 " +
            "달성되거나 더 이상 개인정보의 보관 필요성이 없다고 판단되는 시점까지",
            S(BODY_SIZE), FontStyles.Normal, Ink);
        terms.lineSpacing = 16f;

        FormSpacer(parent, "GapTerms", 34f);

        RectTransform row = UIFactory.CreateRect("ConsentRow", parent);
        UIFactory.SetHeight(row, S(56f));

        // 동그라미만 누르게 하면 지름 20px 남짓이라 잘 안 맞는다. 줄 전체가 Toggle이다.
        Image hit = row.gameObject.AddComponent<Image>();
        hit.color = Color.clear;

        consentToggle = row.gameObject.AddComponent<Toggle>();
        consentToggle.targetGraphic = hit;
        consentToggle.transition = Selectable.Transition.None;
        consentToggle.isOn = false;

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = S(18f);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;

        TextMeshProUGUI label = UIFactory.CreateText("ConsentLabel", row,
            "위 내용을 모두 확인했으며 개인정보 수집에 동의합니다.",
            S(BODY_SIZE), FontStyles.Normal, Ink);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;

        consentToggle.graphic = BuildCircleMark(row);
    }

    /// 클릭은 줄 전체가 받으므로 보이는 것만 만들고, 켜질 때 표시할 안쪽 원을 돌려준다.
    private Graphic BuildCircleMark(RectTransform parent) {
        RectTransform holder = UIFactory.CreateRect("ConsentMark", parent);

        LayoutElement layout = holder.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = S(40f);
        layout.flexibleWidth = 0f;

        Image ring = UIFactory.CreateImage("Ring", holder, Ink);
        CenterSquare(ring.rectTransform, S(34f));
        ring.sprite = CircleSprite();
        ring.raycastTarget = false;

        Image hole = UIFactory.CreateImage("Hole", holder, PanelBg);
        CenterSquare(hole.rectTransform, S(28f));
        hole.sprite = CircleSprite();
        hole.raycastTarget = false;

        Image check = UIFactory.CreateImage("Checkmark", holder, Ink);
        CenterSquare(check.rectTransform, S(18f));
        check.sprite = CircleSprite();
        check.raycastTarget = false;

        return check;
    }

    private static void CenterSquare(RectTransform rect, float size) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
    }

    /// 사전 예약 완료 화면. 폼과 같은 자리에 겹쳐 띄우고, 입력 요소 없이 제품 카드만 보여준다.
    private void BuildPreregDone() {
        Image doneImage = UIFactory.CreateImage("PreregDone", root.transform, PanelBg);
        RectTransform done = doneImage.rectTransform;
        done.anchorMin = new Vector2(1f - PANEL_WIDTH_RATIO, 0f);
        done.anchorMax = new Vector2(1f, 1f);
        done.offsetMin = Vector2.zero;
        done.offsetMax = Vector2.zero;

        UIFactory.AddVerticalLayout(done,
            new RectOffset((int)S(PAD_LEFT), (int)S(PAD_RIGHT), (int)S(PAD_TOP), (int)S(60f)), 0f);

        FormSpacer(done, "GapTop", 380f);

        TextMeshProUGUI message = UIFactory.CreateText("Message", done,
            "사전 예약 등록이 완료되었습니다.", S(NAME_SIZE), FontStyles.Normal, Ink);
        message.alignment = TextAlignmentOptions.Center;

        FormSpacer(done, "GapMessage", 70f);
        BuildDoneCard(done);

        // 버튼을 화면 아래로 밀어낸다
        RectTransform spacer = UIFactory.CreateRect("Spacer", done);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        doneButton = CreateFlatButton("DoneButton", done, "돌아가기", 1f);

        LayoutElement doneLayout = doneButton.GetComponent<LayoutElement>();
        doneLayout.minHeight = doneLayout.preferredHeight = S(BUTTON_HEIGHT);

        preregDone = done.gameObject;
        preregDone.SetActive(false);
    }

    /// 완료 화면의 제품 카드. 시안에서 옅은 회색 판 위에 얹혀 있다.
    private void BuildDoneCard(RectTransform parent) {
        Image card = UIFactory.CreateImage("DoneCard", parent, new Color(0f, 0f, 0f, 0.04f));
        RectTransform rect = card.rectTransform;
        UIFactory.SetHeight(rect, S(340f));

        HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset((int)S(36f), (int)S(36f), (int)S(30f), (int)S(30f));
        layout.spacing = S(40f);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;

        // 회색 판 위에서도 제품이 또렷하도록 사진만 흰 바탕에 올린다
        Image shotPlate = UIFactory.CreateImage("ShotPlate", rect, Color.white);

        LayoutElement plateLayout = shotPlate.gameObject.AddComponent<LayoutElement>();
        plateLayout.minWidth = plateLayout.preferredWidth = S(260f);
        plateLayout.flexibleWidth = 0f;

        // 여백은 이 컨테이너가 주고, 비율 맞춤은 안쪽 RawImage 가 한다.
        // 판이 가로 260 x 세로 280(카드 340 - 위아래 여백 60)이라 정사각형이 아니다.
        // 예전엔 RawImage 를 판에 그대로 늘려서, 정사각형인 제품 사진이 세로로 눌려 보였다.
        RectTransform shotArea = UIFactory.CreateRect("ShotArea", shotPlate.rectTransform);
        UIFactory.Stretch(shotArea, S(10f));

        doneShot = UIFactory.CreateRawImage("Shot", shotArea, null);
        doneShot.raycastTarget = false;

        doneShotFitter = doneShot.gameObject.AddComponent<AspectRatioFitter>();
        doneShotFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        doneShotFitter.aspectRatio = 1f;

        RectTransform info = UIFactory.CreateRect("Info", rect);
        UIFactory.AddVerticalLayout(info, new RectOffset(0, 0, (int)S(24f), 0), 0f);
        info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        doneNameText  = UIFactory.CreateText("Name", info, "", S(NAME_SIZE * 0.8f), FontStyles.Normal, Ink);
        FormSpacer(info, "GapName", 26f);
        donePriceText = UIFactory.CreateText("Price", info, "", S(PRICE_SIZE), FontStyles.Normal, Ink);
        FormSpacer(info, "GapPrice", 22f);
        doneColorText = UIFactory.CreateText("Color", info, "", S(LABEL_SIZE), FontStyles.Normal, Ink);
        FormSpacer(info, "GapColor", 18f);
        doneSizeText  = UIFactory.CreateText("Size", info, "", S(LABEL_SIZE), FontStyles.Normal, Ink);
    }

    private void ShowPreregDone() {
        if (currentProduct == null || preregDone == null) return;

        doneNameText.text  = currentProduct.name;
        donePriceText.text = $"₩{currentProduct.price:N0}";
        doneColorText.text = $"색상 : {(currentColor != null ? currentColor.displayName : "-")}";
        doneSizeText.text  = $"Size : {ResolveSize(currentProduct)}";

        Texture shot = ProductShot.Get(
            ProductCatalog.IndexOf(currentProduct),
            currentColor != null ? currentColor.color : null);

        doneShot.texture = shot;

        // 렌더 텍스처는 정사각형이지만, 나중에 바뀌어도 눌리지 않도록 실제 크기에서 비율을 가져온다.
        if (doneShotFitter != null && shot != null && shot.height > 0) {
            doneShotFitter.aspectRatio = (float)shot.width / shot.height;
        }

        doneShot.gameObject.SetActive(shot != null);

        preregForm.SetActive(false);
        preregDone.SetActive(true);
    }

    private void Update() {
        HandlePreregTabNavigation();
    }

    /// 사전 예약 폼에서 Tab / Shift+Tab 으로 다음·이전 입력칸으로 넘어간다.
    ///
    /// 유니티의 EventSystem 은 방향키(Move)만 내비게이션으로 처리하고 Tab 은 안 본다.
    /// 그래서 인트로 씬의 LegacyTextTabNav 와 같은 방식으로 직접 처리한다.
    private void HandlePreregTabNavigation() {
        if (preregForm == null || !preregForm.activeInHierarchy) return;
        if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame) return;
        if (EventSystem.current == null) return;

        Selectable[] order = { nameField, phoneField, emailField, consentToggle, submitButton };

        GameObject current = EventSystem.current.currentSelectedGameObject;

        int index = -1;
        for (int i = 0; i < order.Length; i++) {
            if (order[i] != null && order[i].gameObject == current) {
                index = i;
                break;
            }
        }

        bool reverse = Keyboard.current.leftShiftKey.isPressed
                    || Keyboard.current.rightShiftKey.isPressed;

        int step = reverse ? -1 : 1;

        // 아무것도 선택돼 있지 않으면 정방향은 첫 칸, 역방향은 마지막 칸부터 시작한다
        int start = index >= 0 ? index : (reverse ? 0 : -1);

        // 비활성이거나 아직 누를 수 없는 항목(등록 버튼 등)은 건너뛴다
        for (int i = 1; i <= order.Length; i++) {
            int next = (((start + step * i) % order.Length) + order.Length) % order.Length;

            Selectable target = order[next];
            if (target == null) continue;
            if (!target.gameObject.activeInHierarchy || !target.interactable) continue;

            EventSystem.current.SetSelectedGameObject(target.gameObject);

            if (target is TMP_InputField field) field.ActivateInputField();

            return;
        }
    }

    private void FormSpacer(RectTransform parent, string name, float designHeight) {
        RectTransform spacer = UIFactory.CreateRect(name, parent);
        UIFactory.SetHeight(spacer, S(designHeight));
    }

    private void BuildToast() {
        Image toastBg = UIFactory.CreateImage("Toast", root.transform, new Color(0.08f, 0.08f, 0.08f, 0.92f));
        RectTransform rt = toastBg.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(560f, 64f);
        toastBg.sprite = RoundedSprite();
        toastBg.type = Image.Type.Sliced;

        toastText = UIFactory.CreateText("ToastText", rt, "", 20f, FontStyles.Normal, Color.white);
        toastText.alignment = TextAlignmentOptions.Center;
        UIFactory.Stretch(toastText.rectTransform);

        toastObject = toastBg.gameObject;
        toastObject.SetActive(false);
    }

    // ---------------------------------------------------------------- 이벤트 연결

    /// onClick 같은 런타임 리스너는 프리팹에 저장되지 않으므로 매 실행마다 다시 붙인다.
    private void WireEvents() {
        if (tryOnButton != null) {
            tryOnButton.onClick.RemoveAllListeners();
            tryOnButton.onClick.AddListener(HandleTryOnClicked);
        }
        if (preregButton != null) {
            preregButton.onClick.RemoveAllListeners();
            preregButton.onClick.AddListener(OpenPreregForm);
        }
        if (closeButton != null) {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(RequestClose);
        }
        if (detailHeaderButton != null) {
            detailHeaderButton.onClick.RemoveAllListeners();
            detailHeaderButton.onClick.AddListener(ToggleDetail);
        }
        if (submitButton != null) {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(SubmitPrereg);
        }
        if (cancelButton != null) {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(ClosePreregForm);
        }
        if (doneButton != null) {
            doneButton.onClick.RemoveAllListeners();
            doneButton.onClick.AddListener(ClosePreregDone);
        }
        if (fittingDoneButton != null) {
            fittingDoneButton.onClick.RemoveAllListeners();
            fittingDoneButton.onClick.AddListener(RequestFittingEnd);
        }

        if (nameField != null) {
            nameField.onValueChanged.RemoveAllListeners();
            nameField.onValueChanged.AddListener(_ => ValidateForm());
        }
        if (phoneField != null) {
            phoneField.onValueChanged.RemoveAllListeners();
            phoneField.onValueChanged.AddListener(_ => ValidateForm());
        }
        if (emailField != null) {
            emailField.onValueChanged.RemoveAllListeners();
            emailField.onValueChanged.AddListener(_ => ValidateForm());
        }
        if (consentToggle != null) {
            consentToggle.onValueChanged.RemoveAllListeners();
            consentToggle.onValueChanged.AddListener(_ => ValidateForm());
        }
    }

    // ---------------------------------------------------------------- 표시

    public void Show(ProductData product, Transform bagRoot,
                     Action<ProductColorOption> colorChangedCallback,
                     Action tryOnCallback,
                     Action closeCallback) {

        if (product == null) return;

        // 피팅 중에 다른 제품을 여는 경로가 생기면 키 선택이 남는다
        if (fittingMode) ExitFittingMode();

        currentProduct = product;
        currentBagRoot = bagRoot;
        onColorChanged = colorChangedCallback;
        onTryOnRequested = tryOnCallback;
        onCloseRequested = closeCallback;

        nameText.text = product.name;
        priceText.text = $"₩{product.price:N0}";
        sizeText.text = $"Size : {ResolveSize(product)}";

        detailText.text = product.heritage_text;

        BuildSwatches(product);

        // 시안 기본 상태는 접힘(∧).
        SetDetailExpanded(false);

        preregForm.SetActive(false);
        if (preregDone != null) preregDone.SetActive(false);
        root.SetActive(true);

        // 이전 제품에서 스크롤을 내려둔 채로 닫았으면 그 위치가 남는다
        if (panelScroll != null) panelScroll.verticalNormalizedPosition = 1f;
    }

    /// 시안은 사이즈를 하나만 보여준다. 목록의 첫 값을 대표로 쓴다.
    private static string ResolveSize(ProductData product) {
        return product.sizes != null && product.sizes.Count > 0 ? product.sizes[0] : "-";
    }

    public void Hide() {
        // 상세정보를 펼쳐둔 채로 닫으면(X, 걸어서 이탈) 그 구간이 유실된다
        FlushDetailView();

        if (root != null) root.SetActive(false);
        if (preregForm != null) preregForm.SetActive(false);
        if (preregDone != null) preregDone.SetActive(false);

        // 피팅 중에 패널이 닫히면 다음에 열었을 때 키 선택이 남아 있게 된다
        if (fittingMode) ExitFittingMode();

        currentProduct = null;
        currentBagRoot = null;
        onColorChanged = null;
        onTryOnRequested = null;
        onCloseRequested = null;
    }

    // ---------------------------------------------------------------- 피팅 모드

    /// 상세 보기 → 피팅. 패널 위쪽은 두고 상세정보·하단 버튼 자리를 키 선택 + [피팅 끝내기]로 바꾼다.
    /// 컬러는 상세 보기에서 고른 것으로 고정된다(스와치는 보이지만 눌리지 않는다).
    public void EnterFittingMode(int height, Action<int> heightChangedCallback, Action endCallback) {
        if (fittingSection == null) return;

        fittingMode = true;
        currentHeight = height;
        onHeightChanged = heightChangedCallback;
        onFittingEndRequested = endCallback;

        // 피팅 시안에는 상세정보가 없어 헤더째로 숨긴다
        SetDetailExpanded(false);
        if (detailHeaderButton != null) detailHeaderButton.gameObject.SetActive(false);
        if (gapDetail != null) gapDetail.gameObject.SetActive(false);
        if (buttonRow != null) buttonRow.gameObject.SetActive(false);

        BuildHeightChips();
        UpdateHeightUI();

        fittingSection.SetActive(true);
        SetSwatchesInteractable(false);

        // 피팅 중엔 [피팅 끝내기]로만 나갈 수 있다. X로 닫으면 시착이 어중간하게 끊긴다.
        if (closeButton != null) closeButton.gameObject.SetActive(false);

        if (root != null) root.SetActive(true);
        if (panelScroll != null) panelScroll.verticalNormalizedPosition = 1f;
    }

    public void ExitFittingMode() {
        fittingMode = false;
        onHeightChanged = null;
        onFittingEndRequested = null;

        if (fittingSection != null) fittingSection.SetActive(false);
        if (detailHeaderButton != null) detailHeaderButton.gameObject.SetActive(true);
        if (gapDetail != null) gapDetail.gameObject.SetActive(true);
        if (buttonRow != null) buttonRow.gameObject.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(true);

        SetDetailExpanded(false);
        SetSwatchesInteractable(true);

        if (panelScroll != null) panelScroll.verticalNormalizedPosition = 1f;
    }

    private void SelectHeight(int height) {
        if (!fittingMode || currentHeight == height) return;

        currentHeight = height;
        UpdateHeightUI();

        onHeightChanged?.Invoke(height);
    }

    private void UpdateHeightUI() {
        if (heightText != null) heightText.text = $"Height : {currentHeight}cm";

        foreach (HeightChip chip in heightChips) {
            ApplyHeightChipState(chip, chip.height == currentHeight);
        }
    }
    
    /// 피팅 중에는 컬러 고정. transition이 None이라 비활성이어도 회색으로 변하지 않는다.
    private void SetSwatchesInteractable(bool value)
    {
        if (swatchRow == null) return;

        swatchesLocked = !value;

        foreach (Button button in swatchRow.GetComponentsInChildren<Button>(true))
        {
            button.interactable = value;
        }

        foreach (Swatch swatch in swatches)
        {
            bool selected = currentColor != null &&
                            swatch.code == currentColor.color;

            ApplySwatchState(swatch, selected);
        }
    }

    private void RequestFittingEnd() {
        onFittingEndRequested?.Invoke();
    }

    // ---------------------------------------------------------------- 상세정보 접기

    private void ToggleDetail() {
        SetDetailExpanded(detailBody == null || !detailBody.activeSelf);
    }

    private void SetDetailExpanded(bool expanded) {
        bool wasExpanded = detailBody != null && detailBody.activeSelf;

        if (detailBody != null) detailBody.SetActive(expanded);

        // 시안은 접힘 ∧ / 펼침 ∨. 꺾쇠가 ∧로 만들어져 있어 펼쳤을 때 뒤집는다.
        if (detailChevron != null) {
            detailChevron.localRotation = Quaternion.Euler(0f, 0f, expanded ? 180f : 0f);
        }

        // 펼쳐 있던 시간을 재서, 접히는 순간(또는 팝업이 닫히는 순간) detail_view로 발행한다.
        if (expanded && !wasExpanded) {
            detailExpandStartTime = Time.time;
        } else if (!expanded && wasExpanded) {
            FlushDetailView();
        }
    }

    /// 명세서 detail_view (meta: {section, duration_sec}). 현재는 헤리티지 한 섹션뿐이다.
    private void FlushDetailView() {
        if (detailExpandStartTime < 0f) return;

        float duration = Time.time - detailExpandStartTime;
        detailExpandStartTime = -1f;

        if (SessionDataManager.Instance != null) {
            SessionDataManager.Instance.LogDetailView(
                currentProduct != null ? currentProduct.product_id : 0,
                "heritage", duration);
        }

        Debug.Log($"[BagViewUI] detail_view - {currentProduct?.product_id}, duration: {duration:F1}s");
    }

    // ---------------------------------------------------------------- 컬러 스와치

    private void BuildSwatches(ProductData product) {
        // 프리팹에 구워진 칩이 남아있을 수 있으므로 자식을 전부 비우고 다시 만든다.
        for (int i = swatchRow.childCount - 1; i >= 0; i--) {
            Destroy(swatchRow.GetChild(i).gameObject);
        }
        swatches.Clear();
        currentColor = null;

        // 컬러가 하나뿐이어도 칩을 보여준다. 숨기면 제품마다 패널 높이가 달라진다.
        foreach (ProductColorOption option in product.colors) {
            if (option == null) continue;
            swatches.Add(CreateSwatch(option));
        }

        // 무조건 첫 컬러를 고르면 재진입할 때 1번 색으로 되돌아간다. 지금 입고 있는 색을 되살린다.
        ProductColorOption current = BagColorState.ResolveOrAssignRandom(product);
        if (current != null) SelectColor(current, notify: false);
    }

    /// 마름모 칩 하나. 사각형 이미지를 45도 돌려서 만든다.
    private Swatch CreateSwatch(ProductColorOption option) {
        float box = S(SWATCH_BOX);

        // 회전은 자식에게만 건다. 컨테이너가 돌아가면 가로 레이아웃 정렬이 어긋난다.
        RectTransform container = UIFactory.CreateRect($"Swatch_{option.color}", swatchRow);
        container.sizeDelta = new Vector2(box, box);

        Image hit = container.gameObject.AddComponent<Image>();
        hit.color = Color.clear;   // 클릭 판정용. 마름모 바깥 모서리까지 눌리지만 칩이 작아 그편이 낫다

        Color color = UIFactory.ParseHex(option.hex, Color.gray);

        var swatch = new Swatch {
            code = option.color,
            color = color,
            // 흰색 계열은 흰 배경에 묻히므로 선택 여부와 무관하게 테두리가 필요하다
            light = color.r + color.g + color.b > 2.4f,
            ring = CreateDiamond("Ring", container, box, Ink),
            gap = CreateDiamond("Gap", container, box, PanelBg),
            fill = CreateDiamond("Fill", container, box, color),
        };

        if (option.model_supported) {
            Button button = container.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            ProductColorOption captured = option;
            button.onClick.AddListener(() => SelectColor(captured));
        } else {
            // 3D 모델이 지원하지 않는 컬러는 비활성 칩으로 (명세서: 회색 + "준비 중")
            Color faded = swatch.fill.color;
            faded.a = 0.25f;
            swatch.fill.color = faded;
        }

        // 선택 색이 정해지기 전에도 칩이 새까맣게 보이지 않도록 비선택 모양을 먼저 잡는다.
        ApplySwatchState(swatch, false);

        return swatch;
    }

    private Image CreateDiamond(string name, RectTransform parent, float size, Color color) {
        Image image = UIFactory.CreateImage(name, parent, color);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        // 45도 돌리면 대각선이 변 × 1.414다. 마름모 폭을 size로 맞추려고 변을 그만큼 줄인다.
        rect.sizeDelta = new Vector2(size / 1.4142f, size / 1.4142f);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

        return image;
    }

    /// 선택은 이중 마름모(테두리 → 흰 여백 → 컬러), 비선택은 꽉 찬 마름모(흰색 계열만 테두리 유지).
    private void ApplySwatchState(Swatch swatch, bool selected) {
        float box = S(SWATCH_BOX);
        float side = box / 1.4142f;

        bool showRing = selected || swatch.light;

        swatch.ring.color = showRing ? Ink : Color.clear;
        swatch.ring.rectTransform.sizeDelta = new Vector2(side, side);

        float gapScale = selected ? 0.86f : (swatch.light ? 0.90f : 1f);
        swatch.gap.color = showRing ? PanelBg : Color.clear;
        swatch.gap.rectTransform.sizeDelta = new Vector2(side * gapScale, side * gapScale);

        float fillScale = selected ? 0.62f : (swatch.light ? 0.86f : 1f);
        swatch.fill.rectTransform.sizeDelta = new Vector2(side * fillScale, side * fillScale);

        // 시착 중에는 현재 선택된 색을 제외한 색상을 흐리게 표시
        Color fillColor = swatch.fill.color;
        fillColor.a = swatchesLocked && !selected ? 0.4f : 1f;
        swatch.fill.color = fillColor;
    }

    private void SelectColor(ProductColorOption option) {
        SelectColor(option, notify: true);
    }

    private void SelectColor(ProductColorOption option, bool notify) {
        if (option == null || currentProduct == null) return;
        if (currentColor != null && currentColor.color == option.color) return;

        currentColor = option;

        // 색이 같은데도 가방을 다시 만들면 Highlighter가 캐싱한 참조가 죽어 그 스툴이 안 눌린다.
        bool modelNeedsSwap =
            BagColorState.GetColor(currentProduct.product_id) != option.color;

        // 이 가방의 색으로 확정 기록. 시착·재진입에서 여기서 다시 읽어간다.
        BagColorState.SetColor(currentProduct.product_id, option.color);

        // 컬러마다 FBX가 달라 머티리얼 교체로는 안 바뀐다. 위에 기록한 색으로 다시 만든다.
        Transform swapped = modelNeedsSwap && currentBagRoot != null && GalleryController.Instance != null
            ? GalleryController.Instance.SwapBagColor(currentBagRoot)
            : null;

        if (swapped != null) {
            currentBagRoot = swapped;

            // 카메라가 사라진 예전 오브젝트를 붙들면 드래그 회전과 자유 이동 복귀가 멈춘다.
            CameraController cameraController = Camera.main != null
                ? Camera.main.GetComponent<CameraController>()
                : null;

            if (cameraController != null) cameraController.NotifyBagReplaced(swapped);

        } else if (modelNeedsSwap && currentBagRoot != null) {
            // 라이브러리가 없을 때(자리표시자 큐브)는 색만 바꾼다
            Renderer renderer = currentBagRoot.GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.material.color = UIFactory.ParseHex(option.hex, Color.gray);
        }

        colorNameText.text = $"색상 : {option.displayName}";

        foreach (Swatch swatch in swatches) {
            ApplySwatchState(swatch, swatch.code == option.color);
        }

        if (notify) onColorChanged?.Invoke(option);
    }

    // ---------------------------------------------------------------- 버튼

    private void HandleTryOnClicked() {
        Debug.Log($"[BagViewUI] tryon_start - {currentProduct?.product_id}, color: {currentColor?.color}");

        if (onTryOnRequested == null) {
            ShowToast("시착 기능은 준비 중입니다");
            return;
        }

        onTryOnRequested.Invoke();
    }

    private void OpenPreregForm() {
        FillPreregProduct();

        preregForm.SetActive(true);
        ValidateForm();

        // 이전에 열었을 때 내려둔 스크롤이 남아 있으면 제품 카드가 안 보인다
        if (preregScroll != null) preregScroll.verticalNormalizedPosition = 1f;

        if (SessionDataManager.Instance != null) {
            SessionDataManager.Instance.LogPreregFormOpen(currentProduct != null ? currentProduct.product_id : 0);
        }

        Debug.Log($"[BagViewUI] prereg_form_open - {currentProduct?.product_id}");
    }

    private void FillPreregProduct() {
        if (currentProduct == null) return;

        preregNameText.text  = currentProduct.name;
        preregPriceText.text = $"₩{currentProduct.price:N0}";
        preregColorText.text = $"색상 : {(currentColor != null ? currentColor.displayName : "-")}";
        preregSizeText.text  = $"Size : {ResolveSize(currentProduct)}";

        // 2D 제품 이미지는 따로 없다. 지금 고른 컬러의 3D 모델을 그 자리에서 찍어 쓴다.
        Texture shot = ProductShot.Get(
            ProductCatalog.IndexOf(currentProduct),
            currentColor != null ? currentColor.color : null);

        preregShot.texture = shot;
        preregShot.gameObject.SetActive(shot != null);
    }

    private void ClosePreregForm() {
        preregForm.SetActive(false);

        if (SessionDataManager.Instance != null) {
            SessionDataManager.Instance.LogPreregDismiss("form");
        }

        Debug.Log("[BagViewUI] prereg_dismiss - stage: form");
    }

    private void ClosePreregDone() {
        if (preregDone != null) preregDone.SetActive(false);
    }

    /// 같은 사유를 글자 입력마다 반복해서 찍지 않으려고 마지막 값을 들고 있는다.
    private string lastValidationReason;

    private void ValidateForm() {
        if (submitButton == null) return;

        string name  = Trimmed(nameField);
        string phone = Trimmed(phoneField);
        string email = Trimmed(emailField);

        bool nameOk  = name.Length >= 2 && name.Length <= 20;
        bool phoneOk = phone.Length >= 10 && phone.Length <= 11;

        // 진짜 검증은 서버가 하므로 여기선 오타를 거르는 정도로 충분하다.
        int at = email.IndexOf('@');
        bool emailOk = at > 0 && email.IndexOf('.', at) > at + 1 && !email.EndsWith(".");

        // consentToggle이 아직 없으면 동의를 받은 적이 없는 것으로 본다
        bool consentOk = consentToggle != null && consentToggle.isOn;

        bool valid = nameOk && phoneOk && emailOk && consentOk;
        submitButton.interactable = valid;

        // 어느 칸 때문에 막혀 있는지 눈에 보이지 않으면 원인을 찾기 어렵다
        string reason = valid
            ? "ok"
            : $"name={nameOk} phone={phoneOk} email={emailOk} consent={consentOk}";

        if (reason != lastValidationReason) {
            lastValidationReason = reason;
            Debug.Log($"[BagViewUI] prereg 입력 검증 - {reason}");
        }
    }

    private static string Trimmed(TMP_InputField field) {
        return field != null && field.text != null ? field.text.Trim() : string.Empty;
    }
    private void SubmitPrereg()
    {
        // 개인정보는 이벤트 버퍼에 남기지 않고 제품 정보만 기록한다.
        if (SessionDataManager.Instance != null)
        {
            SessionDataManager.Instance.LogPreregSubmit(
                currentProduct != null
                    ? currentProduct.product_id
                    : 0,
                currentColor != null
                    ? currentColor.color
                    : null,
                currentProduct != null &&
                currentProduct.sizes.Count > 0
                    ? currentProduct.sizes[0]
                    : null
            );
        }

        string name =
            nameField.text != null
                ? nameField.text.Trim()
                : string.Empty;

        string phone =
            phoneField.text != null
                ? phoneField.text.Trim()
                : string.Empty;

        // 이메일·컬러·사이즈는 명세서 §3.4 요청 본문에 없는 필드라 서버로 보내지 않는다.
        // (컬러·사이즈는 위 prereg_submit 이벤트에 이미 실렸다)

        PreRegAPIClient apiClient =
            FindFirstObjectByType<PreRegAPIClient>();

        if (apiClient == null)
        {
            Debug.LogError(
                "[BagViewUI] PreRegAPIClient를 찾을 수 없습니다."
            );

            ShowToast("등록에 실패했습니다. 다시 시도해주세요");
            return;
        }

        // 성공(201)·중복(409)이면 입력값을 비우고 폼을 닫고, 네트워크 실패면 둘 다 유지한다.
        int productId =
            currentProduct != null
                ? currentProduct.product_id
                : 0;

        apiClient.Submit(
            productId,
            name,
            phone,
            consentToggle.isOn,
            (success, duplicated) =>
            {
                if (!success)
                {
                    ShowToast(
                        "등록에 실패했습니다. 다시 시도해주세요"
                    );

                    return;
                }

                // 다시 열었을 때 남의 입력이 남아 있으면 안 된다
                nameField.text = string.Empty;
                phoneField.text = string.Empty;
                if (emailField != null) emailField.text = string.Empty;
                consentToggle.isOn = false;

                // 중복(409)은 명세서상 에러가 아니라 정상 닫힘이다. 안내만 하고 폼을 닫는다.
                if (duplicated)
                {
                    ShowToast(
                        "이미 등록된 제품입니다"
                    );

                    preregForm.SetActive(false);
                    return;
                }

                // ShowPreregDone이 폼을 닫고 완료 화면을 켜는 것까지 처리한다.
                ShowPreregDone();
            }
        );
    }

    public void RequestClose() {
        onCloseRequested?.Invoke();
    }

    // ---------------------------------------------------------------- 토스트

    private void ShowToast(string message) {
        if (toastObject == null || toastText == null) return;

        toastText.text = message;
        toastObject.SetActive(true);

        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToastAfter(2f));
    }

    private IEnumerator HideToastAfter(float seconds) {
        yield return new WaitForSeconds(seconds);
        toastObject.SetActive(false);
        toastRoutine = null;
    }
}

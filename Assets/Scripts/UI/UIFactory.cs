using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// 런타임에 UI를 생성할 때 쓰는 보일러플레이트 모음 (씬 파일 병합 충돌 방지).
public static class UIFactory {

    /// 한글 폰트 에셋. BagViewUI가 시작할 때 한 번 채워준다.
    public static TMP_FontAsset Font { get; set; }

    public static RectTransform CreateRect(string name, Transform parent) {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    /// 부모를 꽉 채우도록 앵커를 늘린다.
    public static RectTransform Stretch(RectTransform rt, float padding = 0f) {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
        return rt;
    }

    public static Image CreateImage(string name, Transform parent, Color color) {
        RectTransform rt = CreateRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    /// 단색이 아닌 재질(스와치 칩 등)을 텍스처로 띄울 때 쓴다.
    public static RawImage CreateRawImage(string name, Transform parent, Texture texture) {
        RectTransform rt = CreateRect(name, parent);
        RawImage image = rt.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        return image;
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, string content,
                                             float fontSize, FontStyles style, Color color) {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();

        if (Font != null) text.font = Font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;

        return text;
    }

    public static Button CreateButton(string name, Transform parent, string label,
                                      Color background, Color foreground, UnityAction onClick) {
        RectTransform rt = CreateRect(name, parent);

        Image image = rt.gameObject.AddComponent<Image>();
        image.color = background;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Label", rt, label, 22f, FontStyles.Bold, foreground);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);

        if (onClick != null) button.onClick.AddListener(onClick);

        return button;
    }

    public static Toggle CreateToggle(string name, Transform parent, string label) {
        RectTransform rt = CreateRect(name, parent);
        Toggle toggle = rt.gameObject.AddComponent<Toggle>();

        Image box = CreateImage("Background", rt, new Color(1f, 1f, 1f, 0.15f));
        box.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        box.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        box.rectTransform.pivot = new Vector2(0f, 0.5f);
        box.rectTransform.sizeDelta = new Vector2(24f, 24f);
        box.rectTransform.anchoredPosition = Vector2.zero;

        Image check = CreateImage("Checkmark", box.rectTransform, Color.white);
        Stretch(check.rectTransform, 5f);
        check.raycastTarget = false;

        TextMeshProUGUI text = CreateText("Label", rt, label, 16f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.75f));
        text.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(34f, 0f);

        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.isOn = false;

        return toggle;
    }

    public static TMP_InputField CreateInputField(string name, Transform parent, string placeholderText,
                                                  TMP_InputField.ContentType contentType, int characterLimit) {
        RectTransform rt = CreateRect(name, parent);

        // TMP_InputField가 OnEnable에서 textComponent를 찾으므로 자식 구성 후에 켠다.
        rt.gameObject.SetActive(false);

        Image background = rt.gameObject.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.08f);

        RectTransform viewport = CreateRect("Text Area", rt);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(12f, 6f);
        viewport.offsetMax = new Vector2(-12f, -6f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = CreateText("Placeholder", viewport, placeholderText, 18f,
                                                 FontStyles.Normal, new Color(1f, 1f, 1f, 0.35f));
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(placeholder.rectTransform);

        TextMeshProUGUI text = CreateText("Text", viewport, string.Empty, 18f, FontStyles.Normal, Color.white);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(text.rectTransform);

        TMP_InputField field = rt.gameObject.AddComponent<TMP_InputField>();
        field.targetGraphic = background;
        field.textViewport = viewport;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.contentType = contentType;
        field.characterLimit = characterLimit;
        field.lineType = TMP_InputField.LineType.SingleLine;
        if (Font != null) field.fontAsset = Font;

        rt.gameObject.SetActive(true);

        return field;
    }

    /// LayoutGroup 안에서 높이를 고정하고 싶을 때
    public static LayoutElement SetHeight(Component target, float height) {
        LayoutElement element = target.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        return element;
    }

    public static VerticalLayoutGroup AddVerticalLayout(Transform target, RectOffset padding, float spacing) {
        VerticalLayoutGroup group = target.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = padding;
        group.spacing = spacing;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        group.childAlignment = TextAnchor.UpperLeft;
        return group;
    }

    public static HorizontalLayoutGroup AddHorizontalLayout(Transform target, float spacing) {
        HorizontalLayoutGroup group = target.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = spacing;
        group.childControlWidth = false;
        group.childControlHeight = false;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.childAlignment = TextAnchor.MiddleLeft;
        return group;
    }

    public static Color ParseHex(string hex, Color fallback) {
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed)) {
            return parsed;
        }
        return fallback;
    }
}

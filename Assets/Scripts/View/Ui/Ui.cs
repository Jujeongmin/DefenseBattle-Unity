using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 로비·페이지를 코드로 그리는 도우미 (원본 menu.ts 가 innerHTML 로 다시 그리던 것과 같은 방식).
    /// 열 때·상태가 바뀔 때만 만들고 매 프레임 건드리지 않는다. 크기 단위 = 기준 해상도(720) = CSS px × 2.
    /// </summary>
    public static class Ui
    {
        public static SpriteCatalog Cat { get; private set; }

        public static readonly Color32 Cream = new Color32(0xff, 0xf4, 0xdc, 255);
        public static readonly Color32 Cream2 = new Color32(0xe3, 0xcf, 0xa6, 255);
        public static readonly Color32 Ink = new Color32(0x3b, 0x24, 0x16, 255);
        public static readonly Color32 Ink2 = new Color32(0x7d, 0x5d, 0x42, 255);
        public static readonly Color32 Edge = new Color32(0x2a, 0x1d, 0x16, 255);
        public static readonly Color32 Gold = new Color32(0xff, 0xc9, 0x28, 255);
        public static readonly Color32 Gem = new Color32(0x4f, 0xd6, 0xe6, 255);
        public static readonly Color32 Red = new Color32(0xe5, 0x48, 0x4d, 255);
        public static readonly Color32 Green = new Color32(0x4c, 0xaf, 0x50, 255);
        public static readonly Color32 Blue = new Color32(0x3f, 0x8f, 0xb5, 255);
        public static readonly Color32 Face = new Color32(0xf1, 0xe2, 0xc2, 255);
        public static readonly Color32 Dim = new Color32(0, 0, 0, 215); // 선형 색 공간이라 원본(0.6)보다 진하게

        public static void Init(SpriteCatalog catalog) => Cat = catalog;

        // ---------------- 기본 ----------------
        public static RectTransform Rect(Transform parent, string name = "Rect")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(Transform parent, Sprite sprite, Color color, string name = "Image")
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>9-slice 테두리 그림 (UI/Frame 이름). ppuMul 이 클수록 테두리가 얇다</summary>
        public static Image Frame(Transform parent, string frame, float ppuMul = 1.6f, string name = null)
        {
            Image img = Image(parent, Cat.Frame(frame), Color.white, name ?? frame);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppuMul;
            return img;
        }

        public static Image Icon(Transform parent, string icon, float size, Color? tint = null)
        {
            Image img = Image(parent, Cat.Icon(icon) ?? Cat.Frame(icon), tint ?? Color.white, icon);
            img.preserveAspect = true;
            Size(img.gameObject, size, size);
            return img;
        }

        /// <summary>글자 (기본: 크림색 + 얇은 검은 테두리 SDF). ink = 종이 위 진한 글자(테두리 없음)</summary>
        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool ink = false)
        {
            RectTransform rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (ink)
            {
                t.font = Cat.BoldFont;
                t.fontSharedMaterial = Cat.BoldFont.material;
            }
            else
            {
                t.font = Cat.NumberFont;
                if (Cat.HudTextMaterial != null) t.fontSharedMaterial = Cat.HudTextMaterial;
            }
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            t.text = text ?? "";
            return t;
        }

        /// <summary>한 줄 글자: 칸보다 길면 글자 크기를 줄인다 (dom.ts fitText)</summary>
        public static TextMeshProUGUI Line(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool ink = false)
        {
            TextMeshProUGUI t = Text(parent, text, size, color, align, ink);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = Mathf.Max(10, size * 0.5f);
            t.fontSizeMax = size;
            // 레이아웃 안에서 자동 크기 글자는 선호 높이가 0 이 돼 줄어든다 - 한 줄 높이를 정해 둔다
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = Mathf.Ceil(size * 1.3f);
            return t;
        }

        /// <summary>테두리 그림 버튼 (btn_red / btn_gold / btn_blue / btn_green / btn_gray …). 누르면 _down 그림</summary>
        public static Button Button(Transform parent, string frame, string label, float fontSize, Action onClick, float ppuMul = 1.7f)
        {
            Image img = Frame(parent, frame, ppuMul, "Button");
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { pressedSprite = Cat.Frame(frame + "_down"), disabledSprite = Cat.Frame("btn_gray") };
            if (onClick != null) b.onClick.AddListener(() => { Sounds?.Invoke(); onClick(); });
            if (label != null)
            {
                TextMeshProUGUI t = Line(b.transform, label, fontSize, Cream);
                Fill(t.rectTransform, 14, 12, 14, 6);
            }
            return b;
        }

        /// <summary>버튼을 누를 때 울릴 소리 (BattleFlow 가 버튼 소리로 연결)</summary>
        public static Action Sounds;

        /// <summary>글자만 있는 투명 버튼 (탭 영역)</summary>
        public static Button Hit(Transform parent, Action onClick, string name = "Hit")
        {
            Image img = Image(parent, null, new Color(0, 0, 0, 0), name);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(() => { Sounds?.Invoke(); onClick(); });
            return b;
        }

        // ---------------- 배치 ----------------
        public static VerticalLayoutGroup Column(Transform t, float spacing = 12, int padL = 0, int padR = 0, int padT = 0, int padB = 0, TextAnchor align = TextAnchor.UpperCenter)
        {
            var g = t.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padL, padR, padT, padB);
            g.childAlignment = align;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup Row(Transform t, float spacing = 12, int padL = 0, int padR = 0, int padT = 0, int padB = 0, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var g = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padL, padR, padT, padB);
            g.childAlignment = align;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            return g;
        }

        /// <summary>레이아웃 그룹 안 크기 (-1 = 그 값은 건드리지 않음)</summary>
        public static LayoutElement Size(GameObject go, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            LayoutElement le = go.TryGetComponent(out LayoutElement found) ? found : go.AddComponent<LayoutElement>();
            if (w >= 0)
            {
                le.preferredWidth = w;
                le.minWidth = Mathf.Min(w, 40);
            }
            if (h >= 0) le.preferredHeight = le.minHeight = h;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        /// <summary>세로 스크롤 영역 (content 에 Column 을 붙이고 내용만큼 늘어난다)</summary>
        public static RectTransform Scroll(Transform parent, out ScrollRect scroll)
        {
            RectTransform view = Rect(parent, "Scroll");
            var mask = view.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            Image hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            RectTransform content = Rect(view, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            return content;
        }

        public static void Fill(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>앵커 한 점 기준 위치·크기 (가운데 피벗)</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>작은 빨간 점 (받을 것 있음)</summary>
        public static void Dot(Transform parent, float size = 18)
        {
            Image d = Image(parent, null, Red, "Dot");
            var le = d.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            Place(d.rectTransform, new Vector2(1, 1), new Vector2(-4, -4), new Vector2(size, size));
        }

        /// <summary>NEW 표</summary>
        public static void NewTag(Transform parent)
        {
            Image tag = Image(parent, null, Red, "New");
            var le = tag.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            Place(tag.rectTransform, new Vector2(1, 1), new Vector2(-8, -4), new Vector2(56, 26));
            Fill(Line(tag.transform, "NEW", 18, Cream).rectTransform);
        }

        /// <summary>한 줄 글 입력칸 (닉네임 등)</summary>
        public static TMP_InputField Input(Transform parent, string value, int maxLength, float fontSize = 28)
        {
            Image bg = Image(parent, null, new Color32(0xff, 0xfb, 0xf0, 255), "Input");
            bg.raycastTarget = true;
            RectTransform area = Rect(bg.transform, "Area");
            Fill(area, 14, 6, 14, 6);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI text = Text(area, "", fontSize, Ink, TextAlignmentOptions.Left, true);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Fill(text.rectTransform);
            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.characterLimit = maxLength;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.text = value ?? "";
            return field;
        }

        /// <summary>가로 막대 슬라이더 0~1 (음량)</summary>
        public static Slider Slider(Transform parent, float value, Action<float> changed)
        {
            RectTransform root = Rect(parent, "Slider");
            Image track = Image(root, null, new Color32(0, 0, 0, 110), "Track");
            Fill(track.rectTransform, 0, 14, 0, 14);
            RectTransform fillArea = Rect(root, "FillArea");
            Fill(fillArea, 0, 14, 0, 14);
            Image fill = Image(fillArea, null, Gold, "Fill");
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = Vector2.zero;
            RectTransform handleArea = Rect(root, "HandleArea");
            Fill(handleArea, 16, 0, 16, 0);
            Image handle = Image(handleArea, null, Cream, "Handle");
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(32, 0);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0;
            s.maxValue = 1;
            s.value = value;
            if (changed != null) s.onValueChanged.AddListener(v => changed(v));
            return s;
        }

        /// <summary>천 단위 쉼표 (toLocaleString)</summary>
        public static string Num(long v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        public static Color32 Hex(uint rgb) => Rgba.Hex(rgb);
    }
}

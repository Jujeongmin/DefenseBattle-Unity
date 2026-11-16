using DefenseBattle.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD 계층 만들기 (BattleSceneBuilder 가 부른다). 원본 index.html #game-frame + style.css, CSS px × 2 = 기준 해상도(720) 단위.
/// 자주 바뀌는 묶음(글자·진행 바·덱 카드)은 중첩 Canvas 로 따로 다시 그려지게 한다 (정적 UI 와 분리).
/// </summary>
public static class BattleHudBuilder
{
    const string Frame = "Assets/Art/DefenseBattle/UI/Frame/";
    const string HudTextMat = "Assets/Art/DefenseBattle/Generated/HudText.mat";

    static readonly Color32 Cream = new Color32(0xff, 0xf4, 0xdc, 255);
    static readonly Color32 Cream2 = new Color32(0xe3, 0xcf, 0xa6, 255);
    static readonly Color32 Ink = new Color32(0x3b, 0x24, 0x16, 255);
    static readonly Color32 Edge = new Color32(0x2a, 0x1d, 0x16, 255);
    static readonly Color32 GoldC = new Color32(0xff, 0xc9, 0x28, 255);

    static SpriteCatalog s_catalog;
    static TMP_FontAsset s_font;
    static Material s_fontMat;
    static TMP_FontAsset s_inkFont;

    public static BattleHud Build(RectTransform safe, BattleRunner runner, SpriteCatalog catalog)
    {
        s_catalog = catalog;
        s_font = catalog.NumberFont;
        s_fontMat = HudMaterial(catalog.NumberFont);
        s_inkFont = catalog.BoldFont;
        var hud = safe.gameObject.AddComponent<BattleHud>();
        BattleSceneBuilder.Set(hud, "m_runner", runner);

        BuildTopBar(hud, (RectTransform)safe.Find("TopBar"));
        // 위·아래 UI 사이 띠 안의 정사각 칸 = 전장 (#battlefield aspect-ratio 1). 전장 위 표시와 카메라는 이 칸에 맞춘다
        RectTransform board = BattleSceneBuilder.Rt(safe.Find("BoardArea"), "Board");
        board.Fill();
        var fitter = board.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;
        BattleSceneBuilder.Set(runner, "m_boardArea", board);
        BuildBoardOverlay(hud, board);
        BuildProgress(hud, (RectTransform)safe.Find("ProgressRow"));
        BuildControls(hud, (RectTransform)safe.Find("Controls"));
        return hud;
    }

    // ---------------- 상단바 ----------------
    static void BuildTopBar(BattleHud hud, RectTransform bar)
    {
        Img(bar, "Bg", FrameSprite("wood"), Color.white, 2.67f, true).rectTransform.Fill();

        // #topbar: 왼쪽(flex 1) · 가운데(웨이브) · 오른쪽(flex 1) - 폭이 좁아지면 양쪽이 줄고 웨이브 글자는 칸에 맞춰 작아진다
        RectTransform content = BattleSceneBuilder.Rt(bar, "Content");
        content.Fill(20, 12, 20, 10);
        var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        RectTransform left = BattleSceneBuilder.Rt(content, "Left");
        Layout(left.gameObject, minWidth: 150, preferredWidth: 220, flexibleWidth: 1);
        Image portraitBg = Img(left, "PortraitBg", null, new Color32(42, 29, 22, 140));
        portraitBg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(76, 76));
        Image portrait = Img(portraitBg.transform, "Portrait", null, Color.white);
        portrait.rectTransform.Fill(4);
        portrait.preserveAspect = true;
        RectTransform leftDyn = Nested(left, "Dynamic", false);
        leftDyn.Fill(86, 0, 0, 0); // 초상화 오른쪽부터
        TextMeshProUGUI race = Txt(leftDyn, "Race", 26, Cream, TextAlignmentOptions.BottomLeft);
        race.rectTransform.Fill(0, 40, 0, 0);
        race.enableAutoSizing = true;
        race.fontSizeMin = 16;
        race.fontSizeMax = 26;
        TextMeshProUGUI stage = Txt(leftDyn, "Stage", 22, Cream2, TextAlignmentOptions.TopLeft);
        stage.rectTransform.Fill(0, 0, 0, 44);

        RectTransform center = Nested(content, "Center", false);
        Layout(center.gameObject, minWidth: 120, preferredWidth: 200);
        TextMeshProUGUI wave = Txt(center, "Wave", 34, Cream, TextAlignmentOptions.Center);
        wave.rectTransform.Fill(0, 30, 0, 0);
        wave.enableAutoSizing = true;
        wave.fontSizeMin = 18;
        wave.fontSizeMax = 34;
        TextMeshProUGUI boss = Txt(center, "BossTimer", 26, new Color32(0xff, 0x7b, 0x7b, 255), TextAlignmentOptions.Top);
        boss.rectTransform.Fill(0, 0, 0, 52);

        RectTransform right = BattleSceneBuilder.Rt(content, "Right");
        Layout(right.gameObject, minWidth: 3 * 72 + 2 * 6, preferredWidth: 220, flexibleWidth: 1);
        var buttons = right.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttons.spacing = 6;
        buttons.childAlignment = TextAnchor.MiddleRight;
        buttons.childControlWidth = buttons.childControlHeight = true;
        buttons.childForceExpandWidth = buttons.childForceExpandHeight = false;
        Button mute = IconButton(right, "Mute", "sound", out Image muteIcon, out _);
        Button pause = IconButton(right, "Pause", "pause", out Image pauseIcon, out _);
        Button speed = IconButton(right, "Speed", null, out Image _, out TextMeshProUGUI speedText);
        foreach (Button b in new[] { mute, pause, speed }) Layout(b.gameObject, preferredWidth: 72, preferredHeight: 72);

        Set(hud, "m_portrait", portrait);
        Set(hud, "m_race", race);
        Set(hud, "m_stage", stage);
        Set(hud, "m_wave", wave);
        Set(hud, "m_bossTimer", boss);
        Set(hud, "m_speedButton", speed);
        Set(hud, "m_speedText", speedText);
        Set(hud, "m_pauseButton", pause);
        Set(hud, "m_pauseIcon", pauseIcon);
        Set(hud, "m_muteButton", mute);
        Set(hud, "m_muteIcon", muteIcon);
    }

    // ---------------- 전장 위 (소환 로그·골드·신화 배너) ----------------
    static void BuildBoardOverlay(BattleHud hud, RectTransform area)
    {
        RectTransform dyn = Nested(area, "Dynamic", false);
        TextMeshProUGUI log = Txt(dyn, "SummonLog", 22, Cream, TextAlignmentOptions.BottomLeft);
        log.rectTransform.anchorMin = log.rectTransform.anchorMax = log.rectTransform.pivot = Vector2.zero;
        log.rectTransform.anchoredPosition = new Vector2(12, 88);
        log.rectTransform.sizeDelta = new Vector2(460, 200);
        log.alpha = 0.6f;

        Image badge = Img(area, "GoldBadge", FrameSprite("paper_dark"), Color.white, 1.57f, true);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = new Vector2(1, 0);
        badge.rectTransform.anchoredPosition = new Vector2(-16, 16);
        badge.rectTransform.sizeDelta = new Vector2(176, 64);
        Image coin = Img(badge.transform, "Coin", s_catalog.Coin, Color.white);
        coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(34, 34));
        RectTransform badgeDyn = Nested(badge.transform, "Dynamic", false);
        TextMeshProUGUI gold = Txt(badgeDyn, "Gold", 30, GoldC, TextAlignmentOptions.Left);
        gold.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(62 + 50, 0), new Vector2(100, 40));

        Image mythic = Img(area, "Mythic", FrameSprite("ribbon_purple"), Color.white, 2.9f, true);
        mythic.rectTransform.anchorMin = mythic.rectTransform.anchorMax = mythic.rectTransform.pivot = new Vector2(0.5f, 1);
        mythic.rectTransform.anchoredPosition = new Vector2(0, -80);
        mythic.rectTransform.sizeDelta = new Vector2(640, 72);
        TextMeshProUGUI mythicText = Txt(mythic.transform, "Text", 28, Cream, TextAlignmentOptions.Center);
        mythicText.rectTransform.Fill(0, 6, 0, 0);
        mythicText.enableAutoSizing = true;
        mythicText.fontSizeMin = 16;
        mythicText.fontSizeMax = 28;
        mythic.gameObject.SetActive(false);

        Set(hud, "m_summonLog", log);
        Set(hud, "m_goldBadge", badge.rectTransform);
        Set(hud, "m_gold", gold);
        Set(hud, "m_mythic", mythic.gameObject);
        Set(hud, "m_mythicText", mythicText);
    }

    // ---------------- 진행 바 ----------------
    static void BuildProgress(BattleHud hud, RectTransform row)
    {
        Img(row, "Bg", null, Edge).rectTransform.Fill();
        RectTransform dyn = Nested(row, "Dynamic", false);
        Image track = Img(dyn, "Track", null, new Color32(0x4a, 0x30, 0x20, 255));
        track.rectTransform.Fill(8, 6, 8, 6);
        Image fill = Img(track.transform, "Fill", null, new Color32(0xe5, 0x48, 0x4d, 255));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0, 1);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI label = Txt(dyn, "Label", 26, Cream, TextAlignmentOptions.Center);
        label.rectTransform.Fill();
        Set(hud, "m_progressFill", fill.rectTransform);
        Set(hud, "m_progressFillImage", fill);
        Set(hud, "m_progressLabel", label);
    }

    // ---------------- 하단 조작부 ----------------
    static void BuildControls(BattleHud hud, RectTransform controls)
    {
        // 판자 한 장을 크게 늘린 바탕 + 얇은 나무 테두리 (border-image 8px 10px 0 10px - 아래 테두리 없음: 화면 밖으로 내린다)
        Img(controls, "Plank", AssetDatabase.LoadAssetAtPath<Sprite>(Frame + "wood_plank.png"), Color.white).rectTransform.Fill();
        const float FrameMul = 3.2f; // 52px 조각 → 약 16 단위
        Img(controls, "Frame", FrameSprite("wood_frame"), Color.white, FrameMul, true).rectTransform.Fill(0, -71 / FrameMul, 0, 0);

        Button hero = SideButton(controls, "Hero", "helmet", Loc("영웅"));
        Image dot = Img(hero.transform, "MergeDot", null, new Color32(0xe5, 0x48, 0x4d, 255));
        dot.rectTransform.Place(new Vector2(1, 1), new Vector2(-6, -6), new Vector2(20, 20));
        dot.gameObject.SetActive(false);
        Button exit = SideButton(controls, "Exit", "home", Loc("나가기"));

        Button summon = Btn(controls, "Summon", "btn_red", 1.0f);
        var summonGroup = summon.gameObject.AddComponent<CanvasGroup>();
        var summonInput = summon.gameObject.AddComponent<SummonButton>();
        TextMeshProUGUI title = Txt(summon.transform, "Title", 40, Cream, TextAlignmentOptions.Center, Loc("소환"));
        title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(300, 48));
        RectTransform costDyn = Nested(summon.transform, "Dynamic", false);
        TextMeshProUGUI cost = Txt(costDyn, "Cost", 26, Cream, TextAlignmentOptions.Center);
        cost.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(300, 34));

        // 폭이 달라져도 맞게 레이아웃 그룹으로 배치 (#controls: 세로 묶음, .control-buttons / #unit-stacks: 가로)
        RectTransform content = BattleSceneBuilder.Rt(controls, "Content");
        content.Fill();
        var col = content.gameObject.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(22, 22, 22, 16);
        col.spacing = 12;
        col.childControlWidth = col.childControlHeight = true;
        col.childForceExpandWidth = true;
        col.childForceExpandHeight = false;

        // 1줄: 영웅 · 소환(남는 폭) · 나가기
        RectTransform row = BattleSceneBuilder.Rt(content, "ButtonRow");
        Layout(row.gameObject, preferredHeight: 112);
        var rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowGroup.spacing = 14;
        rowGroup.childControlWidth = rowGroup.childControlHeight = true;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = true;
        hero.transform.SetParent(row, false);
        summon.transform.SetParent(row, false);
        exit.transform.SetParent(row, false);
        Layout(hero.gameObject, preferredWidth: 104);
        Layout(summon.gameObject, flexibleWidth: 1);
        Layout(exit.gameObject, preferredWidth: 104);

        // 2줄: 덱 카드 (한 장 최대 200 - .stack max-width 100px, 모자라면 같이 줄어든다, 가운데 정렬)
        RectTransform stacksRt = Nested(content, "Stacks", true);
        Layout(stacksRt.gameObject, flexibleHeight: 1, minHeight: 160);
        var cardsGroup = stacksRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        cardsGroup.spacing = 12;
        cardsGroup.childAlignment = TextAnchor.MiddleCenter;
        cardsGroup.childControlWidth = cardsGroup.childControlHeight = true;
        cardsGroup.childForceExpandWidth = false;
        cardsGroup.childForceExpandHeight = true;
        cardsGroup.padding = new RectOffset(0, 0, 14, 0); // 카드 위로 튀어나오는 수량·합성 표 자리
        var stacks = stacksRt.gameObject.AddComponent<UnitStacksView>();
        var cards = new StackCard[4];
        for (int i = 0; i < 4; i++)
        {
            cards[i] = Card(stacksRt, i);
            Layout(cards[i].gameObject, minWidth: 110, preferredWidth: 200);
        }
        var so = new SerializedObject(stacks);
        SerializedProperty arr = so.FindProperty("m_cards");
        arr.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        Set(hud, "m_summon", summonInput);
        Set(hud, "m_summonGroup", summonGroup);
        Set(hud, "m_summonCost", cost);
        Set(hud, "m_heroButton", hero);
        Set(hud, "m_mergeDot", dot.gameObject);
        Set(hud, "m_exitButton", exit);
        Set(hud, "m_stacks", stacks);
    }

    /// <summary>덱 카드 (style.css .stack): 테두리 = 최고 등급 색, 크림 면, 수량 배지, 합성 표, 상성 화살표, 유닛, 이름 Lv, 강화 버튼</summary>
    static StackCard Card(RectTransform parent, int index)
    {
        var root = BattleSceneBuilder.Rt(parent, "Card" + index);
        var card = root.gameObject.AddComponent<StackCard>();
        card.Group = root.gameObject.AddComponent<CanvasGroup>();
        card.Border = Img(root, "Border", null, new Color32(0x8a, 0x6f, 0x55, 255));
        card.Border.rectTransform.Fill();
        card.CardButton = card.Border.gameObject.AddComponent<Button>();
        card.CardButton.transition = Selectable.Transition.None;
        Image face = Img(root, "Face", null, new Color32(0xf1, 0xe2, 0xc2, 255));
        face.rectTransform.Fill(6);
        face.raycastTarget = false;

        // 카드 폭이 화면에 따라 달라지므로 가로는 늘어나는 앵커, 세로는 위·아래 기준
        card.Arrow = Img(root, "Arrow", null, Color.white);
        card.Arrow.rectTransform.Place(new Vector2(0, 1), new Vector2(22, -22), new Vector2(24, 24));
        card.Arrow.raycastTarget = false;
        card.Icon = Img(root, "Icon", null, Color.white);
        card.Icon.rectTransform.anchorMin = new Vector2(0, 1);
        card.Icon.rectTransform.anchorMax = new Vector2(1, 1);
        card.Icon.rectTransform.pivot = new Vector2(0.5f, 1);
        card.Icon.rectTransform.offsetMin = new Vector2(10, -82);
        card.Icon.rectTransform.offsetMax = new Vector2(-10, -10);
        card.Icon.preserveAspect = true;
        card.Icon.raycastTarget = false;
        card.Name = InkTxt(root, "Name", 22, TextAlignmentOptions.Center);
        card.Name.rectTransform.anchorMin = new Vector2(0, 0);
        card.Name.rectTransform.anchorMax = new Vector2(1, 0);
        card.Name.rectTransform.pivot = new Vector2(0.5f, 0);
        card.Name.rectTransform.offsetMin = new Vector2(8, 56);
        card.Name.rectTransform.offsetMax = new Vector2(-8, 82);
        card.Name.enableAutoSizing = true;
        card.Name.fontSizeMin = 12;
        card.Name.fontSizeMax = 22;

        card.UpButton = Btn(root, "Up", "btn_green", 2f);
        var spriteState = card.UpButton.spriteState;
        spriteState.disabledSprite = FrameSprite("btn_gray");
        card.UpButton.spriteState = spriteState;
        var upRt = card.UpButton.GetComponent<RectTransform>();
        upRt.anchorMin = new Vector2(0, 0);
        upRt.anchorMax = new Vector2(1, 0);
        upRt.pivot = new Vector2(0.5f, 0);
        upRt.offsetMin = new Vector2(10, 10);
        upRt.offsetMax = new Vector2(-10, 54);
        card.UpText = Txt(card.UpButton.transform, "Text", 22, Cream, TextAlignmentOptions.Center);
        card.UpText.rectTransform.Fill(8, 8, 8, 12);
        card.UpText.enableAutoSizing = true;
        card.UpText.fontSizeMin = 12;
        card.UpText.fontSizeMax = 22;

        Image countBg = Img(root, "CountBg", null, Edge);
        countBg.rectTransform.Place(new Vector2(1, 1), new Vector2(0, 2), new Vector2(40, 30));
        countBg.raycastTarget = false;
        card.Count = Txt(countBg.transform, "Count", 24, Cream, TextAlignmentOptions.Center);
        card.Count.rectTransform.Fill();
        Image tag = Img(root, "MergeTag", null, new Color32(0x4c, 0xaf, 0x50, 255));
        tag.rectTransform.Place(new Vector2(0, 1), new Vector2(28, 4), new Vector2(76, 30));
        tag.raycastTarget = false;
        Txt(tag.transform, "Text", 20, Cream, TextAlignmentOptions.Center, Loc("합성!")).rectTransform.Fill();
        card.MergeTag = tag.gameObject;
        tag.gameObject.SetActive(false);
        return card;
    }

    /// <summary>레이아웃 그룹 안 크기 (지정 안 한 값은 -1 = 쓰지 않음)</summary>
    internal static void Layout(GameObject go, float minWidth = -1, float preferredWidth = -1, float flexibleWidth = -1,
        float minHeight = -1, float preferredHeight = -1, float flexibleHeight = -1)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.minWidth = minWidth;
        le.preferredWidth = preferredWidth;
        le.flexibleWidth = flexibleWidth;
        le.minHeight = minHeight;
        le.preferredHeight = preferredHeight;
        le.flexibleHeight = flexibleHeight;
    }

    // ---------------- 부품 ----------------
    internal static Button SideButton(RectTransform parent, string name, string icon, string label)
    {
        Button b = Btn(parent, name, "btn_gold", 1.375f);
        Image i = Img(b.transform, "Icon", s_catalog.Icon(icon), Color.white);
        i.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(44, 44));
        i.raycastTarget = false;
        TextMeshProUGUI t = Txt(b.transform, "Label", 24, Cream, TextAlignmentOptions.Center, label);
        t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(96, 30));
        t.enableAutoSizing = true;
        t.fontSizeMin = 14;
        t.fontSizeMax = 24;
        return b;
    }

    internal static Button IconButton(RectTransform parent, string name, string icon, out Image iconImage, out TextMeshProUGUI text)
    {
        Button b = Btn(parent, name, "btn_blue", 1.7f);
        iconImage = null;
        text = null;
        if (icon != null)
        {
            iconImage = Img(b.transform, "Icon", s_catalog.Icon(icon), Color.white);
            iconImage.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(36, 36));
            iconImage.raycastTarget = false;
        }
        else
        {
            text = Txt(b.transform, "Text", 28, Cream, TextAlignmentOptions.Center, "1x");
            text.rectTransform.Fill(0, 0, 0, 6);
        }
        return b;
    }

    internal static Button Btn(Transform parent, string name, string frame, float ppuMul)
    {
        Image img = Img(parent, name, FrameSprite(frame), Color.white, ppuMul, true);
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState { pressedSprite = FrameSprite(frame + "_down") };
        return b;
    }

    /// <summary>자주 바뀌는 묶음: 중첩 Canvas (부모 칸을 채움). 버튼이 있으면 자체 Raycaster</summary>
    internal static RectTransform Nested(Transform parent, string name, bool raycast)
    {
        RectTransform rt = BattleSceneBuilder.Rt(parent, name);
        rt.Fill();
        rt.gameObject.AddComponent<Canvas>();
        if (raycast) rt.gameObject.AddComponent<GraphicRaycaster>();
        return rt;
    }

    internal static Image Img(Transform parent, string name, Sprite sprite, Color color, float ppuMul = 1f, bool sliced = false)
    {
        RectTransform rt = BattleSceneBuilder.Rt(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        if (sliced)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppuMul;
        }
        return img;
    }

    internal static TextMeshProUGUI Txt(Transform parent, string name, float size, Color color, TextAlignmentOptions align, string text = "")
    {
        RectTransform rt = BattleSceneBuilder.Rt(parent, name);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = s_font;
        t.fontSharedMaterial = s_fontMat;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        // Loc(키) 로 만든 글자: 한국어로 굽지 않고 LocText 가 실행 중에 번역한다 (앞뒤 아이콘·숫자는 그대로)
        int a = text.IndexOf(LocStart), b = text.IndexOf(LocEnd);
        if (a >= 0 && b > a)
        {
            var lt = t.gameObject.AddComponent<DefenseBattle.View.LocText>();
            lt.Set(text.Substring(a + 1, b - a - 1), text.Substring(0, a), text.Substring(b + 1));
        }
        else
        {
            t.text = text;
        }
        return t;
    }

    const char LocStart = '\u0001', LocEnd = '\u0002';

    /// <summary>크림 종이 위 진한 글자 (테두리 없는 픽셀 폰트)</summary>
    internal static TextMeshProUGUI InkTxt(Transform parent, string name, float size, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = Txt(parent, name, size, Ink, align);
        t.font = s_inkFont;
        t.fontSharedMaterial = s_inkFont.material;
        return t;
    }

    internal static Sprite FrameSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Frame + name + ".png");

    /// <summary>번역할 문장 표시 (Txt 가 LocText 로 바꾼다). Txt 가 아닌 곳에 쓰면 안 됨</summary>
    internal static string Loc(string key) => LocStart + key + LocEnd;

    static void Set(Object target, string field, Object value) => BattleSceneBuilder.Set(target, field, value);

    /// <summary>HUD 글자: SDF 숫자 폰트에 얇은 검은 테두리 (style.css --outline-thin)</summary>
    static Material HudMaterial(TMP_FontAsset font)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(HudTextMat);
        if (mat != null) return mat;
        mat = new Material(font.material) { name = "HudText" };
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.165f, 0.114f, 0.086f, 1f));
        AssetDatabase.CreateAsset(mat, HudTextMat);
        return mat;
    }

    // ---------------- RectTransform 도우미 ----------------
    internal static void Fill(this RectTransform rt, float inset = 0) => rt.Fill(inset, inset, inset, inset);

    internal static void Fill(this RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>앵커 한 점 기준 위치·크기 (가운데 피벗)</summary>
    internal static void Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}

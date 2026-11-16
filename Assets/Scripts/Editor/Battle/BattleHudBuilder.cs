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
        BuildBoardOverlay(hud, (RectTransform)safe.Find("BoardArea"));
        BuildProgress(hud, (RectTransform)safe.Find("ProgressRow"));
        BuildControls(hud, (RectTransform)safe.Find("Controls"));
        return hud;
    }

    // ---------------- 상단바 ----------------
    static void BuildTopBar(BattleHud hud, RectTransform bar)
    {
        Img(bar, "Bg", FrameSprite("wood"), Color.white, 2.67f, true).rectTransform.Fill();
        RectTransform dyn = Nested(bar, "Dynamic", false);

        Image portraitBg = Img(bar, "PortraitBg", null, new Color32(42, 29, 22, 140));
        portraitBg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(28 + 38, 0), new Vector2(76, 76));
        Image portrait = Img(portraitBg.transform, "Portrait", null, Color.white);
        portrait.rectTransform.Fill(4);
        portrait.preserveAspect = true;
        // 글자 칸 왼쪽 끝 = 초상화 오른쪽 + 10
        TextMeshProUGUI race = Txt(dyn, "Race", 26, Cream, TextAlignmentOptions.Left);
        race.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(114 + 70, 14), new Vector2(140, 34));
        TextMeshProUGUI stage = Txt(dyn, "Stage", 22, Cream2, TextAlignmentOptions.Left);
        stage.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(114 + 70, -18), new Vector2(140, 30));

        TextMeshProUGUI wave = Txt(dyn, "Wave", 34, Cream, TextAlignmentOptions.Center);
        wave.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(220, 42));
        TextMeshProUGUI boss = Txt(dyn, "BossTimer", 26, new Color32(0xff, 0x7b, 0x7b, 255), TextAlignmentOptions.Center);
        boss.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(220, 32));

        Button speed = IconButton(bar, "Speed", null, out Image _, out TextMeshProUGUI speedText);
        speed.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(-24 - 38, 0), new Vector2(76, 76));
        Button pause = IconButton(bar, "Pause", "pause", out Image pauseIcon, out _);
        pause.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(-24 - 38 - 84, 0), new Vector2(76, 76));
        Button mute = IconButton(bar, "Mute", "sound", out Image muteIcon, out _);
        mute.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(-24 - 38 - 168, 0), new Vector2(76, 76));

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
        Img(controls, "Plank", AssetDatabase.LoadAssetAtPath<Sprite>(Frame + "wood_plank.png"), new Color32(0x7a, 0x4a, 0x32, 255)).rectTransform.Fill();
        Img(controls, "Frame", FrameSprite("wood_frame"), Color.white, 2.9f, true).rectTransform.Fill();

        // 1줄: 영웅 · 소환 · 나가기
        Button hero = SideButton(controls, "Hero", "helmet", Loc("영웅"));
        hero.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(24 + 64, -24 - 62), new Vector2(128, 124));
        Image dot = Img(hero.transform, "MergeDot", null, new Color32(0xe5, 0x48, 0x4d, 255));
        dot.rectTransform.Place(new Vector2(1, 1), new Vector2(0, 0), new Vector2(22, 22));
        dot.gameObject.SetActive(false);
        Button exit = SideButton(controls, "Exit", "home", Loc("나가기"));
        exit.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(-24 - 64, -24 - 62), new Vector2(128, 124));

        Button summon = Btn(controls, "Summon", "btn_red", 1.0f);
        var summonRt = summon.GetComponent<RectTransform>();
        summonRt.anchorMin = new Vector2(0, 1);
        summonRt.anchorMax = new Vector2(1, 1);
        summonRt.pivot = new Vector2(0.5f, 1);
        summonRt.offsetMin = new Vector2(168, -24 - 136);
        summonRt.offsetMax = new Vector2(-168, -24);
        var summonGroup = summon.gameObject.AddComponent<CanvasGroup>();
        var summonInput = summon.gameObject.AddComponent<SummonButton>();
        TextMeshProUGUI title = Txt(summon.transform, "Title", 40, Cream, TextAlignmentOptions.Center, Loc("소환"));
        title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(300, 48));
        RectTransform costDyn = Nested(summon.transform, "Dynamic", false);
        TextMeshProUGUI cost = Txt(costDyn, "Cost", 26, Cream, TextAlignmentOptions.Center);
        cost.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(300, 34));

        // 2줄: 덱 카드 4장
        RectTransform stacksRt = Nested(controls, "Stacks", true);
        stacksRt.anchorMin = new Vector2(0, 0);
        stacksRt.anchorMax = new Vector2(1, 0);
        stacksRt.pivot = new Vector2(0.5f, 0);
        stacksRt.sizeDelta = new Vector2(0, 200);
        stacksRt.anchoredPosition = new Vector2(0, 20);
        var stacks = stacksRt.gameObject.AddComponent<UnitStacksView>();
        var cards = new StackCard[4];
        for (int i = 0; i < 4; i++)
        {
            cards[i] = Card(stacksRt, i);
            cards[i].GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 172, 0), new Vector2(160, 192));
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

        card.Arrow = Img(root, "Arrow", null, Color.white);
        card.Arrow.rectTransform.Place(new Vector2(0, 1), new Vector2(26, -26), new Vector2(28, 28));
        card.Arrow.raycastTarget = false;
        card.Icon = Img(root, "Icon", null, Color.white);
        card.Icon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(84, 84));
        card.Icon.preserveAspect = true;
        card.Icon.raycastTarget = false;
        card.Name = InkTxt(root, "Name", 22, TextAlignmentOptions.Center);
        card.Name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -114), new Vector2(150, 28));
        card.Name.enableAutoSizing = true;
        card.Name.fontSizeMin = 14;
        card.Name.fontSizeMax = 22;

        card.UpButton = Btn(root, "Up", "btn_green", 2f);
        var spriteState = card.UpButton.spriteState;
        spriteState.disabledSprite = FrameSprite("btn_gray");
        card.UpButton.spriteState = spriteState;
        card.UpButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(148, 56));
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

    // ---------------- 부품 ----------------
    static Button SideButton(RectTransform parent, string name, string icon, string label)
    {
        Button b = Btn(parent, name, "btn_gold", 1.375f);
        Image i = Img(b.transform, "Icon", s_catalog.Icon(icon), Color.white);
        i.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(48, 48));
        i.raycastTarget = false;
        Txt(b.transform, "Label", 26, Cream, TextAlignmentOptions.Center, label).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -28), new Vector2(124, 32));
        return b;
    }

    static Button IconButton(RectTransform parent, string name, string icon, out Image iconImage, out TextMeshProUGUI text)
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

    static Button Btn(Transform parent, string name, string frame, float ppuMul)
    {
        Image img = Img(parent, name, FrameSprite(frame), Color.white, ppuMul, true);
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState { pressedSprite = FrameSprite(frame + "_down") };
        return b;
    }

    /// <summary>자주 바뀌는 묶음: 중첩 Canvas (부모 칸을 채움). 버튼이 있으면 자체 Raycaster</summary>
    static RectTransform Nested(Transform parent, string name, bool raycast)
    {
        RectTransform rt = BattleSceneBuilder.Rt(parent, name);
        rt.Fill();
        rt.gameObject.AddComponent<Canvas>();
        if (raycast) rt.gameObject.AddComponent<GraphicRaycaster>();
        return rt;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color, float ppuMul = 1f, bool sliced = false)
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

    static TextMeshProUGUI Txt(Transform parent, string name, float size, Color color, TextAlignmentOptions align, string text = "")
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
        t.text = text;
        return t;
    }

    /// <summary>크림 종이 위 진한 글자 (테두리 없는 픽셀 폰트)</summary>
    static TextMeshProUGUI InkTxt(Transform parent, string name, float size, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = Txt(parent, name, size, Ink, align);
        t.font = s_inkFont;
        t.fontSharedMaterial = s_inkFont.material;
        return t;
    }

    static Sprite FrameSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Frame + name + ".png");

    static string Loc(string key) => DefenseBattle.Core.Loc.Tl(key);

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
    static void Fill(this RectTransform rt, float inset = 0) => rt.Fill(inset, inset, inset, inset);

    static void Fill(this RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>앵커 한 점 기준 위치·크기 (가운데 피벗)</summary>
    static void Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}

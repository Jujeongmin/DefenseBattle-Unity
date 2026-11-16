using DefenseBattle.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BattleHudBuilder;

/// <summary>
/// 전투 오버레이 만들기: 구역 드래그 입력면(Hud 전장 칸), 토스트, 영웅 관리 바텀 시트 (style.css #sell-panel, .grade-card).
/// BattleHudBuilder 다음에 부른다 (같은 UI 도우미·폰트 사용).
/// </summary>
public static class BattleOverlayBuilder
{
    static readonly Color32 Cream = new Color32(0xff, 0xf4, 0xdc, 255);
    static readonly Color32 TabOff = new Color32(0x7d, 0x5d, 0x42, 255);
    static readonly Color32 Face = new Color32(0xf1, 0xe2, 0xc2, 255);
    static readonly Color32 Border = new Color32(0x8a, 0x6f, 0x55, 255);

    public static void Build(RectTransform overlaySafe, RectTransform hudSafe, BattleRunner runner, BattleHud hud, SpriteCatalog catalog)
    {
        ZoneDragInput drag = DragSurface((RectTransform)hudSafe.Find("BoardArea"), runner);
        Toast toast = BuildToast(overlaySafe);
        BuildHeroPanel(overlaySafe, runner, hud, drag, toast, catalog);
        DefenseBattle.View.ResultPanel result = BuildResult(overlaySafe, catalog);
        LaunchPanel launch = BuildLaunch(overlaySafe);
        toast.transform.SetAsLastSibling(); // 토스트는 맨 위

        var flow = new GameObject("BattleFlow").AddComponent<BattleFlow>();
        BattleSceneBuilder.Set(flow, "m_runner", runner);
        BattleSceneBuilder.Set(flow, "m_hud", hud);
        BattleSceneBuilder.Set(flow, "m_result", result);
        BattleSceneBuilder.Set(flow, "m_launch", launch);
        BattleSceneBuilder.Set(flow, "m_drag", drag);
        BattleSceneBuilder.Set(flow, "m_toast", toast);
        var so = new SerializedObject(runner);
        so.FindProperty("m_autoStart").boolValue = false; // 시작 화면이 고른다
        so.ApplyModifiedPropertiesWithoutUndo();
        new GameObject("RewardAdsBridge").AddComponent<RewardAdsBridge>();
    }

    /// <summary>결과 창 (style.css .dialog.result): 가운데 종이 창, 위에 리본 제목</summary>
    static DefenseBattle.View.ResultPanel BuildResult(RectTransform safe, SpriteCatalog catalog)
    {
        RectTransform host = BattleSceneBuilder.Rt(safe, "ResultHost");
        host.Fill();
        var panel = host.gameObject.AddComponent<DefenseBattle.View.ResultPanel>();
        RectTransform root = BattleSceneBuilder.Rt(host, "Panel");
        root.Fill();
        Image dim = Img(root, "Dim", null, new Color32(0, 0, 0, 160));
        dim.rectTransform.Fill();

        Image dialog = Img(root, "Dialog", FrameSprite("paper"), Color.white, 1.57f, true);
        dialog.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(600, 600));
        RectTransform d = dialog.rectTransform;
        var col = dialog.gameObject.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(40, 40, 70, 40);
        col.spacing = 14;
        col.childAlignment = TextAnchor.UpperCenter;
        col.childControlWidth = col.childControlHeight = true;
        col.childForceExpandWidth = true;
        col.childForceExpandHeight = false;
        var fitter = dialog.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image ribbon = Img(root, "Ribbon", FrameSprite("ribbon_yellow"), Color.white, 2.9f, true);
        ribbon.rectTransform.SetParent(d, false);
        var ribbonLe = ribbon.gameObject.AddComponent<LayoutElement>();
        ribbonLe.ignoreLayout = true;
        ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(420, 80));
        TextMeshProUGUI title = Txt(ribbon.transform, "Title", 36, Cream, TextAlignmentOptions.Center);
        title.rectTransform.Fill(30, 10, 30, 0);
        title.enableAutoSizing = true;
        title.fontSizeMin = 20;
        title.fontSizeMax = 36;

        RectTransform stars = BattleSceneBuilder.Rt(d, "Stars");
        Layout(stars.gameObject, preferredHeight: 72);
        var starImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            starImages[i] = Img(stars, "Star" + i, catalog.Icon("star"), Color.white);
            starImages[i].rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 80, i == 1 ? 6 : 0), new Vector2(64, 64));
        }
        TextMeshProUGUI sub = InkTxt(d, "Sub", 24, TextAlignmentOptions.Center);
        sub.textWrappingMode = TextWrappingModes.Normal;
        TextMeshProUGUI rewards = InkTxt(d, "Rewards", 28, TextAlignmentOptions.Center);
        TextMeshProUGUI extra = InkTxt(d, "Extra", 20, TextAlignmentOptions.Center);
        extra.textWrappingMode = TextWrappingModes.Normal;
        extra.color = new Color32(0x7d, 0x5d, 0x42, 255);

        var buttons = new Button[3];
        var texts = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            buttons[i] = Btn(d, "Button" + i, "btn_gray", 1.7f);
            Layout(buttons[i].gameObject, preferredHeight: 84);
            texts[i] = Txt(buttons[i].transform, "Label", 28, Cream, TextAlignmentOptions.Center);
            texts[i].rectTransform.Fill(20, 12, 20, 6);
            texts[i].enableAutoSizing = true;
            texts[i].fontSizeMin = 16;
            texts[i].fontSizeMax = 28;
        }

        BattleSceneBuilder.Set(panel, "m_root", root.gameObject);
        BattleSceneBuilder.Set(panel, "m_ribbon", ribbon);
        BattleSceneBuilder.Set(panel, "m_winRibbon", FrameSprite("ribbon_yellow"));
        BattleSceneBuilder.Set(panel, "m_loseRibbon", FrameSprite("ribbon_red"));
        BattleSceneBuilder.Set(panel, "m_title", title);
        BattleSceneBuilder.Set(panel, "m_stars", stars.gameObject);
        BattleSceneBuilder.Set(panel, "m_starOn", catalog.Icon("star"));
        BattleSceneBuilder.Set(panel, "m_starOff", catalog.Icon("star_off"));
        BattleSceneBuilder.Set(panel, "m_sub", sub);
        BattleSceneBuilder.Set(panel, "m_rewards", rewards);
        BattleSceneBuilder.Set(panel, "m_extra", extra);
        SetArray(panel, "m_starImages", starImages);
        SetArray(panel, "m_buttons", buttons);
        SetArray(panel, "m_buttonTexts", texts);
        // ResultStyle 순서: Plain, Red, Green, Blue
        SetArray(panel, "m_styleSprites", new Object[] { FrameSprite("btn_gray"), FrameSprite("btn_red"), FrameSprite("btn_green"), FrameSprite("btn_blue") });
        SetArray(panel, "m_stylePressed", new Object[] { FrameSprite("btn_gray_down"), FrameSprite("btn_red_down"), FrameSprite("btn_green_down"), FrameSprite("btn_blue_down") });
        return panel;
    }

    /// <summary>임시 시작 화면: 로고 + 1-1 / 무한 모드 (3단계 로비가 대신하면 지운다)</summary>
    static LaunchPanel BuildLaunch(RectTransform safe)
    {
        RectTransform host = BattleSceneBuilder.Rt(safe, "LaunchHost");
        host.Fill();
        var panel = host.gameObject.AddComponent<LaunchPanel>();
        RectTransform root = BattleSceneBuilder.Rt(host, "Panel");
        root.Fill();
        Image bg = Img(root, "Bg", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/DefenseBattle/UI/Common/loading_bg.jpg"), Color.white);
        bg.rectTransform.Fill();
        bg.preserveAspect = false;
        Image logo = Img(root, "Logo", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/DefenseBattle/UI/Common/logo.png"), Color.white);
        logo.rectTransform.Place(new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(560, 300));
        logo.preserveAspect = true;
        Button stage = Btn(root, "Stage", "btn_red", 1.0f);
        stage.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(420, 110));
        Txt(stage.transform, "Label", 36, Cream, TextAlignmentOptions.Center, BattleHudBuilder.Loc("캠페인") + " 1-1").rectTransform.Fill(0, 12, 0, 0);
        Button endless = Btn(root, "Endless", "btn_blue", 1.0f);
        endless.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(420, 110));
        Txt(endless.transform, "Label", 36, Cream, TextAlignmentOptions.Center, BattleHudBuilder.Loc("무한 모드")).rectTransform.Fill(0, 12, 0, 0);

        BattleSceneBuilder.Set(panel, "m_root", root.gameObject);
        BattleSceneBuilder.Set(panel, "m_stage", stage);
        BattleSceneBuilder.Set(panel, "m_endless", endless);
        return panel;
    }

    static ZoneDragInput DragSurface(RectTransform area, BattleRunner runner)
    {
        Image surf = Img(area, "DragSurface", null, Color.clear);
        surf.rectTransform.Fill();
        surf.transform.SetAsFirstSibling(); // 골드 배지 등 다른 것보다 뒤 (그것들이 먼저 입력을 받는다)
        surf.raycastTarget = true;
        var drag = surf.gameObject.AddComponent<ZoneDragInput>();
        BattleSceneBuilder.Set(drag, "m_runner", runner);
        BattleSceneBuilder.Set(runner, "m_drag", drag);
        return drag;
    }

    static Toast BuildToast(RectTransform safe)
    {
        RectTransform host = BattleSceneBuilder.Rt(safe, "ToastHost");
        host.Fill();
        var toast = host.gameObject.AddComponent<Toast>();
        Image bg = Img(host, "Toast", FrameSprite("paper_dark"), Color.white, 1.57f, true);
        bg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 470), new Vector2(600, 72));
        bg.raycastTarget = false;
        var group = bg.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        TextMeshProUGUI text = Txt(bg.transform, "Text", 26, Cream, TextAlignmentOptions.Center);
        text.rectTransform.Fill(16, 8, 16, 8);
        text.enableAutoSizing = true;
        text.fontSizeMin = 16;
        text.fontSizeMax = 26;
        bg.gameObject.SetActive(false);
        BattleSceneBuilder.Set(toast, "m_group", group);
        BattleSceneBuilder.Set(toast, "m_text", text);
        return toast;
    }

    static void BuildHeroPanel(RectTransform safe, BattleRunner runner, BattleHud hud, ZoneDragInput drag, Toast toast, SpriteCatalog catalog)
    {
        RectTransform host = BattleSceneBuilder.Rt(safe, "HeroPanelHost");
        host.Fill();
        host.SetSiblingIndex(0); // 토스트보다 아래
        var panel = host.gameObject.AddComponent<HeroPanel>();

        RectTransform root = BattleSceneBuilder.Rt(host, "Panel");
        root.Fill();
        Image dim = Img(root, "Dim", null, new Color32(26, 18, 12, 115));
        dim.rectTransform.Fill();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;

        Image sheet = Img(root, "Sheet", FrameSprite("paper"), Color.white, 1.57f, true);
        sheet.rectTransform.anchorMin = new Vector2(0, 0);
        sheet.rectTransform.anchorMax = new Vector2(1, 0);
        sheet.rectTransform.pivot = new Vector2(0.5f, 0);
        sheet.rectTransform.sizeDelta = new Vector2(0, 640);
        sheet.rectTransform.anchoredPosition = Vector2.zero;
        RectTransform s = sheet.rectTransform;

        Image ribbon = Img(s, "Title", FrameSprite("ribbon_yellow"), Color.white, 2.9f, true);
        ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(340, 70));
        Txt(ribbon.transform, "Text", 32, Cream, TextAlignmentOptions.Center, BattleHudBuilder.Loc("영웅 관리")).rectTransform.Fill(0, 8, 0, 0);
        Button close = Btn(s, "Close", "round_red", 1.0f);
        close.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(-50, -46), new Vector2(64, 64));
        Image x = Img(close.transform, "Icon", catalog.Icon("close"), Color.white);
        x.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(30, 30));
        x.raycastTarget = false;

        // 직업 탭
        var tabs = new HeroTab[6];
        for (int i = 0; i < tabs.Length; i++)
        {
            Image bg = Img(s, "Tab" + i, null, TabOff);
            bg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2((i - 2.5f) * 112, -124), new Vector2(104, 56));
            var tab = bg.gameObject.AddComponent<HeroTab>();
            tab.Bg = bg;
            tab.Button = bg.gameObject.AddComponent<Button>();
            tab.Label = Txt(bg.transform, "Label", 22, Cream, TextAlignmentOptions.Center);
            tab.Label.rectTransform.Fill(4);
            tab.Label.enableAutoSizing = true;
            tab.Label.fontSizeMin = 12;
            tab.Label.fontSizeMax = 22;
            Image dot = Img(bg.transform, "Dot", null, new Color32(0xe5, 0x48, 0x4d, 255));
            dot.rectTransform.Place(new Vector2(1, 1), new Vector2(-2, -2), new Vector2(16, 16));
            dot.raycastTarget = false;
            tab.Dot = dot.gameObject;
            tabs[i] = tab;
        }

        // 안내 + 판매 수량
        TextMeshProUGUI hint = InkTxt(s, "Hint", 24, TextAlignmentOptions.Left);
        hint.rectTransform.Place(new Vector2(0, 1), new Vector2(40 + 200, -196), new Vector2(400, 40));
        RectTransform qtyRow = BattleSceneBuilder.Rt(s, "QtyRow");
        qtyRow.Place(new Vector2(1, 1), new Vector2(-40 - 120, -196), new Vector2(240, 48));
        var qty = new Button[3];
        string[] qtyLabels = { "1", "10", "ALL" };
        for (int i = 0; i < 3; i++)
        {
            Image b = Img(qtyRow, "Qty" + i, null, TabOff);
            b.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 80, 0), new Vector2(72, 48));
            qty[i] = b.gameObject.AddComponent<Button>();
            Txt(b.transform, "Label", 22, Cream, TextAlignmentOptions.Center, qtyLabels[i]).rectTransform.Fill();
        }

        // 등급 카드 6장
        var cards = new GradeCardView[6];
        for (int i = 0; i < cards.Length; i++)
        {
            Image border = Img(s, "Grade" + i, null, Border);
            border.rectTransform.Place(new Vector2(0.5f, 1), new Vector2((i - 2.5f) * 112, -330), new Vector2(104, 156));
            var card = border.gameObject.AddComponent<GradeCardView>();
            card.Border = border;
            card.Group = border.gameObject.AddComponent<CanvasGroup>();
            card.Button = border.gameObject.AddComponent<Button>();
            card.Button.transition = Selectable.Transition.None;
            Image face = Img(border.transform, "Face", null, Face);
            face.rectTransform.Fill(5);
            face.raycastTarget = false;
            card.Name = Txt(border.transform, "Name", 20, Cream, TextAlignmentOptions.Center);
            card.Name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(98, 28));
            card.Name.enableAutoSizing = true;
            card.Name.fontSizeMin = 12;
            card.Name.fontSizeMax = 20;
            card.Icon = Img(border.transform, "Icon", null, Color.white);
            card.Icon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -68), new Vector2(64, 64));
            card.Icon.preserveAspect = true;
            card.Icon.raycastTarget = false;
            card.Count = InkTxt(border.transform, "Count", 22, TextAlignmentOptions.Center);
            card.Count.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(98, 28));
            card.Bottom = Txt(border.transform, "Bottom", 20, Cream, TextAlignmentOptions.Center);
            card.Bottom.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(98, 26));
            card.Bottom.enableAutoSizing = true;
            card.Bottom.fontSizeMin = 12;
            card.Bottom.fontSizeMax = 20;
            cards[i] = card;
        }

        // 합성 / 판매 탭
        Image mergeBg = Img(s, "MergeTab", null, TabOff);
        mergeBg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(-160, 70), new Vector2(300, 76));
        var mergeTab = mergeBg.gameObject.AddComponent<Button>();
        TextMeshProUGUI mergeText = Txt(mergeBg.transform, "Label", 28, Cream, TextAlignmentOptions.Center);
        mergeText.rectTransform.Fill();
        Image sellBg = Img(s, "SellTab", null, TabOff);
        sellBg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(160, 70), new Vector2(300, 76));
        var sellTab = sellBg.gameObject.AddComponent<Button>();
        Txt(sellBg.transform, "Label", 28, Cream, TextAlignmentOptions.Center,
            DefenseBattle.Core.Loc.Icon("coin") + " " + BattleHudBuilder.Loc("판매")).rectTransform.Fill();

        BattleSceneBuilder.Set(panel, "m_runner", runner);
        BattleSceneBuilder.Set(panel, "m_hud", hud);
        BattleSceneBuilder.Set(panel, "m_drag", drag);
        BattleSceneBuilder.Set(panel, "m_toast", toast);
        BattleSceneBuilder.Set(panel, "m_root", root.gameObject);
        BattleSceneBuilder.Set(panel, "m_dim", dimButton);
        BattleSceneBuilder.Set(panel, "m_close", close);
        BattleSceneBuilder.Set(panel, "m_hint", hint);
        BattleSceneBuilder.Set(panel, "m_qtyRow", qtyRow.gameObject);
        BattleSceneBuilder.Set(panel, "m_mergeTab", mergeTab);
        BattleSceneBuilder.Set(panel, "m_mergeTabText", mergeText);
        BattleSceneBuilder.Set(panel, "m_sellTab", sellTab);
        SetArray(panel, "m_tabs", tabs);
        SetArray(panel, "m_qty", qty);
        SetArray(panel, "m_cards", cards);
    }

    static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty arr = so.FindProperty(field);
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

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

using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>트로피 로드 (menu.ts openRoad): 아레나 구간과 보상 (도달하면 로비·대전 결과에서 자동 수령)</summary>
    public sealed class RoadPage
    {
        readonly SpriteCatalog m_cat;
        readonly Modal m_panel;
        readonly Action m_closed;

        public RoadPage(SpriteCatalog cat, Modal panel, Action closed)
        {
            m_cat = cat;
            m_panel = panel;
            m_closed = closed;
        }

        public void Open()
        {
            EconState e = EconCache.State;
            int trophies = e.Pvp.Trophies;
            RectTransform body = m_panel.Panel(Loc.Tl("트로피 로드"), m_closed);
            Ui.Line(body, Loc.Icon("trophy") + " " + trophies + " " + Loc.Tl("· 대전에서 이기면 트로피가 올라요"), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
            foreach (RoadStep s in TrophyRoad.Steps)
            {
                foreach (PvpTier t in Arenas.All)
                    if (t.Min == s.At) Ui.Line(body, Loc.Tl(t.Kr) + " " + Loc.Tl("아레나"), 30, Ui.Hex(t.Color));
                bool done = e.RoadClaimed.Contains(s.At);
                bool reached = trophies >= s.At;
                Image row = Ui.Frame(body, done ? "btn_green" : reached ? "btn_gold" : "paper_dark", 1.8f, "Step" + s.At);
                Ui.Size(row.gameObject, -1, 84);
                Ui.Row(row.transform, 12, 20, 20, 8, 12, TextAnchor.MiddleLeft);
                Ui.Size(Ui.Line(row.transform, Loc.Icon("trophy") + s.At, 26, Ui.Cream, TextAlignmentOptions.Left).gameObject, 130, -1);
                Image pic = RewardPic(row.transform, s);
                if (pic != null) Ui.Size(pic.gameObject, 56, 56);
                Ui.Size(Ui.Line(row.transform, Lobby.RoadText(s), 24, Ui.Cream, TextAlignmentOptions.Left).gameObject, -1, -1, 1);
                Ui.Icon(row.transform, done ? "check" : reached ? "gift" : "lock", 40);
            }
        }

        /// <summary>보상 그림 (menu.ts roadRewardIcon): 유닛·몹 카드는 캐릭터, 상자는 상자 색 (보석은 글자 안 아이콘)</summary>
        Image RewardPic(Transform parent, RoadStep s)
        {
            Sprite sp = null;
            Color c = Color.white;
            if (s.Unit != null) sp = m_cat.Unit(s.Unit.Value, 0)[1];
            else if (s.Attack != null) { AttackDef a = Attacks.Get(s.Attack.Value); sp = m_cat.Mob(a.Race, a.Variant)[1]; }
            else if (s.Chest != null) { sp = m_cat.Frame("chest"); c = Color.Lerp(Color.white, Ui.Hex(Chests.Get(s.Chest.Value).Color), 0.55f); }
            if (sp == null) return null;
            Image img = Ui.Image(parent, sp, c, "Reward");
            img.preserveAspect = true;
            return img;
        }
    }
}

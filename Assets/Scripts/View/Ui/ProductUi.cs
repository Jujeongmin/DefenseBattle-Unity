using System.Collections.Generic;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>상품 그림·효율 배지·구성 줄 (상점 카드와 특가 팝업이 같이 쓴다)</summary>
    public static class ProductUi
    {
        /// <summary>효율 표 (기본 보석 상품 대비 100% 넘을 때만, 부모 왼쪽 위 모서리)</summary>
        public static void EffBadge(Transform parent, ProductDef p)
        {
            int eff = Products.Efficiency(p);
            if (eff <= 100) return;
            Image tag = Ui.Image(parent, null, Ui.Red, "Eff");
            tag.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Ui.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(56, -6), new Vector2(116, 34));
            Ui.Fill(Ui.Line(tag.transform, Loc.Tl("효율") + " " + eff + "%", 18, Ui.Cream).rectTransform, 4, 0, 4, 0);
        }

        /// <summary>상품 그림 (UI/Shop/{id}, 없으면 상품 아이콘)</summary>
        public static Image Picture(Transform parent, ProductDef p, float size)
        {
            Image img = Ui.Image(parent, Ui.Cat.Frame(p.Id) ?? Ui.Cat.Icon(p.Icon), Color.white, "Pic");
            img.preserveAspect = true;
            Ui.Size(img.gameObject, size, size);
            return img;
        }

        /// <summary>구성 줄: 유닛(그림 + 이름 Lv), 보석, 아이템 ×n (offers.ts grantRows)</summary>
        public static void GrantRows(Transform parent, SpriteCatalog cat, ProductDef p)
        {
            ProductGrant g = p.Grant;
            foreach (KeyValuePair<Job, int> kv in g.Units)
            {
                RectTransform row = Row(parent);
                Image u = Ui.Image(row, cat.Unit(kv.Key, 0)[1], Color.white, "Unit");
                u.preserveAspect = true;
                Ui.Size(u.gameObject, 44, 44);
                Ui.Line(row, Loc.Tl(Units.Get(kv.Key).Kr) + " <b>Lv" + kv.Value + "</b>", 24, Ui.Cream, TextAlignmentOptions.Left);
            }
            if (g.Crystals > 0)
            {
                RectTransform row = Row(parent);
                Ui.Icon(row, "gem", 36);
                Ui.Line(row, Loc.Tl("보석") + " <b>" + Ui.Num(g.Crystals) + "</b>", 24, Ui.Cream, TextAlignmentOptions.Left);
            }
            foreach (KeyValuePair<ItemKey, int> kv in g.Items)
            {
                RectTransform row = Row(parent);
                Ui.Icon(row, Products.ItemIcon(kv.Key), 36);
                Ui.Line(row, Loc.Tl(Products.ItemKr(kv.Key)) + " <b>×" + kv.Value + "</b>", 24, Ui.Cream, TextAlignmentOptions.Left);
            }
        }

        static RectTransform Row(Transform parent)
        {
            RectTransform row = Ui.Rect(parent, "Grant");
            Ui.Size(row.gameObject, -1, 46);
            Ui.Row(row, 10, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            return row;
        }

        /// <summary>남은 시간 h:mm:ss (offers.ts timeLeft)</summary>
        public static string Clock(long ms)
        {
            long h = ms / 3_600_000L, m = ms % 3_600_000L / 60_000L, s = ms % 60_000L / 1000L;
            return h + ":" + m.ToString("00") + ":" + s.ToString("00");
        }
    }
}

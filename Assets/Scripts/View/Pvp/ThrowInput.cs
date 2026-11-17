using System;
using DefenseBattle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 몹 카드 던지기 (ui.ts buildAttackCards): 카드를 누르면 상대 미니맵이 크게 펼쳐지고, 손가락을 따라 몹 그림이 움직인다.
    /// 미니맵 위에서 떼면 그 지점에서 가장 가까운 길 위치로 Game.SendAttack → 카드에서 미니맵 착지점까지 포물선.
    /// 미니맵 밖에서 떼면 취소 (움직이지 않고 떼면 던지는 법 안내). 골드·쿨타임이 모자라면 골드 배지가 깜빡인다.
    /// </summary>
    public sealed class ThrowInput
    {
        const float GhostSize = 72, TapSlop = 12;

        readonly PvpHud m_hud;
        readonly RectTransform m_parent;
        readonly SpriteCatalog m_cat;
        readonly Image m_ghost;
        readonly ThrowFly m_fly;
        readonly Vector3[] m_corners = new Vector3[4];
        Game m_game;
        int m_slot = -1;
        Vector2 m_downAt;

        public Toast Toast;
        public Action FlashGold;
        /// <summary>던지기 성공 (첫 대전 안내를 닫는다)</summary>
        public event Action Thrown;

        public ThrowInput(PvpHud hud, RectTransform parent, SpriteCatalog cat)
        {
            m_hud = hud;
            m_parent = parent;
            m_cat = cat;
            m_ghost = Ui.Image(parent, null, new Color(1, 1, 1, 0.9f), "ThrowGhost");
            m_ghost.preserveAspect = true;
            RectTransform g = m_ghost.rectTransform;
            g.anchorMin = g.anchorMax = new Vector2(0.5f, 0.5f);
            g.sizeDelta = new Vector2(GhostSize, GhostSize);
            m_ghost.gameObject.SetActive(false);
            m_fly = new ThrowFly(parent);
            m_fly.Landed += () => { if (m_hud.Peek != null) m_hud.Peek.Hit(); };
            hud.CardDown += OnDown;
            hud.CardDrag += OnDrag;
            hud.CardUp += OnUp;
        }

        public void Bind(Game g)
        {
            Cancel();
            m_fly.Clear();
            m_game = g;
        }

        public void Tick(float dt) => m_fly.Tick(dt);

        public void Cancel()
        {
            m_slot = -1;
            m_ghost.gameObject.SetActive(false);
            if (m_hud.Peek != null) m_hud.Peek.Aiming = false;
            if (m_hud.OppView != null) m_hud.OppView.SetAim(null);
        }

        void OnDown(int slot, PointerEventData e)
        {
            if (m_game == null || m_game.Over || m_hud.CountingDown || !m_hud.CardEnabled(slot)) { FlashGold?.Invoke(); return; }
            m_slot = slot;
            m_downAt = e.position;
            AttackDef a = Attacks.Get(m_hud.CardId(slot));
            m_ghost.sprite = m_cat.Mob(a.Race, a.Variant)[1];
            m_ghost.gameObject.SetActive(true);
            m_ghost.rectTransform.SetAsLastSibling();
            m_hud.Peek.Aiming = true; // 상대 전장을 크게 펼친다
            Move(e);
        }

        void OnDrag(int slot, PointerEventData e)
        {
            if (slot != m_slot) return;
            Move(e);
        }

        void Move(PointerEventData e)
        {
            m_ghost.rectTransform.anchoredPosition = Local(e.position, e.pressEventCamera);
            m_hud.OppView.SetAim(PosAt(e.position, e.pressEventCamera));
        }

        void OnUp(int slot, PointerEventData e)
        {
            if (slot != m_slot) return;
            double? pos = PosAt(e.position, e.pressEventCamera);
            float moved = Vector2.Distance(e.position, m_downAt);
            int card = m_hud.CardId(slot);
            Cancel();
            if (pos.HasValue)
            {
                if (m_game != null && m_game.SendAttack(card, pos.Value))
                {
                    AttackDef a = Attacks.Get(card);
                    m_fly.Launch(m_cat.Mob(a.Race, a.Variant)[1], Center(m_hud.CardRect(slot)), SlotPoint(pos.Value));
                    m_hud.OppView.MarkThrow(pos.Value);
                    Thrown?.Invoke();
                }
                else FlashGold?.Invoke();
            }
            else if (moved < TapSlop && Toast != null) Toast.Show(Loc.Tl("카드를 위쪽 <b>상대 전장</b>으로 끌어다 던지세요"));
        }

        /// <summary>화면 점 → 상대 길 위치 (미니맵 밖이면 null)</summary>
        double? PosAt(Vector2 screen, Camera cam)
        {
            RectTransform view = m_hud.Peek.View;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, cam, out Vector2 local)) return null;
            Rect r = view.rect;
            float u = (local.x - r.xMin) / r.width, v = (r.yMax - local.y) / r.height;
            if (u < 0 || u > 1 || v < 0 || v > 1) return null;
            return Board.PathPosNear(u * Board.MapSize, v * Board.MapSize);
        }

        /// <summary>작은 미니맵 칸 위 길 위치 pos 의 점 (펼친 창이 줄어든 뒤 실제로 보일 자리 - ui.ts oppPoint)</summary>
        Vector2 SlotPoint(double pos)
        {
            m_hud.OppSlot.GetWorldCorners(m_corners);
            Vector3 lo = m_parent.InverseTransformPoint(m_corners[0]), hi = m_parent.InverseTransformPoint(m_corners[2]);
            Vec2 p = Board.PathAt(pos);
            float x = Mathf.Lerp(lo.x, hi.x, (float)(p.X / Board.MapSize)), y = Mathf.Lerp(hi.y, lo.y, (float)(p.Y / Board.MapSize));
            return new Vector2(x, y) - m_parent.rect.center;
        }

        Vector2 Center(RectTransform r)
        {
            r.GetWorldCorners(m_corners);
            Vector3 c = m_parent.InverseTransformPoint((m_corners[0] + m_corners[2]) * 0.5f);
            return new Vector2(c.x, c.y) - m_parent.rect.center;
        }

        Vector2 Local(Vector2 screen, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_parent, screen, cam, out Vector2 local);
            return local - m_parent.rect.center;
        }
    }
}

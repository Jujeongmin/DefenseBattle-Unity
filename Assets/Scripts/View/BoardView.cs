using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 전장 바닥 (render.ts drawBackground, drawZones, drawGaps, drawBurns): 구운 배경 스프라이트 + 아래층 도형.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer m_background;

        /// <summary>빨간 빈틈 다시 계산 간격 (render.ts: 0.4)</summary>
        const double GapRefresh = 0.4;

        readonly List<int> m_gapIdx = new List<int>(BoardSnapshot.GapSamples);
        readonly List<Vector2> m_gaps = new List<Vector2>(BoardSnapshot.GapSamples);
        double m_gapAt = -1;

        /// <summary>지금 빨갛게 보이는 빈틈 수 (판 안 안내용)</summary>
        public int GapCount => m_gaps.Count;

        public void Init(SpriteCatalog catalog)
        {
            if (m_background != null) m_background.sprite = catalog.Field;
        }

        /// <summary>새 판</summary>
        public void ResetState()
        {
            m_gaps.Clear();
            m_gapAt = -1;
        }

        public void Draw(Game g, double t, ShapeBatch under, ZoneDragLogic drag)
        {
            DrawZones(g, t, under, drag);
            DrawGaps(g, t, under);
            DrawBurns(g, t, under);
        }

        /// <summary>구역 (render.ts drawZones): 드래그 중이면 출발 구역 밝게·대상 금색·나머지 어둡게, 둘은 움직이는 점선</summary>
        void DrawZones(Game g, double t, ShapeBatch b, ZoneDragLogic drag)
        {
            bool dragging = drag != null && drag.Active;
            Color32 edge = Rgba.Of(20, 20, 20, 0.18);
            for (int i = 0; i < g.Zones.Length; i++)
            {
                Zone z = g.Zones[i];
                bool isFrom = dragging && drag.From == i, isTo = dragging && drag.To == i;
                Color32 fill = isFrom ? Rgba.Of(255, 255, 255, 0.16)
                    : isTo ? Rgba.Of(255, 207, 63, 0.22)
                    : dragging ? Rgba.Of(0, 0, 0, 0.10)
                    : z.Job.HasValue ? Rgba.Hex(Units.Get(z.Job.Value).Tint, 0.10) : Rgba.Of(255, 255, 255, 0.02);
                Vector2 a = P(z.Tri[0]), c1 = P(z.Tri[1]), c2 = P(z.Tri[2]);
                b.FillTriangle(a, c1, c2, fill);
                if (isFrom || isTo)
                {
                    Color32 dash = isTo ? Rgba.Hex(0xffcf3f) : Rgba.Of(255, 255, 255, 0.85);
                    float offset = (float)(t * 40); // 개미행렬 점선 (lineDashOffset = -t*40)
                    offset = DashedLine(b, a, c1, offset, dash);
                    offset = DashedLine(b, c1, c2, offset, dash);
                    DashedLine(b, c2, a, offset, dash);
                }
                else
                {
                    b.Line(a.x, a.y, c1.x, c1.y, 2, edge);
                    b.Line(c1.x, c1.y, c2.x, c2.y, 2, edge);
                    b.Line(c2.x, c2.y, a.x, a.y, 2, edge);
                }
            }
        }

        /// <summary>점선 [10, 6] 굵기 4. phase 는 무늬 시작 위치(px) - 이어지는 다음 변에 넘긴다</summary>
        static float DashedLine(ShapeBatch b, Vector2 from, Vector2 to, float phase, Color32 c)
        {
            const float Dash = 10, Gap = 6, Period = Dash + Gap;
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 1e-3f) return phase;
            Vector2 u = d / len;
            float s = phase % Period - Period; // phase 가 커질수록 무늬가 선 방향으로 흐른다
            for (; s < len; s += Period)
            {
                float a0 = Mathf.Max(0, s), a1 = Mathf.Min(len, s + Dash);
                if (a1 <= a0) continue;
                b.Line(from.x + u.x * a0, from.y + u.y * a0, from.x + u.x * a1, from.y + u.y * a1, 4, c);
            }
            return phase + len;
        }

        /// <summary>드래그 화살표 (출발 구역 중심 → 손가락) + 대상 구역 위 교환 표시 (render.ts drawDrag / drawSwapMark)</summary>
        public void DrawDrag(Game g, ShapeBatch b, ZoneDragLogic drag)
        {
            if (drag == null || !drag.Active) return;
            Zone from = g.Zones[drag.From];
            float cx = (float)from.Cx, cy = (float)from.Cy;
            float dx = drag.X - cx, dy = drag.Y - cy;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 24) return; // 너무 짧으면 생략
            float ux = dx / len, uy = dy / len;
            Color32 white = Rgba.Of(255, 255, 255, 0.9);
            b.Line(cx, cy, drag.X - ux * 16, drag.Y - uy * 16, 5, white);
            b.FillTriangle(new Vector2(drag.X, drag.Y),
                new Vector2(drag.X - ux * 18 - uy * 9, drag.Y - uy * 18 + ux * 9),
                new Vector2(drag.X - ux * 18 + uy * 9, drag.Y - uy * 18 - ux * 9), white);
            if (drag.To >= 0)
            {
                Zone to = g.Zones[drag.To];
                DrawSwapMark(b, (float)to.Cx, (float)to.Cy);
            }
        }

        /// <summary>엇갈린 두 화살표 (흰 면 + 검은 테두리)</summary>
        static void DrawSwapMark(ShapeBatch b, float x, float y)
        {
            Color32 edge = Rgba.Of(0, 0, 0, 0.6), face = Rgba.Hex(0xffffff);
            for (int k = 0; k < 2; k++)
            {
                int dir = k == 0 ? 1 : -1;
                float yy = y + dir * 8;
                // 테두리 (4px) 먼저 크게, 그 위에 흰 면
                b.Line(x - dir * 18, yy, x + dir * 8, yy, 10, edge);
                b.FillTriangle(new Vector2(x + dir * 6, yy - 12), new Vector2(x + dir * 21, yy), new Vector2(x + dir * 6, yy + 12), edge);
                b.Line(x - dir * 16, yy, x + dir * 8, yy, 6, face);
                b.FillTriangle(new Vector2(x + dir * 8, yy - 9), new Vector2(x + dir * 18, yy), new Vector2(x + dir * 8, yy + 9), face);
            }
        }

        /// <summary>어떤 유닛도 닿지 않는 길 구간을 빨갛게 (0.4초마다 다시 계산)</summary>
        void DrawGaps(Game g, double t, ShapeBatch b)
        {
            if (g.Units.Count == 0) return;
            if (t - m_gapAt > GapRefresh || m_gapAt < 0)
            {
                m_gapAt = t;
                g.PathGaps(m_gapIdx);
                m_gaps.Clear();
                foreach (int i in m_gapIdx)
                {
                    Vec2 p = Board.PathAt((double)i / BoardSnapshot.GapSamples);
                    m_gaps.Add(new Vector2((float)p.X, (float)p.Y));
                }
            }
            if (m_gaps.Count == 0) return;
            Color32 c = Rgba.Of(255, 50, 40, 0.22 + Math.Sin(t * 4) * 0.08);
            foreach (Vector2 p in m_gaps) b.FillRect(p.x - 12, p.y - 12, 24, 24, c);
        }

        /// <summary>화염술사 불 장판: 중심 주황 → 0.6 지점 붉은 주황 → 가장자리 투명</summary>
        void DrawBurns(Game g, double t, ShapeBatch b)
        {
            foreach (Burn burn in g.Burns)
            {
                double left = Math.Min(1, (burn.Until - g.Time) / 0.6);
                float r = (float)(burn.R * (0.95 + Math.Sin(t * 9 + burn.X) * 0.05));
                float x = (float)burn.X, y = (float)burn.Y;
                Color32 mid = Rgba.Of(255, 90, 30, 0.3 * left);
                b.RadialCircle(x, y, r * 0.6f, Rgba.Of(255, 190, 60, 0.45 * left), mid, 0f);
                b.RadialRing(x, y, r * 0.6f, r, mid, Rgba.Of(255, 60, 20, 0));
            }
        }

        static Vector2 P(Vec2 v) => new Vector2((float)v.X, (float)v.Y);
    }
}

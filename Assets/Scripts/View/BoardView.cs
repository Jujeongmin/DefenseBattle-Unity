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

        public void Draw(Game g, double t, ShapeBatch under)
        {
            DrawZones(g, under);
            DrawGaps(g, t, under);
            DrawBurns(g, t, under);
        }

        void DrawZones(Game g, ShapeBatch b)
        {
            Color32 edge = Rgba.Of(20, 20, 20, 0.18);
            foreach (Zone z in g.Zones)
            {
                Color32 fill = z.Job.HasValue ? Rgba.Hex(Units.Get(z.Job.Value).Tint, 0.10) : Rgba.Of(255, 255, 255, 0.02);
                Vector2 a = P(z.Tri[0]), c1 = P(z.Tri[1]), c2 = P(z.Tri[2]);
                b.FillTriangle(a, c1, c2, fill);
                b.Line(a.x, a.y, c1.x, c1.y, 2, edge);
                b.Line(c1.x, c1.y, c2.x, c2.y, 2, edge);
                b.Line(c2.x, c2.y, a.x, a.y, 2, edge);
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

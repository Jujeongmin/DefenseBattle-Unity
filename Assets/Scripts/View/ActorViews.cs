using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 유닛·몹 그리기 (render.ts drawMobs / drawMobAura / drawUnitAuras / drawUnits / drawUnitSparkles).
    /// 스프라이트는 풀링한 SpriteRenderer 를 Id 로 짝짓는다 - 엔진이 죽은 Mob 객체를 다른 Id 로 다시 쓰므로 객체 참조를 들지 않는다.
    /// 이번 프레임에 안 보인 Id 의 렌더러는 풀로 돌아간다.
    /// 층: 그림자·오라 → under / 몹 스프라이트 10 / 상태색·체력바 → mobOver / 유닛 스프라이트 20 / 반짝임 → over
    /// </summary>
    public sealed class ActorViews : MonoBehaviour
    {
        /// <summary>유닛 그리기 크기 (render.ts UNIT_DRAW)</summary>
        const float UnitDraw = 62;
        const int MobOrder = 10, UnitOrder = 20;
        /// <summary>시트 한 칸 (px) = 월드 1 단위 (PPU 32)</summary>
        const float Cell = 32;

        sealed class Actor
        {
            public GameObject Go;
            public SpriteRenderer Main;
            /// <summary>피격 흰 번쩍임 (가산)</summary>
            public SpriteRenderer Flash;
            public int Stamp;
        }

        SpriteCatalog m_catalog;
        Material m_spriteMat, m_flashMat;
        readonly Dictionary<int, Actor> m_mobs = new Dictionary<int, Actor>(128);
        readonly Dictionary<int, Actor> m_units = new Dictionary<int, Actor>(64);
        readonly Stack<Actor> m_free = new Stack<Actor>(64);
        readonly List<int> m_gone = new List<int>(64);
        int m_stamp;

        public int ActiveCount => m_mobs.Count + m_units.Count;

        public void Init(SpriteCatalog catalog, Material spriteMaterial, Material flashMaterial)
        {
            m_catalog = catalog;
            m_spriteMat = spriteMaterial;
            m_flashMat = flashMaterial;
        }

        public SpriteRenderer MobRenderer(int id) => m_mobs.TryGetValue(id, out Actor a) ? a.Main : null;
        public SpriteRenderer UnitRenderer(int id) => m_units.TryGetValue(id, out Actor a) ? a.Main : null;

        /// <summary>새 판: 모두 풀로</summary>
        public void ResetState()
        {
            Release(m_mobs, int.MaxValue);
            Release(m_units, int.MaxValue);
        }

        public void Draw(Game g, double t, ShapeBatch under, ShapeBatch mobOver, ShapeBatch over)
        {
            m_stamp++;
            DrawUnitAuras(g, t, under);
            foreach (Mob m in g.Mobs) DrawMob(g, m, t, under, mobOver);
            foreach (Unit u in g.Units) DrawUnit(u, t);
            DrawUnitSparkles(g, t, over);
            Release(m_mobs, m_stamp);
            Release(m_units, m_stamp);
        }

        // ---------------- 몹 ----------------
        void DrawMob(Game g, Mob m, double t, ShapeBatch under, ShapeBatch mobOver)
        {
            float s = (float)m.Size, x = (float)m.X, y = (float)m.Y;
            under.FillEllipse(x, y + s * 0.38f, s * 0.32f, s * 0.12f, Rgba.Of(0, 0, 0, 0.25)); // 발밑 그림자
            DrawMobAura(m, t, under);

            double basePeriod = m.Kind == MobKind.Fast ? 0.1 : m.Kind == MobKind.Tank ? 0.22 : 0.16;
            double period = m.Slow > 0 ? basePeriod / Math.Max(0.3, 1 - m.SlowPct) : basePeriod; // 둔화: 걸음도 느리게
            int col = m.Pause > 0 ? 1 : SpriteAnim.WalkCol(t, m.Id * 0.07, period); // 코너 정지 시 대기 프레임
            Sprite sprite = m_catalog.Mob(m.Race, m.Variant)[SpriteAnim.FrameIndex(m.Dir, col)];

            float alpha = m.HitFlash > 0 ? 0.65f : 1f;
            if (m.Ghost) alpha *= (float)(0.6 + Math.Sin(t * 3 + m.Id) * 0.1); // 유령: 반투명
            if (m.Hidden) alpha *= 0.22f; // 그림자 도적: 숨음
            Actor a = Get(m_mobs, m.Id, MobOrder);
            Place(a, sprite, x, y, s, alpha, m.HitFlash > 0.06);

            // 상태 덧칠: 중독(초록)·표식(붉은 과녁)·광폭(빨강)·둔화(얼음)
            if (m.Poison.Until > g.Time)
                mobOver.FillCircle(x, y, s * 0.34f, Rgba.Of(120, 220, 80, 0.18 + m.Poison.Stacks * 0.07));
            if (m.MarkUntil > g.Time)
            {
                Color32 red = Rgba.Of(255, 80, 60, 0.9);
                float my = y - s * 0.62f;
                mobOver.StrokeCircle(x, my, 6, 2, red);
                mobOver.Line(x - 9, my, x + 9, my, 2, red);
                mobOver.Line(x, my - 9, x, my + 9, 2, red);
            }
            if (m.Enraged) mobOver.FillCircle(x, y, s * 0.38f, Rgba.Of(255, 60, 40, 0.22 + Math.Sin(t * 10 + m.Id) * 0.08));
            if (m.Slow > 0)
            {
                double k = Math.Min(1, m.Slow / 0.4); // 끝나갈 때 옅어진다
                mobOver.FillCircle(x, y, s * 0.36f, Rgba.Of(120, 200, 255, 0.34 * k));
                mobOver.StrokeEllipse(x, y + s * 0.38f, s * 0.42f, s * 0.16f, 5, Rgba.Of(150, 220, 255, 0.9 * k));
            }

            // 체력 막대 (빨강 고정, 상대가 보낸 몹만 주황)
            float w = Mathf.Max(28, s * 0.8f), h = m.Boss ? 6 : 4;
            float ratio = (float)Math.Max(0, m.Hp / m.MaxHp);
            float by = y - s / 2 - 8;
            mobOver.FillRect(x - w / 2 - 1, by - 1, w + 2, h + 2, Rgba.Of(0, 0, 0, 0.65));
            mobOver.FillRect(x - w / 2, by, w * ratio, h, m.Kind == MobKind.Card && !m.Boss ? Rgba.Hex(0xff8a3d) : Rgba.Hex(0xff5252));
            if (m.Slow > 0)
            {
                // 체력 막대 오른쪽 파란 아래 화살표 두 개 = 느려짐 (검은 테두리 3px)
                float ax = x + w / 2 + 10, ay = by - 6;
                for (int i = 0; i < 2; i++)
                {
                    float dy = i * 8;
                    mobOver.FillTriangle(new Vector2(ax - 10.5f, ay + dy - 1.5f), new Vector2(ax + 10.5f, ay + dy - 1.5f), new Vector2(ax, ay + dy + 10.5f), Rgba.Of(0, 0, 0, 0.75));
                    mobOver.FillTriangle(new Vector2(ax - 8, ay + dy), new Vector2(ax + 8, ay + dy), new Vector2(ax, ay + dy + 8), Rgba.Hex(0x8fd8ff));
                }
            }
        }

        /// <summary>몹 종류별 표식: 보스=붉은 오라, 탱커=방패 링, 빠름=잔상 선, 카드 몹=발밑 붉은 고리(+신관 회복 범위)</summary>
        static void DrawMobAura(Mob m, double t, ShapeBatch b)
        {
            float s = (float)m.Size, x = (float)m.X, y = (float)m.Y;
            if (m.Boss)
            {
                float r = s * 0.55f + (float)Math.Sin(t * 4) * 3;
                b.RadialRing(x, y, r * 0.3f, r, Rgba.Of(255, 60, 60, 0), Rgba.Of(255, 60, 60, 0.35));
            }
            else if (m.Kind == MobKind.Tank)
            {
                b.StrokeCircle(x, y + 2, s * 0.42f, 3, Rgba.Of(180, 200, 230, 0.7));
            }
            else if (m.Kind == MobKind.Fast && m.Pause <= 0)
            {
                int dx = m.Dir == 1 ? 1 : m.Dir == 2 ? -1 : 0;
                int dy = m.Dir == 0 ? -1 : m.Dir == 3 ? 1 : 0;
                Color32 c = Rgba.Of(255, 255, 255, 0.45);
                for (int i = -1; i <= 1; i++)
                {
                    float ox = dy != 0 ? i * 8 : 0, oy = dx != 0 ? i * 8 : 0;
                    b.Line(x + dx * 14 + ox, y + dy * 14 + oy, x + dx * 28 + ox, y + dy * 28 + oy, 2, c);
                }
            }
            else if (m.Kind == MobKind.Card && m.IsCard)
            {
                if (Attacks.All[m.Card].Heal > 0) b.FillCircle(x, y, 130, Rgba.Of(126, 232, 126, 0.10));
                b.StrokeEllipse(x, y + s * 0.38f, s * 0.36f, s * 0.14f, 3, Rgba.Of(255, 90, 61, 0.55 + Math.Sin(t * 6) * 0.2));
            }
        }

        // ---------------- 유닛 ----------------
        void DrawUnit(Unit u, double t)
        {
            int dir, col;
            if (u.Moving)
            {
                dir = u.Dir; // 구역 교환 등으로 이동 중: 이동 방향 + 걷기
                col = SpriteAnim.WalkCol(t, u.Id * 0.09, 0.14);
            }
            else
            {
                dir = u.Face; // 공격 중인 몹 쪽 (없으면 정면), 공격할 때만 스윙
                col = u.Flash > 0 ? SpriteAnim.AttackCol(u.Flash) : 1;
            }
            Sprite sprite = m_catalog.Unit(u.Job, u.GradeIndex)[SpriteAnim.FrameIndex(dir, col)];
            Place(Get(m_units, u.Id, UnitOrder), sprite, (float)u.X, (float)u.Y, UnitDraw, 1f, false);
        }

        /// <summary>전설 이상 발밑 등급 오라 (유닛보다 먼저)</summary>
        static void DrawUnitAuras(Game g, double t, ShapeBatch b)
        {
            foreach (Unit u in g.Units)
            {
                if (u.GradeIndex < 3) continue;
                uint color = Grades.All[u.GradeIndex].Color;
                double pulse = 0.5 + Math.Sin(t * 3 + u.Id) * 0.2;
                float x = (float)u.X, y = (float)u.Y + 22;
                b.FillEllipse(x, y, 30, 11, Rgba.Hex(color, 0.28 * pulse + 0.1));
                if (u.GradeIndex >= 4) b.StrokeEllipse(x, y, 36, 13, 2, Rgba.Hex(color, 0.7 * pulse));
            }
        }

        /// <summary>신화·태초: 몸 둘레를 도는 반짝임 (신화 2개, 태초 3개)</summary>
        static void DrawUnitSparkles(Game g, double t, ShapeBatch b)
        {
            foreach (Unit u in g.Units)
            {
                if (u.GradeIndex < 4) continue;
                int n = u.GradeIndex >= 5 ? 3 : 2;
                uint color = Grades.All[u.GradeIndex].Color;
                for (int i = 0; i < n; i++)
                {
                    double a = t * 1.6 + u.Id + i * Math.PI * 2 / n;
                    float x = (float)(u.X + Math.Cos(a) * 26), y = (float)(u.Y - 6 + Math.Sin(a) * 10);
                    double tw = 0.5 + 0.5 * Math.Sin(t * 6 + i * 2 + u.Id); // 깜빡임
                    float r = (float)(2 + tw * 4);
                    Color32 white = Rgba.Of(255, 255, 255, 0.55 + tw * 0.45);
                    b.FillRect(x - 1, y - r, 2, r * 2, white);
                    b.FillRect(x - r, y - 1, r * 2, 2, white);
                    b.FillRect(x - 1.5f, y - 1.5f, 3, 3, Rgba.Hex(color, 0.95));
                }
            }
        }

        // ---------------- 풀 ----------------
        Actor Get(Dictionary<int, Actor> map, int id, int order)
        {
            if (!map.TryGetValue(id, out Actor a))
            {
                a = m_free.Count > 0 ? m_free.Pop() : Create();
                a.Go.SetActive(true);
                a.Main.sortingOrder = order;
                a.Flash.sortingOrder = order;
                map.Add(id, a);
            }
            a.Stamp = m_stamp;
            return a;
        }

        Actor Create()
        {
            var go = new GameObject("Actor");
            go.transform.SetParent(transform, false);
            var main = go.AddComponent<SpriteRenderer>();
            main.sharedMaterial = m_spriteMat;
            var flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(go.transform, false);
            var flash = flashGo.AddComponent<SpriteRenderer>();
            flash.sharedMaterial = m_flashMat;
            flash.color = new Color(1, 1, 1, 0.35f);
            return new Actor { Go = go, Main = main, Flash = flash };
        }

        static void Place(Actor a, Sprite sprite, float x, float y, float size, float alpha, bool flash)
        {
            a.Main.sprite = sprite;
            Color c = a.Main.color;
            if (c.a != alpha) a.Main.color = new Color(1, 1, 1, alpha);
            Transform tr = a.Go.transform;
            tr.localPosition = BoardSpace.ToWorld(x, y);
            float scale = size / Cell;
            if (tr.localScale.x != scale) tr.localScale = new Vector3(scale, scale, 1);
            if (a.Flash.enabled != flash) a.Flash.enabled = flash;
            if (flash) a.Flash.sprite = sprite;
        }

        void Release(Dictionary<int, Actor> map, int stamp)
        {
            m_gone.Clear();
            foreach (KeyValuePair<int, Actor> kv in map)
                if (kv.Value.Stamp != stamp) m_gone.Add(kv.Key);
            foreach (int id in m_gone)
            {
                Actor a = map[id];
                map.Remove(id);
                a.Go.SetActive(false);
                a.Flash.enabled = false;
                m_free.Push(a);
            }
        }
    }
}

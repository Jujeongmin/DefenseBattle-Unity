using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 이펙트 11종 + 화면 흔들림 + 보스 체력 바 (render.ts drawEffects / drawProj / drawBurst / drawDrop / drawBossBar).
    /// 도형은 ShapeBatch(over = 이펙트, top = 보스바), 글자는 FloatingTextPool, 떨어지는 몹(PvP)은 스프라이트 풀.
    /// 값·색은 원본 그대로. 엔진 이펙트 객체는 재사용되므로 매 프레임 Game.Effects 를 다시 읽는다.
    /// </summary>
    public sealed class EffectsView : MonoBehaviour
    {
        const float TwoPi = Mathf.PI * 2;

        SpriteCatalog m_catalog;
        Material m_spriteMat;
        FloatingTextPool m_texts;
        /// <summary>흔들 전장 루트 (UI 는 안 흔듦)</summary>
        Transform m_shakeRoot;
        readonly List<SpriteRenderer> m_drops = new List<SpriteRenderer>(8);
        int m_dropsUsed;
        readonly char[] m_buf = new char[64];

        public void Init(SpriteCatalog catalog, Material spriteMaterial, FloatingTextPool texts, Transform shakeRoot)
        {
            m_catalog = catalog;
            m_spriteMat = spriteMaterial;
            m_texts = texts;
            m_shakeRoot = shakeRoot;
        }

        public void Draw(Game g, double t, ShapeBatch over, ShapeBatch top)
        {
            Shake(g);
            m_dropsUsed = 0;
            foreach (Effect e in g.Effects)
            {
                switch (e.Type)
                {
                    case EffectType.Proj: DrawProj(e, over); break;
                    case EffectType.Pop: DrawPop(e, over); break;
                    case EffectType.Splash: DrawSplash(e, over); break;
                    case EffectType.Slash: DrawSlash(e, over); break;
                    case EffectType.Merge: DrawMerge(e, over); break;
                    case EffectType.Summon: DrawSummon(e, over); break;
                    case EffectType.Burst: DrawBurst(e, over); break;
                    case EffectType.Drop: DrawDrop(e, t, over); break;
                    case EffectType.Beam: DrawBeam(e, over); break;
                    case EffectType.Dmg: DrawDmg(e); break;
                    case EffectType.Gold: DrawGold(e); break;
                }
            }
            for (int i = m_dropsUsed; i < m_drops.Count; i++)
                if (m_drops[i].enabled) m_drops[i].enabled = false;
            DrawBossBar(g, top);
        }

        void Shake(Game g)
        {
            if (m_shakeRoot == null) return;
            double sh = g.Shake;
            if (sh > 0.2)
            {
                float dx = (UnityEngine.Random.value - 0.5f) * (float)sh, dy = (UnityEngine.Random.value - 0.5f) * (float)sh;
                m_shakeRoot.localPosition = new Vector3(dx * BoardSpace.Px, -dy * BoardSpace.Px, 0);
            }
            else if (m_shakeRoot.localPosition != Vector3.zero)
            {
                m_shakeRoot.localPosition = Vector3.zero;
            }
        }

        // ---------------- 투사체: 궁수=화살, 마법사=마력구, 전사=검기 ----------------
        static void DrawProj(Effect e, ShapeBatch b)
        {
            float p = (float)(1 - e.Ttl / e.Dur);
            float x = (float)(e.X1 + (e.X2 - e.X1) * p), y = (float)(e.Y1 + (e.Y2 - e.Y1) * p);
            float ang = Mathf.Atan2((float)(e.Y2 - e.Y1), (float)(e.X2 - e.X1));
            uint color = e.Grade == 0 ? Units.Get(e.Job).Tint : Grades.All[Mathf.Clamp(e.Grade, 0, Grades.Count - 1)].Color;
            BaseJob kind = Units.Get(e.Job).Base;
            float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
            if (kind == BaseJob.Archer)
            {
                LineLocal(b, x, y, c, s, -22, 0, -6, 0, 3, Rgba.Hex(color, 0.5)); // 궤적
                LineLocal(b, x, y, c, s, -10, 0, 6, 0, 2, Rgba.Hex(0xf5e6c8));
                b.FillTriangle(Rot(x, y, c, s, 9, 0), Rot(x, y, c, s, 3, -3), Rot(x, y, c, s, 3, 3), Rgba.Hex(0xdddddd));
            }
            else if (kind == BaseJob.Wizard)
            {
                float r = (e.Job == Job.Bomber ? 8 : 5) + e.Grade * 0.8f;
                float outer = r * 2.2f;
                Color32 mid = Rgba.Hex(color, 0.85);
                b.RadialCircle(x, y, outer * 0.35f, Rgba.Of(255, 255, 255, 0.95), mid, 0f);
                b.RadialRing(x, y, outer * 0.35f, outer, mid, Rgba.Of(142, 91, 216, 0));
            }
            else
            {
                Vector2 center = Rot(x, y, c, s, -6, 0);
                b.StrokeArc(center.x, center.y, 12, ang - 1.1f, ang + 1.1f, 4, Rgba.Hex(color, 0.9));
            }
        }

        static Vector2 Rot(float x, float y, float c, float s, float lx, float ly) => new Vector2(x + lx * c - ly * s, y + lx * s + ly * c);

        static void LineLocal(ShapeBatch b, float x, float y, float c, float s, float x1, float y1, float x2, float y2, float w, Color32 col)
        {
            Vector2 a = Rot(x, y, c, s, x1, y1), z = Rot(x, y, c, s, x2, y2);
            b.Line(a.x, a.y, z.x, z.y, w, col);
        }

        // ---------------- 처치·타격 ----------------
        static void DrawPop(Effect e, ShapeBatch b)
        {
            float life = e.Boss ? 0.8f : 0.45f;
            float p = 1 - (float)e.Ttl / life;
            float alpha = Mathf.Max(0, 1 - p);
            float x = (float)e.X, y = (float)e.Y;
            b.StrokeCircle(x, y, p * (e.Boss ? 70 : 26) + 4, e.Boss ? 5 : 3, Rgba.Of(255, 240, 150, alpha));
            int n = e.Boss ? 14 : 6; // 파편
            Color32 c = Rgba.Of(255, 200, 90, alpha);
            for (int i = 0; i < n; i++)
            {
                float ang = (float)e.Seed + (float)i / n * TwoPi;
                float d = 6 + p * (e.Boss ? 80 : 30);
                b.FillRect(x + Mathf.Cos(ang) * d - 2, y + Mathf.Sin(ang) * d - 2 + p * p * 12, 4, 4, c);
            }
        }

        static void DrawSplash(Effect e, ShapeBatch b)
        {
            float p = 1 - (float)e.Ttl / 0.35f;
            float alpha = Mathf.Max(0, 1 - p);
            float x = (float)e.X, y = (float)e.Y, r = (float)e.R;
            b.RadialCircle(x, y, r * (0.5f + p * 0.5f), Rgba.Hex(e.Color, 0.45 * alpha), Rgba.Hex(e.Color, 0), 0f);
            b.StrokeCircle(x, y, r * (0.4f + p * 0.6f), 2, Rgba.Hex(e.Color, alpha));
        }

        static void DrawSlash(Effect e, ShapeBatch b)
        {
            float p = 1 - (float)e.Ttl / 0.25f;
            float seed = (float)e.Seed;
            b.StrokeArc((float)e.X, (float)e.Y, 22, seed - 0.9f + p * 0.6f, seed + 0.9f + p * 0.6f, 5 * (1 - p) + 1, Rgba.Of(255, 240, 200, 1 - p));
        }

        static void DrawMerge(Effect e, ShapeBatch b)
        {
            float p = 1 - (float)e.Ttl / 0.7f;
            float x = (float)e.X, y = (float)e.Y;
            for (int k = 0; k < 2; k++) b.StrokeCircle(x, y, 10 + p * (50 + k * 25), 4, Rgba.Hex(e.Color, 1 - p));
            Color32 dot = Rgba.Hex(e.Color, (1 - p) * 0.8);
            for (int i = 0; i < 8; i++)
            {
                float ang = i / 8f * TwoPi + p * 2;
                float d = 40 * (1 - p);
                b.FillCircle(x + Mathf.Cos(ang) * d, y + Mathf.Sin(ang) * d, 3, dot);
            }
        }

        static void DrawSummon(Effect e, ShapeBatch b)
        {
            float p = 1 - (float)e.Ttl / 0.5f;
            float x = (float)e.X, y = (float)e.Y;
            float h = (e.Big ? 120 : 70) * (1 - p * 0.5f); // 기둥 빛
            float w = e.Big ? 36 : 24;
            b.VerticalGradientRect(x - w / 2, y - h, w, h + 16, Rgba.Hex(e.Color, 0), Rgba.Hex(e.Color, 0.55 * (1 - p)));
            b.StrokeEllipse(x, y + 16, 10 + p * 22, 4 + p * 8, 2, Rgba.Hex(e.Color, 1 - p));
        }

        /// <summary>태초 등급 타격 폭발: 확장 링 2겹 + 방사형 파편</summary>
        static void DrawBurst(Effect e, ShapeBatch b)
        {
            float p = 1 - (float)e.Ttl / 0.45f;
            float alpha = Mathf.Max(0, 1 - p);
            float x = (float)e.X, y = (float)e.Y;
            float r = 8 + p * 46;
            b.StrokeCircle(x, y, r, 4 * (1 - p) + 1, Rgba.Of(255, 110, 40, alpha));
            b.StrokeCircle(x, y, r * 0.55f, 2.5f * (1 - p) + 0.5f, Rgba.Of(255, 230, 120, alpha));
            if (p < 0.35f) b.FillCircle(x, y, 10 * (1 - p / 0.35f) + 2, Rgba.Of(255, 255, 220, (1 - p / 0.35f) * 0.9));
            Color32 c = Rgba.Of(255, 170, 60, alpha);
            for (int i = 0; i < 6; i++)
            {
                float ang = (float)e.Seed + i / 6f * TwoPi;
                float d = 10 + p * 40;
                b.FillCircle(x + Mathf.Cos(ang) * d, y + Mathf.Sin(ang) * d, 3.5f * (1 - p) + 0.5f, c);
            }
        }

        static void DrawBeam(Effect e, ShapeBatch b)
        {
            float k = Mathf.Max(0, (float)(e.Ttl / e.Dur));
            float x1 = (float)e.X1, y1 = (float)e.Y1, x2 = (float)e.X2, y2 = (float)e.Y2, w = (float)e.W;
            b.Line(x1, y1, x2, y2, w, Rgba.Hex(e.Color, k));
            b.Line(x1, y1, x2, y2, Mathf.Max(1, w * 0.35f), Rgba.Of(255, 255, 255, 0.8 * k));
        }

        /// <summary>상대가 던진 몹: 착지 지점 붉은 경고 고리, 하늘에서 포물선으로 떨어진다</summary>
        void DrawDrop(Effect e, double t, ShapeBatch b)
        {
            float k = 1 - (float)(e.Ttl / e.Dur); // 0 → 1 (착지)
            float x = (float)e.X, y = (float)e.Y;
            float r = 26 + (1 - k) * 34; // 점점 좁아짐
            b.StrokeEllipse(x, y + 10, r, r * 0.45f, 4, Rgba.Of(255, 70, 50, 0.5 + Math.Sin(t * 18) * 0.3));
            b.FillEllipse(x, y + 10, 8 + k * 16, 3 + k * 6, Rgba.Of(0, 0, 0, 0.15 + k * 0.25));
            if (e.Card < 0 || e.Card >= Attacks.All.Length) return;
            AttackDef a = Attacks.All[e.Card];
            float size = 60 * (float)a.Size * (0.7f + k * 0.3f);
            float h = (1 - k) * (1 - k) * 520;
            float sx = x + (1 - k) * ((float)e.Seed - 0.5f) * 160;
            float rot = (1 - k) * (e.Seed > 0.5 ? 1 : -1) * 3; // 라디안, 캔버스 기준(시계 방향)
            SpriteRenderer sr = NextDrop();
            sr.sprite = m_catalog.Mob(a.Race, a.Variant)[SpriteAnim.FrameIndex(0, 1)];
            Transform tr = sr.transform;
            tr.localPosition = BoardSpace.ToWorld(sx, y - h);
            tr.localRotation = Quaternion.Euler(0, 0, -rot * Mathf.Rad2Deg); // 월드는 y 가 위라 방향이 반대
            float scale = size / 32f;
            tr.localScale = new Vector3(scale, scale, 1);
        }

        SpriteRenderer NextDrop()
        {
            if (m_dropsUsed < m_drops.Count)
            {
                SpriteRenderer r = m_drops[m_dropsUsed++];
                if (!r.enabled) r.enabled = true;
                return r;
            }
            var go = new GameObject("Drop");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = m_spriteMat;
            sr.sortingOrder = 35;
            m_drops.Add(sr);
            m_dropsUsed++;
            return sr;
        }

        // ---------------- 숫자 ----------------
        void DrawDmg(Effect e)
        {
            if (m_texts == null) return;
            float p = 1 - (float)e.Ttl / 0.7f;
            float alpha = Mathf.Clamp01((float)e.Ttl / 0.7f * 1.6f);
            float size = e.Crit ? 24 + (p < 0.15f ? (0.15f - p) * 60 : 0) : 17;
            int n = ShortNumber(e.Value, m_buf, 0);
            if (e.Crit) n = CharText.Put(m_buf, n, '!');
            m_texts.Show(m_buf, n, (float)e.X, (float)e.Y - p * 28, size, Rgba.Hex(e.Crit ? 0xff6b3du : 0xffe066u, alpha));
        }

        void DrawGold(Effect e)
        {
            if (m_texts == null) return;
            float p = 1 - (float)e.Ttl / 0.8f;
            float alpha = Mathf.Max(0, 1 - p * p);
            int n = CharText.Put(m_buf, 0, '+');
            n = CharText.PutInt(m_buf, n, (long)e.Value);
            n = CharText.Put(m_buf, n, "<sprite name=\"coin\">");
            m_texts.Show(m_buf, n, (float)e.X, (float)e.Y - 14 - p * 22, 15, Rgba.Hex(0xffcf3f, alpha));
        }

        /// <summary>큰 수 축약 (render.ts short: 12.3K, 4.5M). buf[start..] 에 쓰고 끝 위치를 돌려준다</summary>
        public static int ShortNumber(double v, char[] buf, int start)
        {
            if (v >= 1e6) return CharText.Put(buf, CharText.PutFixed1(buf, start, v / 1e6), 'M');
            if (v >= 1e4) return CharText.Put(buf, CharText.PutFixed1(buf, start, v / 1e3), 'K');
            return CharText.PutInt(buf, start, (long)Math.Round(v));
        }

        // ---------------- 보스 체력 바 ----------------
        void DrawBossBar(Game g, ShapeBatch top)
        {
            Mob boss = null;
            foreach (Mob m in g.Mobs)
                if (m.Boss) { boss = m; break; }
            if (boss == null) return;
            const float W = 720 * 0.62f, H = 16, X = (720 - W) / 2, Y = 14;
            float ratio = Mathf.Max(0, (float)(boss.Hp / boss.MaxHp));
            top.FillRect(X - 3, Y - 3, W + 6, H + 6, Rgba.Of(0, 0, 0, 0.65));
            Color32 left = Rgba.Hex(0xff3b3b), right = Rgba.Hex(0xff9f43);
            top.HorizontalGradientRect(X, Y, W * ratio, H, left, Color32.Lerp(left, right, ratio));
            if (m_texts == null) return;
            int n = CharText.Put(m_buf, 0, "BOSS  ");
            n = CharText.PutInt(m_buf, n, (long)Math.Ceiling(ratio * 100));
            n = CharText.Put(m_buf, n, "%   ");
            n = CharText.PutInt(m_buf, n, (long)Math.Round(Math.Max(0, g.BossTimer / Waves.PlaySpeed)));
            n = CharText.Put(m_buf, n, 's');
            m_texts.Show(m_buf, n, 360, Y + H / 2 + 5, 13, Rgba.Hex(0xffffff));
        }
    }
}

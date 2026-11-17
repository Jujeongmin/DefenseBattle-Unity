using System.Collections.Generic;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 상대 전장 그리기 (oppRender.ts OppRenderer): 받은 스냅샷을 SnapshotTimeline 으로 보간해
    /// 바닥·빈틈·유닛·몹·체력 막대·조준/던진 자리 표시를 그린다.
    /// 내 전장과 멀리 떨어진 곳(월드 x = Offset)에 그려 전용 카메라가 RenderTexture 에 찍는다 → HUD 의 RawImage 가 보여 준다.
    /// 스프라이트는 풀에서 꺼내 쓰고, 매 프레임 할당이 없다. RenderTexture 는 한 번 만들어 재사용하고 지울 때 해제한다.
    /// </summary>
    public sealed class OppBoardView : MonoBehaviour
    {
        const float Offset = 1000f;
        const int TexSize = 384;
        const float UnitDraw = 62, Cell = 32;
        const int GroundOrder = -100, MobOrder = 10, UnitOrder = 20;
        static readonly Color32 GapRed = new Color32(255, 50, 40, 80), Shadow = new Color32(0, 0, 0, 64), BarBg = new Color32(0, 0, 0, 166),
            HpRed = new Color32(0xff, 0x52, 0x52, 255), HpSent = new Color32(0xff, 0x8a, 0x3d, 255), SentRing = new Color32(0xff, 0x5a, 0x3d, 255),
            BossRing = new Color32(0xff, 0xd5, 0x4f, 255), Aim = new Color32(255, 255, 255, 230), ThrowRing = new Color32(0xff, 0x8a, 0x3d, 255);

        SpriteCatalog m_cat;
        Material m_spriteMat;
        Camera m_cam;
        ShapeBatch m_under, m_over;
        readonly List<SpriteRenderer> m_mobs = new List<SpriteRenderer>(64);
        readonly List<SpriteRenderer> m_units = new List<SpriteRenderer>(32);
        readonly SnapshotTimeline m_timeline = new SnapshotTimeline();
        readonly OppFrame m_frame = new OppFrame();
        static Vector2[] s_gapPoints;
        IPvpOpponent m_opp;
        double m_aim = -1;

        struct ThrowMark { public double Pos; public float At; }
        readonly ThrowMark[] m_throws = new ThrowMark[8];
        int m_throwN;

        public RenderTexture Texture { get; private set; }

        public static OppBoardView Create(SpriteCatalog cat, Material spriteMat, Material shapeMat)
        {
            var go = new GameObject("OppBoard");
            go.transform.position = new Vector3(Offset, 0, 0);
            var v = go.AddComponent<OppBoardView>();
            v.Init(cat, spriteMat, shapeMat);
            go.SetActive(false);
            return v;
        }

        void Init(SpriteCatalog cat, Material spriteMat, Material shapeMat)
        {
            m_cat = cat;
            m_spriteMat = spriteMat;
            var ground = new GameObject("Ground").AddComponent<SpriteRenderer>();
            ground.transform.SetParent(transform, false);
            ground.sprite = cat.Field;
            ground.sharedMaterial = spriteMat;
            ground.sortingOrder = GroundOrder;
            m_under = Batch("Under", shapeMat, MobOrder - 1);
            m_over = Batch("Over", shapeMat, UnitOrder + 2000);

            Texture = new RenderTexture(TexSize, TexSize, 0, RenderTextureFormat.ARGB32) { name = "OppBoard", filterMode = FilterMode.Point };
            Texture.Create();
            m_cam = new GameObject("OppCamera").AddComponent<Camera>();
            m_cam.transform.SetParent(transform, false);
            m_cam.transform.localPosition = new Vector3(0, 0, -10);
            m_cam.orthographic = true;
            m_cam.orthographicSize = BoardSpace.WorldSize / 2f;
            m_cam.clearFlags = CameraClearFlags.SolidColor;
            m_cam.backgroundColor = new Color32(0x6c, 0x9a, 0x3a, 255);
            m_cam.targetTexture = Texture;

            if (s_gapPoints == null)
            {
                s_gapPoints = new Vector2[BoardSnapshot.GapSamples];
                for (int i = 0; i < s_gapPoints.Length; i++)
                {
                    Vec2 p = Board.PathAt(i / (double)BoardSnapshot.GapSamples);
                    s_gapPoints[i] = new Vector2((float)p.X, (float)p.Y);
                }
            }
        }

        ShapeBatch Batch(string name, Material mat, int order)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            var b = go.AddComponent<ShapeBatch>();
            b.Configure(mat, "Default", order);
            return b;
        }

        void OnDestroy()
        {
            if (Texture == null) return;
            if (m_cam != null) m_cam.targetTexture = null;
            Texture.Release();
            Destroy(Texture);
            Texture = null;
        }

        /// <summary>대전 시작: 상대 스냅샷을 받기 시작한다</summary>
        public void Begin(IPvpOpponent opp)
        {
            End();
            m_opp = opp;
            opp.Snapshot += OnSnapshot;
            m_timeline.Clear();
            m_aim = -1;
            m_throwN = 0;
            gameObject.SetActive(true);
        }

        public void End()
        {
            if (m_opp != null) m_opp.Snapshot -= OnSnapshot;
            m_opp = null;
            gameObject.SetActive(false);
        }

        void OnSnapshot(BoardSnapshot s) => m_timeline.Push(s, Time.timeAsDouble);

        /// <summary>끄는 중인 조준 위치 (길 둘레 비율, 없으면 null)</summary>
        public void SetAim(double? pos) => m_aim = pos ?? -1;

        /// <summary>방금 던진 자리 (상대 쪽 결과가 보이기 전까지 잠깐 표시)</summary>
        public void MarkThrow(double pos)
        {
            if (m_throwN == m_throws.Length) { System.Array.Copy(m_throws, 1, m_throws, 0, m_throws.Length - 1); m_throwN--; }
            m_throws[m_throwN++] = new ThrowMark { Pos = pos, At = Time.time };
        }

        void LateUpdate()
        {
            double now = Time.timeAsDouble;
            float t = (float)(now * Waves.PlaySpeed);
            m_under.Begin();
            m_over.Begin();
            int mobs = 0, units = 0;
            if (m_timeline.Sample(now, m_frame))
            {
                OppFrame f = m_frame;
                // 사거리 빈틈 (미니맵에서도 보이게 조금 크게)
                Color32 gap = GapRed;
                gap.a = (byte)(80 + Mathf.Sin(t * 4) * 20);
                for (int i = 0; i < f.Gap.Length; i++)
                    if (f.Gap[i]) m_under.FillRect(s_gapPoints[i].x - 15, s_gapPoints[i].y - 15, 30, 30, gap);
                // 전설 이상 유닛 발밑 오라
                for (int i = 0; i < f.UnitCount; i++)
                {
                    if (f.UnitGrade[i] < 3 || f.UnitGrade[i] >= Grades.Count) continue;
                    m_under.FillEllipse(f.UnitX[i], f.UnitY[i] + 22, 30, 11, Rgba.Hex(Grades.All[f.UnitGrade[i]].Color, 0.35));
                }
                for (int i = 0; i < f.UnitCount; i++)
                {
                    int g = Mathf.Clamp(f.UnitGrade[i], 0, Grades.Count - 1);
                    Sprite[] frames = m_cat.Unit((Job)Mathf.Clamp(f.UnitJob[i], 0, Units.All.Length - 1), g);
                    int dir = f.UnitFace[i] & 3;
                    int col = (f.UnitFace[i] & 4) != 0 ? ((int)(t * 8 + i) % 2 == 0 ? 2 : 0) : 1; // 공격 중이면 휘두르기
                    Place(Get(m_units, units++, UnitOrder), frames[SpriteAnim.FrameIndex(dir, col)], f.UnitX[i], f.UnitY[i], UnitDraw, 1f, UnitOrder + (int)f.UnitY[i]);
                }
                for (int i = 0; i < f.MobCount; i++)
                {
                    int code = f.MobCode[i];
                    float x = f.MobX[i], y = f.MobY[i], s = f.MobSize[i];
                    int st = f.MobStatus[i];
                    m_under.FillEllipse(x, y + s * 0.38f, s * 0.32f, s * 0.12f, Shadow);
                    Sprite[] frames = m_cat.Mob((Race)OppFrame.Race(code), OppFrame.Variant(code));
                    int col = (st & BoardSnapshot.StatusStop) != 0 ? 1 : SpriteAnim.WalkCol(t, f.MobId[i] * 0.07, (st & BoardSnapshot.StatusSlow) != 0 ? 0.28 : 0.16);
                    float alpha = (st & BoardSnapshot.StatusHidden) != 0 ? 0.22f : (st & BoardSnapshot.StatusGhost) != 0 ? 0.6f : 1f;
                    Place(Get(m_mobs, mobs++, MobOrder), frames[SpriteAnim.FrameIndex(OppFrame.Dir(code), col)], x, y, s, alpha, MobOrder + (int)y);
                    int kind = OppFrame.Kind(code);
                    if (kind != BoardSnapshot.KindNormal) m_over.StrokeCircle(x, y, s * 0.42f, 4, kind == BoardSnapshot.KindBoss ? BossRing : SentRing);
                    // 체력 막대 (미니맵이 작아도 보이게 두껍게)
                    float w = Mathf.Max(28, s * 0.8f), bh = 8, by = y - s / 2 - bh - 2;
                    m_over.FillRect(x - w / 2 - 1, by - 1, w + 2, bh + 2, BarBg);
                    m_over.FillRect(x - w / 2, by, w * f.MobHp[i] / 100f, bh, kind == BoardSnapshot.KindSent ? HpSent : HpRed);
                }
            }
            for (int i = units; i < m_units.Count; i++) if (m_units[i].enabled) m_units[i].enabled = false;
            for (int i = mobs; i < m_mobs.Count; i++) if (m_mobs[i].enabled) m_mobs[i].enabled = false;
            DrawMarks(t);
            m_under.End();
            m_over.End();
        }

        void DrawMarks(float t)
        {
            if (m_aim >= 0)
            {
                Vec2 p = Board.PathAt(m_aim);
                float r = 40 + Mathf.Sin(t * 6) * 6;
                m_over.StrokeCircle((float)p.X, (float)p.Y, r, 6, Aim);
                m_over.FillCircle((float)p.X, (float)p.Y, 8, Aim);
            }
            float now = Time.time;
            int w = 0;
            for (int i = 0; i < m_throwN; i++)
            {
                float age = now - m_throws[i].At;
                if (age > 1.2f) continue;
                m_throws[w++] = m_throws[i];
                Vec2 p = Board.PathAt(m_throws[i].Pos);
                Color32 c = ThrowRing;
                c.a = (byte)(255 * (1 - age / 1.2f));
                m_over.StrokeCircle((float)p.X, (float)p.Y, 24 + age * 60, 6, c);
            }
            m_throwN = w;
        }

        SpriteRenderer Get(List<SpriteRenderer> pool, int i, int order)
        {
            if (i < pool.Count) return pool[i];
            var go = new GameObject(order == MobOrder ? "Mob" : "Unit");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = m_spriteMat;
            pool.Add(r);
            return r;
        }

        static void Place(SpriteRenderer r, Sprite sprite, float x, float y, float size, float alpha, int order)
        {
            if (!r.enabled) r.enabled = true;
            r.sprite = sprite;
            if (r.sortingOrder != order) r.sortingOrder = order;
            if (r.color.a != alpha) r.color = new Color(1, 1, 1, alpha);
            Transform tr = r.transform;
            tr.localPosition = BoardSpace.ToWorld(x, y);
            float scale = size / Cell;
            if (tr.localScale.x != scale) tr.localScale = new Vector3(scale, scale, 1);
        }
    }
}

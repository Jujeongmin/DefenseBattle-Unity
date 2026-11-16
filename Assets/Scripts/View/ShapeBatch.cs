using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DefenseBattle.View
{
    /// <summary>
    /// 원본 render.ts 의 캔버스 도형(원·고리·타원·사각·선·호·삼각·그라데이션)을 그리는 동적 메시.
    /// 매 프레임 Begin() → 도형들 → End() 로 다시 채운다. 버퍼는 재사용해 할당이 없다.
    /// 좌표·크기는 모두 논리 px (720 전장, y 아래로) - 월드 변환은 여기서 한다.
    /// 같은 층에서는 부르는 순서대로 겹친다 (캔버스와 같음).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ShapeBatch : MonoBehaviour
    {
        [SerializeField] Material m_material;
        [SerializeField] string m_sortingLayer = "Default";
        [SerializeField] int m_sortingOrder;

        const int MinSegments = 12, MaxSegments = 48;
        const float Half = BoardSpace.WorldSize / 2f;
        const float Px = BoardSpace.Px;

        readonly List<Vector3> m_verts = new List<Vector3>(4096);
        readonly List<Color32> m_cols = new List<Color32>(4096);
        readonly List<int> m_tris = new List<int>(8192);
        Mesh m_mesh;

        /// <summary>반지름별 원 둘레 단위 벡터 (분할 수마다 한 번만 만든다)</summary>
        static readonly Vector2[][] s_unit = new Vector2[MaxSegments + 1][];

        public int VertexCount => m_verts.Count;

        /// <summary>반지름(px)에 따른 원 분할 수 12~48</summary>
        public static int Segments(float r) => Mathf.Clamp(Mathf.CeilToInt(r * 0.6f), MinSegments, MaxSegments);

        public void Configure(Material material, string sortingLayer, int sortingOrder)
        {
            m_material = material;
            m_sortingLayer = sortingLayer;
            m_sortingOrder = sortingOrder;
            m_mesh = null;
            EnsureInit();
        }

        void EnsureInit()
        {
            if (m_mesh != null) return;
            m_mesh = new Mesh { name = "ShapeBatch", indexFormat = IndexFormat.UInt32 };
            m_mesh.MarkDynamic();
            // 전장 전체를 덮는 고정 경계 (매 프레임 다시 계산하지 않는다)
            m_mesh.bounds = new Bounds(Vector3.zero, new Vector3(BoardSpace.WorldSize * 2, BoardSpace.WorldSize * 2, 1));
            GetComponent<MeshFilter>().sharedMesh = m_mesh;
            var r = GetComponent<MeshRenderer>();
            if (m_material != null) r.sharedMaterial = m_material;
            r.sortingLayerName = m_sortingLayer;
            r.sortingOrder = m_sortingOrder;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (m_mesh == null) return;
            if (Application.isPlaying) Destroy(m_mesh);
            else DestroyImmediate(m_mesh);
        }

        public void Begin()
        {
            EnsureInit();
            m_verts.Clear();
            m_cols.Clear();
            m_tris.Clear();
        }

        public void End()
        {
            EnsureInit();
            m_mesh.Clear(true);
            m_mesh.SetVertices(m_verts);
            m_mesh.SetColors(m_cols);
            m_mesh.SetTriangles(m_tris, 0, false);
        }

        // ---------------- 도형 ----------------
        public void FillCircle(float x, float y, float r, Color32 c) => FillEllipse(x, y, r, r, c);

        public void FillEllipse(float x, float y, float rx, float ry, Color32 c)
        {
            Vector2[] u = Unit(Segments(Mathf.Max(rx, ry)));
            int center = Add(x, y, c);
            for (int i = 0; i < u.Length; i++) Add(x + u[i].x * rx, y + u[i].y * ry, c);
            for (int i = 0; i < u.Length; i++) Tri(center, center + 1 + i, center + 1 + (i + 1) % u.Length);
        }

        public void StrokeCircle(float x, float y, float r, float width, Color32 c) => StrokeEllipse(x, y, r, r, width, c);

        /// <summary>테두리: 선 중심이 (rx, ry) 위 (캔버스 stroke 와 같음)</summary>
        public void StrokeEllipse(float x, float y, float rx, float ry, float width, Color32 c)
        {
            Vector2[] u = Unit(Segments(Mathf.Max(rx, ry)));
            float h = width / 2f;
            int start = m_verts.Count;
            for (int i = 0; i < u.Length; i++)
            {
                Add(x + u[i].x * (rx - h), y + u[i].y * (ry - h), c);
                Add(x + u[i].x * (rx + h), y + u[i].y * (ry + h), c);
            }
            for (int i = 0; i < u.Length; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % u.Length * 2;
                Quad(a, a + 1, b + 1, b);
            }
        }

        /// <summary>호 테두리 (각도 라디안, 캔버스처럼 0 = 오른쪽, 양수 = 시계 방향 - y 가 아래로)</summary>
        public void StrokeArc(float x, float y, float r, float a0, float a1, float width, Color32 c)
        {
            int seg = Mathf.Max(2, Mathf.CeilToInt(Segments(r) * Mathf.Abs(a1 - a0) / (Mathf.PI * 2)));
            float h = width / 2f;
            int start = m_verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = a0 + (a1 - a0) * i / seg;
                float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                Add(x + cx * (r - h), y + cy * (r - h), c);
                Add(x + cx * (r + h), y + cy * (r + h), c);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2;
                Quad(a, a + 1, a + 3, a + 2);
            }
        }

        /// <summary>왼쪽 위 (x, y), 폭 w, 높이 h</summary>
        public void FillRect(float x, float y, float w, float h, Color32 c) => VerticalGradientRect(x, y, w, h, c, c);

        public void VerticalGradientRect(float x, float y, float w, float h, Color32 top, Color32 bottom)
        {
            int a = Add(x, y, top);
            Add(x + w, y, top);
            Add(x + w, y + h, bottom);
            Add(x, y + h, bottom);
            Quad(a, a + 1, a + 2, a + 3);
        }

        /// <summary>굵기 width 의 선분 (끝은 평평)</summary>
        public void Line(float x1, float y1, float x2, float y2, float width, Color32 c)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            float nx = 0, ny = 0;
            if (len > 1e-4f)
            {
                nx = -dy / len * width / 2f;
                ny = dx / len * width / 2f;
            }
            int a = Add(x1 + nx, y1 + ny, c);
            Add(x2 + nx, y2 + ny, c);
            Add(x2 - nx, y2 - ny, c);
            Add(x1 - nx, y1 - ny, c);
            Quad(a, a + 1, a + 2, a + 3);
        }

        public void FillTriangle(Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int i = Add(a.x, a.y, color);
            Add(b.x, b.y, color);
            Add(c.x, c.y, color);
            Tri(i, i + 1, i + 2);
        }

        /// <summary>방사 그라데이션 원: 중심~innerFrac·r 은 inner 색, 거기서 r 까지 outer 색으로 (createRadialGradient)</summary>
        public void RadialCircle(float x, float y, float r, Color32 inner, Color32 outer, float innerFrac)
        {
            Vector2[] u = Unit(Segments(r));
            float ri = r * Mathf.Clamp01(innerFrac);
            int center = Add(x, y, inner);
            int ring = m_verts.Count;
            for (int i = 0; i < u.Length; i++)
            {
                Add(x + u[i].x * ri, y + u[i].y * ri, inner);
                Add(x + u[i].x * r, y + u[i].y * r, outer);
            }
            for (int i = 0; i < u.Length; i++)
            {
                int a = ring + i * 2, b = ring + (i + 1) % u.Length * 2;
                Tri(center, a, b);
                Quad(a, a + 1, b + 1, b);
            }
        }

        /// <summary>고리 모양 그라데이션: 반지름 r0 에서 c0 → r1 에서 c1 (여러 색 지점 그라데이션을 이어 그릴 때)</summary>
        public void RadialRing(float x, float y, float r0, float r1, Color32 c0, Color32 c1)
        {
            Vector2[] u = Unit(Segments(r1));
            int ring = m_verts.Count;
            for (int i = 0; i < u.Length; i++)
            {
                Add(x + u[i].x * r0, y + u[i].y * r0, c0);
                Add(x + u[i].x * r1, y + u[i].y * r1, c1);
            }
            for (int i = 0; i < u.Length; i++)
            {
                int a = ring + i * 2, b = ring + (i + 1) % u.Length * 2;
                Quad(a, a + 1, b + 1, b);
            }
        }

        // ---------------- 내부 ----------------
        int Add(float x, float y, Color32 c)
        {
            m_verts.Add(new Vector3(x * Px - Half, Half - y * Px, 0f));
            m_cols.Add(c);
            return m_verts.Count - 1;
        }

        void Tri(int a, int b, int c)
        {
            m_tris.Add(a);
            m_tris.Add(b);
            m_tris.Add(c);
        }

        void Quad(int a, int b, int c, int d)
        {
            Tri(a, b, c);
            Tri(a, c, d);
        }

        static Vector2[] Unit(int segments)
        {
            Vector2[] u = s_unit[segments];
            if (u != null) return u;
            u = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 2 * i / segments;
                u[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            s_unit[segments] = u;
            return u;
        }
    }
}

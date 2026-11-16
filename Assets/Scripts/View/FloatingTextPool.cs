using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 전장 위 글자 (데미지·골드 숫자, 이름표, 보스바 문구) - 월드 TextMeshPro 풀.
    /// 매 프레임 BeginFrame → Show… → EndFrame (안 쓴 칸은 숨김). 글자는 char[] 로 받아 바뀐 때만 TMP 에 넘긴다.
    /// 캔버스 strokeText 처럼 검은 테두리 (SDF 머티리얼 Outline).
    /// </summary>
    public sealed class FloatingTextPool : MonoBehaviour
    {
        /// <summary>도형 층(최대 40) 위</summary>
        const int SortingOrder = 45;

        sealed class Item
        {
            public TextMeshPro Text;
            public readonly char[] Chars = new char[64];
            public int Len = -1;
            public Color32 Color;
            public float Size = -1;
        }

        readonly List<Item> m_items = new List<Item>(64);
        TMP_FontAsset m_font;
        Material m_material;
        int m_used;

        /// <summary>이번 프레임에 쓴 칸 수</summary>
        public int ActiveCount => m_used;

        public void Init(TMP_FontAsset font)
        {
            m_font = font;
            m_material = new Material(font.material) { name = "FloatingText" };
            m_material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m_material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            m_material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0, 0, 0, 0.75f));
            m_material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
        }

        void OnDestroy()
        {
            if (m_material == null) return;
            if (Application.isPlaying) Destroy(m_material);
            else DestroyImmediate(m_material);
        }

        public void BeginFrame() => m_used = 0;

        /// <summary>
        /// 글자 한 칸: (x, y) = 논리 px, 가로 가운데·글자 기준선 (캔버스 textAlign center, alphabetic). sizePx = 캔버스 글꼴 px
        /// </summary>
        public void Show(char[] buf, int len, float x, float y, float sizePx, Color32 color)
        {
            Item it = m_used < m_items.Count ? m_items[m_used] : Create();
            m_used++;
            if (!it.Text.gameObject.activeSelf) it.Text.gameObject.SetActive(true);
            if (!Same(it, buf, len))
            {
                System.Array.Copy(buf, it.Chars, len);
                it.Len = len;
                it.Text.SetCharArray(it.Chars, 0, len);
            }
            if (it.Size != sizePx)
            {
                it.Size = sizePx;
                it.Text.fontSize = sizePx * BoardSpace.Px * 10f; // TMP 월드 글자: fontSize 10 = 1 유닛
            }
            if (!it.Color.Equals(color))
            {
                it.Color = color;
                it.Text.color = color;
            }
            it.Text.transform.localPosition = BoardSpace.ToWorld(x, y);
        }

        public void EndFrame()
        {
            for (int i = m_used; i < m_items.Count; i++)
                if (m_items[i].Text.gameObject.activeSelf) m_items[i].Text.gameObject.SetActive(false);
        }

        /// <summary>테스트·디버그용</summary>
        public string TextAt(int i) => new string(m_items[i].Chars, 0, m_items[i].Len);

        static bool Same(Item it, char[] buf, int len)
        {
            if (it.Len != len) return false;
            for (int i = 0; i < len; i++) if (it.Chars[i] != buf[i]) return false;
            return true;
        }

        Item Create()
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = m_font;
            t.fontSharedMaterial = m_material;
            t.alignment = TextAlignmentOptions.Baseline;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(4, 1);
            t.sortingOrder = SortingOrder;
            var it = new Item { Text = t };
            m_items.Add(it);
            return it;
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 던진 몹이 카드에서 상대 미니맵 착지점까지 포물선으로 날아간다 (ui.ts flyThrow: 0.38초, 반쯤에서 1.4배·200도, 끝에서 0.7배·400도).
    /// 이미지 몇 장을 돌려 쓴다. 닿으면 Landed (미니맵 흔들림).
    /// </summary>
    public sealed class ThrowFly
    {
        const float Dur = 0.38f, Size = 64;

        struct Fly
        {
            public Image Img;
            public Vector2 From, To;
            public float T;
            public bool On;
        }

        readonly RectTransform m_parent;
        readonly Fly[] m_flies = new Fly[3];

        public event Action Landed;

        public ThrowFly(RectTransform parent)
        {
            m_parent = parent;
            for (int i = 0; i < m_flies.Length; i++)
            {
                Image img = Ui.Image(parent, null, Color.white, "ThrowFly");
                img.preserveAspect = true;
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.sizeDelta = new Vector2(Size, Size);
                img.gameObject.SetActive(false);
                m_flies[i].Img = img;
            }
        }

        /// <summary>from·to = parent 가운데 기준 좌표</summary>
        public void Launch(Sprite sprite, Vector2 from, Vector2 to)
        {
            int k = 0;
            for (int i = 0; i < m_flies.Length; i++) if (!m_flies[i].On) { k = i; break; }
            ref Fly f = ref m_flies[k];
            f.From = from;
            f.To = to;
            f.T = 0;
            f.On = true;
            f.Img.sprite = sprite;
            f.Img.gameObject.SetActive(true);
            f.Img.rectTransform.SetAsLastSibling();
            Place(ref f);
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < m_flies.Length; i++)
            {
                ref Fly f = ref m_flies[i];
                if (!f.On) continue;
                f.T += dt / Dur;
                if (f.T >= 1)
                {
                    f.On = false;
                    f.Img.gameObject.SetActive(false);
                    Landed?.Invoke();
                    continue;
                }
                Place(ref f);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < m_flies.Length; i++) { m_flies[i].On = false; m_flies[i].Img.gameObject.SetActive(false); }
        }

        static void Place(ref Fly f)
        {
            float k = f.T * f.T; // ease-in
            // 가운데가 두 점 중 높은 쪽보다 90 위 (UI 는 y 위가 +)
            Vector2 mid = new Vector2((f.From.x + f.To.x) / 2, Mathf.Max(f.From.y, f.To.y) + 90);
            Vector2 p = (1 - k) * (1 - k) * f.From + 2 * (1 - k) * k * mid + k * k * f.To;
            RectTransform rt = f.Img.rectTransform;
            rt.anchoredPosition = p;
            float s = k < 0.5f ? Mathf.Lerp(1f, 1.4f, k * 2) : Mathf.Lerp(1.4f, 0.7f, (k - 0.5f) * 2);
            rt.localScale = new Vector3(s, s, 1);
            rt.localRotation = Quaternion.Euler(0, 0, -400 * k);
        }
    }
}

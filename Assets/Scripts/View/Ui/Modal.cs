using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 가운데 확인 창 (ui.ts confirmModal / alertModal): 어두운 배경 + 종이 창 + 제목 리본 + 버튼 1~2개.
    /// 열 때 만들고 닫을 때 지운다 (로비 안에서만 쓰므로 전투 중 할당과 무관).
    /// </summary>
    public sealed class Modal
    {
        readonly Transform m_host;
        GameObject m_open;

        public Modal(Transform host) => m_host = host;

        public bool IsOpen => m_open != null;

        /// <summary>확인 / 취소 (닫기·배경 탭 = 취소)</summary>
        public void Confirm(string title, string body, string ok, Action<bool> done, string okFrame = "btn_green")
            => Open(title, body, ok, Loc.Tl("취소"), okFrame, done);

        /// <summary>확인 하나</summary>
        public void Alert(string title, string body, Action done = null)
            => Open(title, body, Loc.Tl("확인"), null, "btn_blue", _ => done?.Invoke());

        /// <summary>내용을 직접 채우는 창 (fill 이 content 에 그린다). 닫기 버튼만</summary>
        public RectTransform Custom(string title, Action closed = null)
        {
            RectTransform content = Shell(title, out RectTransform buttons, () => { Close(); closed?.Invoke(); });
            UnityEngine.Object.Destroy(buttons.gameObject);
            return content;
        }

        public void Close()
        {
            if (m_open != null) UnityEngine.Object.Destroy(m_open);
            m_open = null;
        }

        void Open(string title, string body, string ok, string cancel, string okFrame, Action<bool> done)
        {
            bool answered = false;
            Action<bool> answer = v =>
            {
                if (answered) return;
                answered = true;
                Close();
                done?.Invoke(v);
            };
            RectTransform content = Shell(title, out RectTransform buttons, () => answer(false));
            TextMeshProUGUI t = Ui.Text(content, body, 28, Ui.Ink, TextAlignmentOptions.Center, true);
            Ui.Size(t.gameObject, -1, -1);
            if (cancel != null) Ui.Size(Ui.Button(buttons, "btn_gray", cancel, 30, () => answer(false)).gameObject, 220, 92);
            Ui.Size(Ui.Button(buttons, okFrame, ok, 30, () => answer(true)).gameObject, 240, 92);
        }

        /// <summary>창 틀: 배경 + 종이 + 제목 + 내용 칸 + 버튼 줄</summary>
        RectTransform Shell(string title, out RectTransform buttons, Action dismiss)
        {
            Close();
            RectTransform root = Ui.Rect(m_host, "Modal");
            Ui.Fill(root);
            m_open = root.gameObject;
            Button dim = Ui.Hit(root, dismiss, "Dim");
            dim.GetComponent<Image>().color = Ui.Dim;
            Ui.Fill(dim.GetComponent<RectTransform>());

            Image paper = Ui.Frame(root, "paper", 1.4f, "Dialog");
            paper.raycastTarget = true; // 창 안을 눌러도 닫히지 않게
            RectTransform pr = paper.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(620, 0);
            Ui.Column(paper.transform, 22, 40, 40, 76, 40);
            var fit = paper.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 제목 리본 (창 위쪽에 걸친다)
            Image ribbon = Ui.Frame(paper.transform, "ribbon_red", 1.3f, "Ribbon");
            ribbon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Ui.Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(440, 86));
            Ui.Fill(Ui.Line(ribbon.transform, title, 34, Ui.Cream).rectTransform, 30, 18, 30, 8);

            Button x = Ui.Hit(paper.transform, dismiss, "Close");
            x.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Ui.Place(x.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(64, 64));
            Ui.Fill(Ui.Icon(x.transform, "close", 40, Ui.Ink2).rectTransform, 12, 12, 12, 12);

            RectTransform content = Ui.Rect(paper.transform, "Content");
            Ui.Column(content, 14);
            buttons = Ui.Rect(paper.transform, "Buttons");
            Ui.Row(buttons, 24);
            Ui.Size(buttons.gameObject, -1, 92);
            return content;
        }
    }
}

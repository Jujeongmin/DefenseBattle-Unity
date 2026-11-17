using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>설정 (menu.ts openSettings): 배경음악·효과음 음량, 언어 5종, 튜토리얼 다시 하기, 프로필</summary>
    public sealed class SettingsPage
    {
        readonly Modal m_panel;
        readonly BattleAudio m_audio;
        readonly Action m_closed;

        /// <summary>프로필 열기 / 튜토리얼 다시 (로비가 연결)</summary>
        public Action OpenProfile;
        public Action ReplayTutorial;

        public SettingsPage(Modal panel, BattleAudio audio, Action closed)
        {
            m_panel = panel;
            m_audio = audio;
            m_closed = closed;
        }

        public void Open()
        {
            RectTransform body = m_panel.Panel(Loc.Tl("설정"), m_closed);
            Volume(body, "music", Loc.Tl("배경음악"), SoundSettings.BgmVolume, v => m_audio.SetBgmVolume(v), false);
            Volume(body, "sound", Loc.Tl("효과음"), SoundSettings.SfxVolume, v => m_audio.SetSfxVolume(v), true);

            Image lang = Ui.Frame(body, "paper_dark", 1.8f, "Language");
            Ui.Size(lang.gameObject, -1, 230);
            Ui.Column(lang.transform, 10, 20, 20, 14, 18);
            Ui.Line(lang.transform, Loc.Icon("flag") + " Language · " + Loc.Tl("언어"), 26, Ui.Cream, TextAlignmentOptions.Left);
            RectTransform grid = Ui.Rect(lang.transform, "List");
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(176, 64);
            g.spacing = new Vector2(10, 10);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            for (int i = 0; i < Localization.Ids.Length; i++)
            {
                string id = Localization.Ids[i];
                Button b = Ui.Button(grid, id == Localization.Current ? "btn_gold" : "btn_blue", Localization.Names[i], 24, () =>
                {
                    if (id == Localization.Current) return;
                    Localization.Save(id);
                    Open(); // 새 언어로 다시 그리기
                });
                // 한국어·영어일 땐 CJK 폴백이 없어 日本語·中文 이 깨진다 → 그 언어 글꼴로 이름을 그린다
                TMP_FontAsset cjk = Ui.Cat.CjkFont(id);
                if (cjk != null) b.GetComponentInChildren<TextMeshProUGUI>().font = cjk;
            }

            Row(body, "play", Loc.Tl("튜토리얼"), Loc.Tl("다시 하기"), () => ReplayTutorial?.Invoke());
            Row(body, "trophy", "<noparse>" + Profile.Nick + "</noparse>", Loc.Tl("프로필"), () => OpenProfile?.Invoke());
        }

        void Volume(Transform body, string icon, string label, float value, Action<float> set, bool sfx)
        {
            Image row = Ui.Frame(body, "paper_dark", 1.8f, label);
            Ui.Size(row.gameObject, -1, 120);
            Ui.Row(row.transform, 14, 20, 20, 12, 14);
            Image ic = Ui.Icon(row.transform, value > 0 ? icon : "mute", 48);
            RectTransform info = Ui.Rect(row.transform, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 4, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(info, label, 24, Ui.Cream, TextAlignmentOptions.Left);
            TextMeshProUGUI num = null;
            Slider s = Ui.Slider(info, value, v =>
            {
                v = Mathf.Round(v * 20) / 20; // 5 단위 (step 5)
                set(v);
                num.text = Mathf.RoundToInt(v * 100).ToString();
                ic.sprite = Ui.Cat.Icon(v > 0 ? icon : "mute");
            });
            Ui.Size(s.gameObject, -1, 44);
            if (sfx)
            {
                // 놓으면 그 크기로 한 번 들려준다
                var trigger = s.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
                up.callback.AddListener(_ => m_audio.Play(Sfx.Button));
                trigger.triggers.Add(up);
            }
            num = Ui.Line(row.transform, Mathf.RoundToInt(value * 100).ToString(), 26, Ui.Cream);
            Ui.Size(num.gameObject, 64, -1);
        }

        static void Row(Transform body, string icon, string title, string button, Action click)
        {
            Image row = Ui.Frame(body, "paper_dark", 1.8f, title);
            Ui.Size(row.gameObject, -1, 100);
            Ui.Row(row.transform, 14, 20, 20, 10, 12);
            Ui.Icon(row.transform, icon, 44);
            Ui.Size(Ui.Line(row.transform, title, 26, Ui.Cream, TextAlignmentOptions.Left).gameObject, -1, -1, 1);
            Ui.Size(Ui.Button(row.transform, "btn_blue", button, 24, click).gameObject, 180, 68);
        }
    }
}

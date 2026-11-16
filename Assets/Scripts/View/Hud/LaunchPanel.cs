using System;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>임시 시작 화면 (1-1 / 무한 모드) - 3단계 로비가 생기면 지운다</summary>
    public sealed class LaunchPanel : MonoBehaviour
    {
        [SerializeField] GameObject m_root;
        [SerializeField] Button m_stage;
        [SerializeField] Button m_endless;

        public event Action<BattleLaunch> Chosen;

        void Awake()
        {
            m_stage.onClick.AddListener(() => Choose(new BattleLaunch(BattleLaunch.Mode.Stage, "1-1")));
            m_endless.onClick.AddListener(() => Choose(new BattleLaunch(BattleLaunch.Mode.Endless, null)));
        }

        public void Show() => m_root.SetActive(true);
        public void Hide() => m_root.SetActive(false);

        void Choose(BattleLaunch l)
        {
            Hide();
            Chosen?.Invoke(l);
        }
    }
}

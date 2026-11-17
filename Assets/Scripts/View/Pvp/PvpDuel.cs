using System;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 대전 한 판 연결 (main.ts startPvp): 3초 카운트다운 동안 내 판을 멈추고, 끝나면 함께 시작.
    /// 내가 던진 몹 → 상대, 상대가 던진 몹 → 내 전장. 0.25초마다 내 전장 스냅샷을 상대에게 (봇은 약한 곳을 노린다).
    /// 내 전장이 무너지면 패배를 알리고, 서버 결과가 6초 안에 안 오면 트로피 변화 없이 패배로 끝낸다. 결과는 한 번만.
    /// 시간은 BattleFlow 가 Tick 으로 준다 (내 판과 같은 시계 - 에디터 배속도 양쪽 똑같이).
    /// </summary>
    public sealed class PvpDuel
    {
        /// <summary>봇전 시작 카운트다운 (pvp.ts startAt = now + 3000). 실시간은 상대의 StartDelay (서버 시작 시각까지)</summary>
        public const double CountdownSec = 3;
        /// <summary>내가 무너진 뒤 결과를 기다리는 최대 시간 (main.ts fallback 6000)</summary>
        public const double ResultTimeout = 6;

        readonly Game m_player;
        readonly BoardSnapshot m_mySnap = new BoardSnapshot();
        readonly AttackGate m_gate = new AttackGate();
        double m_countdown;
        double m_snapAcc;
        double m_fallback = -1;
        bool m_started;
        bool m_closed;

        public IPvpOpponent Opponent { get; }
        public Game Player => m_player;
        /// <summary>시작 뒤 흐른 실제 초 (경과 시간 표시)</summary>
        public double Elapsed { get; private set; }
        public bool CountingDown => !m_started && !m_closed;
        public double CountdownLeft => Math.Max(0, m_countdown);

        /// <summary>카운트다운이 끝나 시작함 (GO!)</summary>
        public event Action Go;
        /// <summary>대전 끝 (결과 화면) - 한 번만</summary>
        public event Action<PvpOutcome> Result;

        public PvpDuel(Game player, IPvpOpponent opp)
        {
            m_player = player;
            Opponent = opp;
            m_countdown = Math.Max(0, opp.StartDelay);
            player.Paused = true;
            player.AttackSent += OnMyAttack;
            player.GameEnded += OnMyEnd;
            opp.Attack += OnOppAttack;
            opp.Finished += OnFinished;
            _ = opp.Begin();
        }

        void OnMyAttack(AttackPayload p) => Opponent.SendAttack(p);

        void OnOppAttack(AttackPayload p)
        {
            if (m_closed || !m_started) return;
            if (m_gate.Allow(ref p, m_player.Wave, Elapsed)) m_player.ReceiveAttack(p); // 웨이브·빈도 검사 (실시간 상대는 직접 보낸다)
        }

        void OnMyEnd(GameOverData d)
        {
            if (m_closed || !d.IsPvp || d.PvpWon || m_fallback >= 0) return;
            Opponent.ReportDefeat();
            m_fallback = ResultTimeout;
        }

        void OnFinished(PvpOutcome o)
        {
            if (m_closed) return;
            Close();
            if (!m_player.Over) m_player.PvpEnd(o.Won, o.Won ? Loc.Tl("상대 필드가 무너졌습니다!") : Loc.Tl("내 필드가 무너졌습니다"));
            Result?.Invoke(o);
        }

        public void Tick(double dt)
        {
            if (m_closed) return;
            if (m_fallback >= 0)
            {
                Opponent.Tick(dt); // 실시간 상대는 결과를 Ping 으로도 받는다
                if (m_closed) return;
                m_fallback -= dt;
                if (m_fallback < 0) OnFinished(new PvpOutcome { Won = false, Trophies = EconCache.State.Pvp.Trophies });
                return;
            }
            if (!m_started)
            {
                // 실시간 상대는 서버 시작 시각을 따라 줄어드는 StartDelay 를 가진다 - 프레임이 끊겨 dt 가 잘려도 늦지 않게
                m_countdown = Math.Min(m_countdown - dt, Opponent.StartDelay);
                if (m_countdown > 0) return;
                m_started = true;
                m_player.Paused = false;
                Go?.Invoke();
                return;
            }
            if (m_player.Over) return;
            Elapsed += dt;
            Opponent.Tick(dt);
            m_snapAcc += dt;
            if (m_snapAcc >= PvpConfig.ReportInterval)
            {
                m_snapAcc = 0;
                Opponent.SendSnapshot(m_player.Snapshot(m_mySnap));
            }
        }

        /// <summary>대전을 내림 (로비로). reportDefeat = 도중에 나가 패배로 기록</summary>
        public void Leave(bool reportDefeat)
        {
            if (!m_closed && reportDefeat && m_fallback < 0) Opponent.ReportDefeat();
            Close();
            Opponent.Leave();
        }

        void Close()
        {
            if (m_closed) return;
            m_closed = true;
            m_player.AttackSent -= OnMyAttack;
            m_player.GameEnded -= OnMyEnd;
            Opponent.Attack -= OnOppAttack;
            Opponent.Finished -= OnFinished;
        }
    }
}

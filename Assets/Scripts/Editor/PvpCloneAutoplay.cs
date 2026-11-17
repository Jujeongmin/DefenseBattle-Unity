using System;
using System.Reflection;
using System.Threading.Tasks;
using DefenseBattle.Core;
using DefenseBattle.View;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 개발용: 실시간 대전 2인스턴스 확인 (Multiplayer Play Mode). 가상 플레이어(Library/VP/ 아래 복제 에디터)에서만 돈다.
/// 플레이 모드에 들어가면 로비가 뜰 때까지 기다렸다가 실제 상대를 2분 동안 찾고, 찾으면 대전을 연다 (전장은 손대지 않음).
/// 새 계정이면 첫 대전이 늘 봇이므로 봇전 패배를 한 번 기록해 둔다. 메인 에디터에서 '대전 시작'을 누르면 둘이 만난다.
/// </summary>
[InitializeOnLoad]
static class PvpCloneAutoplay
{
    const double SearchSec = 120;

    static PvpCloneAutoplay()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/VP/")) return;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode) Run();
        };
    }

    static async void Run()
    {
        try
        {
            Lobby lobby = null;
            double end = EditorApplication.timeSinceStartup + 90;
            while (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < end)
            {
                lobby = UnityEngine.Object.FindAnyObjectByType<Lobby>();
                if (lobby != null && EconCache.Loaded && PvpOnline.Available) break;
                await Task.Delay(500);
            }
            if (lobby == null || !EconCache.Loaded) { Debug.LogWarning("[CloneAuto] 로비가 뜨지 않음"); return; }

            if (PvpRules.IsFirstMatch(EconCache.State))
            {
                RunTicket t = await CloudClient.Call<RunTicket>(CloudApi.PvpBotStart);
                await CloudClient.Call<BotResultReply>(CloudApi.PvpBotResult, CloudClient.Args("id", t.Id, "won", false));
                await CloudClient.Sync();
                Debug.Log("[CloneAuto] 첫 대전(봇) 패배 기록");
            }

            MatchSearch search = PvpOnline.NewSearch();
            Debug.Log("[CloneAuto] 상대 찾는 중 (트로피 " + EconCache.State.Pvp.Trophies + ")");
            MatchInfo m = await search.Find(SearchSec, () => !EditorApplication.isPlaying, null);
            if (m == null) { Debug.LogWarning("[CloneAuto] 상대 없음"); return; }
            Debug.Log("[CloneAuto] 매칭 " + m.MatchId + " 호스트=" + m.Host + " 상대=" + m.Opponent?.Nick);
            OnlineOpponent opp = await search.Connect(m, () => !EditorApplication.isPlaying);
            if (opp == null) { Debug.LogWarning("[CloneAuto] 연결 실패"); return; }
            Debug.Log("[CloneAuto] 연결됨, " + opp.StartDelay.ToString("0.00") + "초 뒤 시작");
            opp.Attack += p => Debug.Log("[CloneAuto] 상대 공격 도착 카드 " + p.Card);
            opp.Finished += o => Debug.Log("[CloneAuto] 결과 승리=" + o.Won + " 트로피 " + o.Trophies + " (" + o.Delta + ")");
            MethodInfo launch = typeof(Lobby).GetMethod("Launch", BindingFlags.Instance | BindingFlags.NonPublic);
            launch.Invoke(lobby, new object[] { BattleLaunch.Pvp(opp) });
        }
        catch (Exception ex) { Debug.LogWarning("[CloneAuto] 실패: " + ex); }
    }
}

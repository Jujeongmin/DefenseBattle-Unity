using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Leaderboards.Model;

namespace DefenseBattle.Server.Ugs
{
    public sealed class SystemClock : IClock
    {
        public long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>INotifier → Cloud Code 푸시 메시지 (기기는 SubscribeToPlayerMessagesAsync 로 받는다)</summary>
    public sealed class UgsNotifier : INotifier
    {
        readonly IPushClient m_push;
        readonly IExecutionContext m_ctx;

        public UgsNotifier(IPushClient push, IExecutionContext ctx)
        {
            m_push = push;
            m_ctx = ctx;
        }

        public async Task Send(string playerId, string type, object payload)
        {
            try
            {
                await m_push.SendPlayerMessageAsync(m_ctx, JsonConvert.SerializeObject(payload, Json.Settings), type, playerId);
            }
            catch (ApiException)
            {
                // 알림은 덤 - 기기가 poll 로도 확인하므로 실패해도 진행
            }
        }
    }

    /// <summary>IRankBoard → UGS Leaderboards "trophies" (대시보드에서 '마지막 점수 유지'로 만든다 - 트로피는 내려가기도 하므로)</summary>
    public sealed class UgsRankBoard : IRankBoard
    {
        public const string LeaderboardId = "trophies";

        readonly IGameApiClient m_api;
        readonly IExecutionContext m_ctx;

        public UgsRankBoard(IGameApiClient api, IExecutionContext ctx)
        {
            m_api = api;
            m_ctx = ctx;
        }

        public async Task Submit(string playerId, int trophies, int wins, string nick)
        {
            var meta = new Dictionary<string, object> { { "nick", nick }, { "wins", wins } };
            await m_api.Leaderboards.AddLeaderboardPlayerScoreAsync(m_ctx, m_ctx.ServiceToken, Guid.Parse(m_ctx.ProjectId), LeaderboardId, playerId,
                new AddLeaderboardScore(trophies, meta));
        }

        public async Task<List<RankEntry>> Top(int count)
        {
            var res = await m_api.Leaderboards.GetLeaderboardScoresAsync(m_ctx, m_ctx.ServiceToken, Guid.Parse(m_ctx.ProjectId), LeaderboardId, true, 0, count);
            var list = new List<RankEntry>();
            foreach (LeaderboardEntry e in res.Data.Results)
            {
                JObject meta = e.Metadata == null ? null : e.Metadata as JObject ?? JObject.Parse(e.Metadata.ToString());
                list.Add(new RankEntry
                {
                    PlayerId = e.PlayerId,
                    Nick = (string)meta?["nick"] ?? Loc.Tl(NickFilter.DefaultNick),
                    Wins = (int?)meta?["wins"] ?? 0,
                    Trophies = (int)e.Score,
                    Rank = e.Rank + 1,
                });
            }
            return list;
        }
    }

    /// <summary>
    /// Google Play 라이선스 공개 키 (Cloud Code 대시보드 → Secrets 의 GOOGLE_PLAY_LICENSE_KEY).
    /// 영수증 검증은 GooglePlayReceiptVerifier (Economy 는 새 프로젝트에서 쓸 수 없다).
    /// </summary>
    public static class UgsLicenseKey
    {
        public const string SecretName = "GOOGLE_PLAY_LICENSE_KEY";

        public static async Task<string> Load(IGameApiClient api, IExecutionContext ctx)
        {
            try
            {
                var secret = await api.SecretManager.GetSecret(ctx, SecretName);
                return secret?.Value;
            }
            catch (Exception)
            {
                return null; // 키가 없으면 검증기가 모든 영수증을 거절한다
            }
        }
    }
}

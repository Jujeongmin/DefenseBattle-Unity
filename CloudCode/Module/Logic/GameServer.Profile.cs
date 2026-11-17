using System.Threading.Tasks;
using DefenseBattle.Core;

namespace DefenseBattle.Server
{
    // 닉네임·랭킹 (원본 setNick / pvpLeaderboard)
    public sealed partial class GameServer
    {
        /// <summary>닉네임 바꾸기 (비속어·빈 이름은 기본 이름). 대전 기록이 있으면 랭킹 이름도 바로 바꾼다</summary>
        public async Task<string> SetNick(string playerId, string nick)
        {
            string clean = NickFilter.Clean(nick);
            await m_store.MutatePlayer<ProfileRecord, bool>(playerId, Keys.Profile, () => new ProfileRecord(), p =>
            {
                p.Nick = clean;
                return true;
            });
            EconState e = await LoadEcon(playerId);
            if (e.Pvp.Wins + e.Pvp.Losses > 0) await m_rank.Submit(playerId, e.Pvp.Trophies, e.Pvp.Wins, clean);
            return clean;
        }

        /// <summary>저장된 닉네임 (없으면 "") - 새 기기에서 같은 계정으로 들어왔을 때 이름을 되찾는다</summary>
        public async Task<string> GetNick(string playerId)
        {
            ProfileRecord p = await m_store.ReadPlayer<ProfileRecord>(playerId, Keys.Profile);
            return p?.Nick ?? "";
        }

        public async Task<LeaderboardReply> Leaderboard(string playerId)
        {
            EconState e = await LoadEcon(playerId);
            return new LeaderboardReply { Top = await m_rank.Top(50), Me = e.Pvp };
        }

        /// <summary>
        /// 계정 삭제 (Google Play 계정 삭제 정책): 대기열에서 빼고, 플레이어 저장 값(경제·프로필·기록) 전부와 랭킹 기록을 지운다.
        /// 로그인 계정 자체는 기기가 이어서 Authentication 으로 지운다. 결제 영수증은 이 플레이어 표시에 묶여 있어 다른 계정이 다시 쓸 수 없다.
        /// </summary>
        public async Task<bool> DeleteAccount(string playerId)
        {
            await PvpCancel(playerId);
            await m_store.DeletePlayer(playerId);
            await m_rank.Remove(playerId);
            return true;
        }
    }
}

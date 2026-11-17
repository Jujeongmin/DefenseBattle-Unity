using System.Threading.Tasks;

namespace DefenseBattle.View
{
    /// <summary>Relay 연결 만들기: 호스트는 방을 열어 참가 코드를 받고, 손님은 그 코드로 들어간다</summary>
    public interface IPvpLinkFactory
    {
        Task<(IPvpLink link, string joinCode)> Host();
        Task<IPvpLink> Join(string joinCode);
    }
}

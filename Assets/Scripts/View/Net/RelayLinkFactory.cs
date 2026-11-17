using System;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>IPvpLinkFactory 의 실제 구현: UGS Relay 방 (손님 1명, dtls)</summary>
    public sealed class RelayLinkFactory : IPvpLinkFactory
    {
        const string Protocol = "dtls";
        /// <summary>손님이 호스트에 붙기를 기다리는 최대 초</summary>
        const double JoinTimeout = 10;

        public async Task<(IPvpLink link, string joinCode)> Host()
        {
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(1);
            string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            var (link, transport) = RelayLink.Create();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(alloc, Protocol));
            if (!link.StartHost())
            {
                link.Close();
                throw new Exception("relay host start failed");
            }
            Debug.Log("[Pvp] Relay 방 열림");
            return (link, code);
        }

        public async Task<IPvpLink> Join(string joinCode)
        {
            JoinAllocation join = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var (link, transport) = RelayLink.Create();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(join, Protocol));
            if (!await link.StartClient(JoinTimeout))
            {
                link.Close();
                throw new Exception("relay join timed out");
            }
            Debug.Log("[Pvp] Relay 방 들어감");
            return link;
        }
    }
}

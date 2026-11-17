using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DefenseBattle.View
{
    /// <summary>
    /// Relay 위 두 기기 연결 (IPvpLink). 대전 한 판마다 NetworkManager 를 만들고 끝나면 지운다 (오브젝트 동기화 없이 이름 메시지만).
    /// 스냅샷(~2.5KB)은 조각 나눔 방식, 공격은 순서대로 꼭 닿는 방식. 받은 바이트는 재사용 버퍼에 복사해 넘긴다.
    /// </summary>
    public sealed class RelayLink : IPvpLink
    {
        const string SnapMsg = "s";
        const string AtkMsg = "a";
        const ulong NoPeer = ulong.MaxValue;

        readonly GameObject m_go;
        readonly NetworkManager m_net;
        readonly byte[] m_recv = new byte[Core.PvpWire.MaxSnapshotBytes];
        ulong m_peer = NoPeer;
        bool m_closed;

        public event Action<byte[], int> SnapshotReceived;
        public event Action<byte[], int> AttackReceived;

        public bool Connected => !m_closed && m_peer != NoPeer;

        RelayLink(GameObject go, NetworkManager net)
        {
            m_go = go;
            m_net = net;
        }

        /// <summary>NetworkManager + UnityTransport 를 만든다 (씬 관리·플레이어 오브젝트 없이)</summary>
        public static (RelayLink link, UnityTransport transport) Create()
        {
            if (NetworkManager.Singleton != null) Object.Destroy(NetworkManager.Singleton.gameObject); // 지난 판 것이 남았으면
            var go = new GameObject("PvpRelay");
            Object.DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            transport.MaxPayloadSize = 16384;
            var net = go.AddComponent<NetworkManager>();
            net.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = false,
            };
            return (new RelayLink(go, net), transport);
        }

        /// <summary>호스트 시작 - 상대가 들어오면 Connected</summary>
        public bool StartHost()
        {
            m_net.OnClientConnectedCallback += OnClientConnected;
            m_net.OnClientDisconnectCallback += OnClientDisconnected;
            if (!m_net.StartHost()) return false;
            Register();
            return true;
        }

        /// <summary>손님 시작 → 호스트에 붙을 때까지 기다림</summary>
        public async Task<bool> StartClient(double timeoutSec)
        {
            m_net.OnClientConnectedCallback += OnClientConnected;
            m_net.OnClientDisconnectCallback += OnClientDisconnected;
            if (!m_net.StartClient()) return false;
            Register();
            double end = Time.realtimeSinceStartupAsDouble + timeoutSec;
            while (!Connected && !m_closed && Time.realtimeSinceStartupAsDouble < end) await Task.Yield();
            return Connected;
        }

        void Register()
        {
            m_net.CustomMessagingManager.RegisterNamedMessageHandler(SnapMsg, OnSnap);
            m_net.CustomMessagingManager.RegisterNamedMessageHandler(AtkMsg, OnAtk);
        }

        void OnClientConnected(ulong id)
        {
            if (m_net.IsHost)
            {
                if (id != NetworkManager.ServerClientId) m_peer = id;
            }
            else if (id == m_net.LocalClientId) m_peer = NetworkManager.ServerClientId;
        }

        void OnClientDisconnected(ulong id)
        {
            if (!m_net.IsHost || id == m_peer) m_peer = NoPeer;
        }

        void OnSnap(ulong sender, FastBufferReader reader)
        {
            int n = Read(reader);
            if (n > 0) SnapshotReceived?.Invoke(m_recv, n);
        }

        void OnAtk(ulong sender, FastBufferReader reader)
        {
            int n = Read(reader);
            if (n > 0) AttackReceived?.Invoke(m_recv, n);
        }

        int Read(FastBufferReader reader)
        {
            int n = reader.Length - reader.Position;
            if (n <= 0 || n > m_recv.Length) return 0;
            byte[] buf = m_recv;
            reader.ReadBytesSafe(ref buf, n);
            return n;
        }

        public void SendSnapshot(byte[] buf, int len) => Send(SnapMsg, buf, len, NetworkDelivery.ReliableFragmentedSequenced);
        public void SendAttack(byte[] buf, int len) => Send(AtkMsg, buf, len, NetworkDelivery.ReliableSequenced);

        void Send(string name, byte[] buf, int len, NetworkDelivery how)
        {
            if (!Connected) return;
            using (var w = new FastBufferWriter(len, Allocator.Temp))
            {
                w.WriteBytesSafe(buf, len);
                m_net.CustomMessagingManager.SendNamedMessage(name, m_peer, w, how);
            }
        }

        public void Close()
        {
            if (m_closed) return;
            m_closed = true;
            m_peer = NoPeer;
            if (m_net != null)
            {
                m_net.OnClientConnectedCallback -= OnClientConnected;
                m_net.OnClientDisconnectCallback -= OnClientDisconnected;
                if (m_net.CustomMessagingManager != null)
                {
                    m_net.CustomMessagingManager.UnregisterNamedMessageHandler(SnapMsg);
                    m_net.CustomMessagingManager.UnregisterNamedMessageHandler(AtkMsg);
                }
                m_net.Shutdown();
            }
            if (m_go != null) Object.Destroy(m_go);
        }
    }
}

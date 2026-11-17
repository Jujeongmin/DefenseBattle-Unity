# 실시간 PvP (6단계) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 로비 '대전 시작'이 실제 상대를 8초 찾고, 찾으면 Relay 로 전장을 주고받으며 한 판을 끝까지 하고, 결과·트로피는 서버가 정한다. 못 찾으면 지금처럼 봇.

**Architecture:** 매칭·시작 시각·이탈 판정·결과는 이미 있는 Cloud Code (`PvpQueue/Poll/Cancel/Ready/Ping/Defeat/Leave/ClaimAway`). 기기 사이 중계는 Relay + Netcode for GameObjects 의 Named Message (스냅샷 0.25초 Unreliable, 공격 ReliableSequenced). 4단계 `IPvpOpponent` 자리에 `OnlineOpponent` 가 들어가고, 화면(`PvpDuel`·`MatchPopup`)은 거의 그대로.
서버·전송은 인터페이스(`IPvpServer`, `IPvpLink`)로 주입해 흐름을 EditMode 테스트로 확인하고, 실제 Relay 는 Multiplayer Play Mode 2인스턴스로 확인한다.

**Tech Stack:** Unity 6000.4, NGO 2.14.0, Multiplayer Services 2.4.1 (Relay), UGS Cloud Code, Multiplayer Play Mode.

**Spec:** `docs/superpowers/specs/2026-10-19-defense-battle-port-design.md` (6. 실시간 PvP, 대응표 pvpQueue~pvpDefeat)

## Global Constraints
- 원본 기준: `../DefenseBattle/game/src/pvp.ts` (findOnlineMatch 0.5초 확인·idle 이면 재등록, BOT_FALLBACK_SEC 8, 시작 시각 = 서버 startAt 을 서버 now 기준 남은 시간으로, 스냅 0.25초, 상대 유예 20초 + 0.5초 뒤 ClaimAway, 내 연결 30초 못 붙으면 포기).
- 첫 대전(`PvpRules.IsFirstMatch`)은 지금처럼 바로 봇.
- 판 중 프레임당 할당 0B: 스냅샷 직렬화·수신은 버퍼 재사용 (CLAUDE.md 최적화 규칙).
- 결과·트로피·보상은 서버 권위. 기기는 서버 결과를 받아 보여 주고 `CloudClient.Sync()` 로 경제를 다시 받는다.
- 화면 문구는 `Loc.Tl` (새 문구는 `Tools/i18n_extra.json` 5개 언어).

## Review Focus
1. 한쪽이 앱을 내림(백그라운드)·꺼짐 → 남은 쪽이 20초 뒤 승리, 돌아온 쪽은 패배 결과를 받는다.
2. 매칭은 됐는데 상대가 준비 안 함·Relay 연결 실패 → 로비로 돌아가고 안내 (멈추지 않음).
3. 양쪽이 같은 순간 시작 (기기 시계 차이 무시, 서버 시각 기준 남은 시간).
4. 결과가 두 번 오거나(Ping·Defeat 답) 늦게 와도 결과 화면은 한 번.
5. 판 중 스냅샷·공격 송수신 할당 0B.

---

### Task 1: 스냅샷·공격 직렬화 (Core)
**Files:** Create `Assets/Scripts/Core/Net/PvpWire.cs`, Test `Assets/Tests/EditMode/PvpWireTests.cs`
**Interfaces — Produces:** `static int PvpWire.WriteSnapshot(BoardSnapshot s, byte[] buf)` (쓴 길이), `static bool PvpWire.ReadSnapshot(byte[] buf, int len, BoardSnapshot into)` (목록 Clear 후 채움), `static int PvpWire.WriteAttack(AttackPayload p, byte[] buf)`, `static bool PvpWire.ReadAttack(byte[] buf, int len, out AttackPayload p)`, `const int PvpWire.MaxSnapshotBytes = 8192`.
- 정수는 zigzag varint, double 은 8바이트. Fx(`List<int[]>`)는 길이+원소.
- [ ] 테스트: `Snapshot_RoundTrips` (모든 필드), `Snapshot_ReadReusesLists` (두 번째 읽기 뒤 이전 값 없음), `Attack_RoundTrips`, `Read_Truncated_ReturnsFalse`, `Write_SteadyState_NoAllocation` (.NET 쪽 GC.GetAllocatedBytesForCurrentThread, Unity 쪽 Ignore - PoolTests 와 같은 방식).
- [ ] 구현 → `dotnet test CoreTests/Tests` → 커밋.

### Task 2: 실시간 상대 `OnlineOpponent`
**Files:** Create `Assets/Scripts/View/Pvp/IPvpServer.cs`, `IPvpLink.cs`, `OnlineOpponent.cs`; Modify `IPvpOpponent.cs` (+`double StartDelay { get; }`), `BotOpponent.cs` (=`PvpDuel.CountdownSec`), `PvpDuel.cs` (카운트다운 = `opp.StartDelay`); Test `Assets/Tests/EditMode/View/OnlineOpponentTests.cs`.
**Interfaces — Produces:**
- `IPvpServer`: `Task<QueueStatus> Queue()`, `Poll()`, `Task Cancel()`, `Task<MatchState> Ready(string matchId, string joinCode)`, `Task<MatchState> Ping(string matchId)`, `Task<PvpMatchResult> Defeat(string matchId)`, `Leave(string matchId)`, `ClaimAway(string matchId)`, `string PlayerId { get; }`.
- `IPvpLink`: `bool Connected`, `event Action<byte[], int> SnapshotReceived`, `event Action<byte[], int> AttackReceived`, `void SendSnapshot(byte[] b, int len)`, `void SendAttack(byte[] b, int len)`, `void Close()`.
- `OnlineOpponent(MatchInfo m, double startDelay, IPvpServer s, IPvpLink link)` : `IPvpOpponent`. Tick 이 2초마다 Ping (결과가 오면 Finished), 상대 스냅이 20.5초 없으면 ClaimAway (2초 간격 재시도), ReportDefeat → Defeat 답, Leave → 서버 Leave + link.Close. 결과는 한 번만 (`PvpOutcome` 에 내 Delta·Trophies·Reward), 결과 뒤 `CloudClient.Sync()` 는 화면 쪽(주입 콜백 `Action onResult`)에서.
- [ ] 테스트: `Snapshot_FromLink_RaisesSnapshot`, `Attack_RoundTripsThroughLink`, `Ping_DoneResult_FinishesOnce`, `Defeat_ResultFinishes`, `DefeatAndPingBothReturnResult_FinishedOnce`, `NoSnapshotsForGrace_ClaimsAway`, `SnapshotsFlowing_DoesNotClaim`, `Leave_CallsServerAndClosesLink`, `Ping_Cancelled_FinishesAsLossWithoutTrophyChange`.
- [ ] `PvpDuel` 기존 테스트 통과 + `Duel_UsesOpponentStartDelay`.

### Task 3: 매칭 `MatchSearch` + 화면
**Files:** Create `Assets/Scripts/View/Pvp/MatchSearch.cs`, `IPvpLinkFactory.cs`; Modify `Assets/Scripts/View/Ui/MatchPopup.cs`; Test `Assets/Tests/EditMode/View/MatchSearchTests.cs`.
**Interfaces — Produces:** `MatchSearch(IPvpServer s, IPvpLinkFactory f, Func<double> clock)`; `Task<MatchInfo> Find(double maxWait, Func<bool> cancelled, Action<double> onSecond)` (null = 시간 초과 → 봇, 취소면 서버 Cancel 후 null); `Task<OnlineOpponent> Connect(MatchInfo m, Func<bool> cancelled)` (호스트: `f.Host()` → 참가 코드 → Ready, 손님: Ready → Ping 으로 코드 기다림 → `f.Join(code)`; 둘 다 Playing 될 때까지 0.5초마다 Ping, 30초 넘거나 Cancelled 면 null + Leave). `IPvpLinkFactory`: `Task<(IPvpLink link, string joinCode)> Host()`, `Task<IPvpLink> Join(string code)`.
- 시작 지연 = `(StartAt - Now)/1000` (서버 시각 기준) - 받은 순간부터 잰다.
- [ ] 테스트: `Find_MatchedOnQueue`, `Find_MatchedOnPoll`, `Find_IdleRequeues`, `Find_Timeout_CancelsAndReturnsNull`, `Find_Cancelled_CancelsServer`, `Connect_Host_UploadsCodeAndStarts`, `Connect_Guest_WaitsForCodeThenJoins`, `Connect_Cancelled_ReturnsNullAndLeaves`, `Connect_ReadyTimeout_ReturnsNull`, `Connect_StartDelayFromServerClock`.
- [ ] `MatchPopup`: 첫 대전은 봇 그대로. 아니면 실제 찾기(초 표시·취소) → 8초 넘으면 지금 봇 흐름(남은 연출 없이 '매칭 완료') → 찾으면 '매칭 완료' VS(상대 닉·트로피) 동안 Connect → 성공이면 launch, 실패면 닫고 토스트 "상대와 연결하지 못했어요". 결과가 오면 `CloudClient.Sync()`.

### Task 4: Relay 연결 + 2인스턴스 확인
**Files:** Create `Assets/Scripts/View/Net/RelayLinkFactory.cs`, `RelayLink.cs`, `UgsPvpServer.cs`; Modify `DefenseBattle.View.asmdef` (NGO·Transport·Multiplayer 참조), `UgsSession.cs` (에디터 Multiplayer Play Mode 가상 플레이어면 Authentication 프로필을 나눔 - `Application.dataPath` 가 `Library/VP/` 아래); Packages: `com.unity.multiplayer.playmode`.
- RelayLink: 런타임에 NetworkManager + UnityTransport 를 만들고 (씬 관리 끔, 플레이어 프리팹 없음) Relay 서버 데이터(dtls)로 호스트/클라이언트 시작, CustomMessagingManager 이름 `"s"`(UnreliableSequenced) `"a"`(ReliableSequenced). 버퍼 재사용 (`FastBufferWriter` Allocator.Temp, 수신은 byte[] 필드로 복사).
- [ ] 컴파일 → 에디터 + 가상 플레이어 1명으로: 두 쪽 다 대전 시작 → 매칭 → 같은 시드·동시 시작 → 몹 던지기가 상대에게 도착 → 한쪽 패배 → 양쪽 결과·트로피 반영. 판 도중 한쪽 나가기 → 상대 승리. 판 중 할당 0B (Profiler GC Alloc).
- [ ] 커밋.

### Task 5: 확인·마무리
- [ ] 테스트 3종(Core·Cloud·Unity), 콘솔 0, spec 진행표 6단계, 최종 리뷰(opus), 고치기, push.

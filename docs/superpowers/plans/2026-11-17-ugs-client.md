# 5단계 UGS 연동 마무리 (결제·Play 게임즈 로그인·닉네임) Implementation Plan

> 실행: Native (superpowers:executing-plans). 기록: `.superpowers/sdd/2026-11-17-ugs-client/progress.md`. 각 Task 는 테스트 → 구현 → 컴파일·콘솔 → 확인 → 커밋 → push.

**Goal:** 상점·특가·시즌 패스의 결제 버튼이 Google Play 결제 → 서버 영수증 검증(`RedeemPurchase`) → 지급까지 동작하고, Play 게임즈 로그인으로 기기를 바꿔도 같은 계정(진행·닉네임)이 이어진다. spec 5단계 완료 기준: 저장값 조작이 반영되지 않음(서버 권위 - 이미), 결제 중복 지급 없음(서버 주문 id - 이미, 클라이언트는 확인(confirm)을 서버 지급 뒤에만), 기기 바꿔도 진행 유지.

**Architecture:** 서버는 이미 완성(`RedeemPurchase` + `GooglePlayReceiptVerifier` + 주문 id 1회). 기기 쪽은 SDK 를 모르는 결정 로직을 View 에 두고(`PurchaseFlow`·`GoogleReceipt`·`AccountLink`, EditMode 테스트), Unity IAP 5 / GPGS SDK 를 부르는 얇은 연결부는 `Assets/Scripts/Platform/`(asmdef 없음 - 기존 `Assets/Scripts/Ads` 와 같은 방식)에 둔다. 연결부는 씬의 `PlatformBridge` 컴포넌트가 `Store.Service`·`UgsSession.Link` 에 넣는다.

**Spec:** `docs/superpowers/specs/2026-10-19-defense-battle-port-design.md` 5단계, 대응표(`$onItemPurchased` → Unity IAP + Cloud Code 검증), `docs/ugs-setup.md` 1·4번(사용자 대시보드 작업).

## Global Constraints
- 재화 지급은 서버만. 기기는 서버가 `Granted` 또는 `AlreadyGranted` 를 돌려준 뒤에만 스토어 주문을 확인(ConfirmPurchase)한다 - 확인 안 된 Google 결제는 3일 뒤 자동 환불되므로 검증 실패분이 사용자에게 남지 않는다.
- 결제할 때 `obfuscatedAccountId = PurchaseTag.ForPlayer(UGS PlayerId)` (서버가 남의 영수증 재사용을 막는 값).
- 비밀값(라이선스 키·웹 클라이언트 보안 비밀·키스토어)은 저장소에 넣지 않는다.
- 에디터·Play 게임즈 미설정 기기는 지금처럼 익명 로그인으로 동작해야 한다 (로그인 실패가 게임 시작을 막지 않는다).
- 화면 문구는 한국어 키 `Loc.Tl` (원본에 있는 문구 우선).

## Review Focus
1. 결제 도중 앱이 꺼짐 → 다음 실행에서 남은 주문을 서버에 보내 지급하고 확인한다 (중복 지급 없음).
2. 서버 검증 실패(서명·계정 불일치) → 주문을 확인하지 않고 "결제가 완료되지 않았어요" (재화 없음).
3. 새 기기: 익명으로 먼저 시작했다가 Play 게임즈 로그인이 기존 계정에 이미 연결돼 있으면 그 계정으로 바꾼다 (진행 유지).
4. 결제 버튼 두 번 누름 → 한 번만 결제 창.
5. 오프라인·스토어 연결 실패 → 가격은 원화 기본값, 결제는 "준비 중" 대신 연결 안내.

---

### Task 1: 서버 닉네임 조회 + 새 기기에서 닉네임 되찾기
**Files:** `CloudCode/Module/Logic/GameServer.Profile.cs` (`GetNick(playerId) → string` - 없으면 ""), `CloudCode/Module/Ugs/Functions.cs`, `Assets/Scripts/Core/Net/CloudApi.cs` (`GetNick`), `Assets/Scripts/View/Net/Profile.cs` (`EnsureSynced`: 아직 안 알렸으면 먼저 서버 이름을 받아 있으면 그걸 쓰고, 없을 때만 SetNick), Test: `CloudCode/Tests/ServerTests.cs` (`GetNick_ReturnsSavedNickOrEmpty`).
- [ ] RED → 구현 → `dotnet test CloudCode/Tests` → Cloud Code 배포 → 커밋.

### Task 2: 결제 흐름 (SDK 없는 로직)
**Files:** Create `Assets/Scripts/View/Net/GoogleReceipt.cs`, `Assets/Scripts/View/Net/IIapBackend.cs`, `Assets/Scripts/View/Net/PurchaseFlow.cs`; Test `Assets/Tests/EditMode/View/PurchaseFlowTests.cs`.
**Interfaces:**
- `GoogleReceipt.TryParse(string unityReceipt, out string json, out string signature) : bool` (Unity 통합 영수증 `{"Store","TransactionID","Payload":"{\"json\",\"signature\"}"}`).
- `IIapBackend`: `event Action<IapOrder> Pending; event Action<string, bool> Failed /*productId, cancelled*/; Task<bool> Connect(IReadOnlyList<string> productIds); string PriceOf(string id); decimal? LocalPrice(string id); string Currency(string id); void Buy(string productId, string accountTag); void Confirm(IapOrder o);`
- `IapOrder`: `string ProductId; string Receipt; object Native`.
- `PurchaseFlow : IStore` — ctor(`IIapBackend`, `Func<string> playerTag`, `Func<RedeemArgs, Task<PurchaseReply>> redeem`). `Buy(id)` → 한 번에 하나(진행 중이면 null), backend.Buy → Pending 이 오면 영수증 파싱 → redeem → `Granted||AlreadyGranted` 면 `EconCache.Apply(reply.Econ)` + Confirm + 결과(reveals) / 아니면 Confirm 안 함 + null. Failed → null. 시작 전에 온 Pending(남은 주문)도 같은 방식으로 처리하고 `Restored` 이벤트(상품 id 목록).
- Tests: `Buy_GrantedConfirmsAndReturnsReveals`, `Buy_RejectedDoesNotConfirm`, `Buy_AlreadyGrantedConfirms`, `Buy_CancelledReturnsNull`, `Buy_WhileBusy_ReturnsNull`, `PendingOnStart_RedeemedAndRestoredRaised`, `GoogleReceipt_ParsesPayload`, `Buy_PassesPlayerTag`.
- [ ] RED → 구현 → Unity EditMode 통과 → 커밋.

### Task 3: Unity IAP 5 연결부 + 상점 연결
**Files:** Create `Assets/Scripts/Platform/UnityIapBackend.cs` (`StoreController` - Connect, FetchProducts(소모형), OnPurchasePending, OnPurchaseFailed, Google `SetObfuscatedAccountId`, ConfirmPurchase), `Assets/Scripts/Platform/PlatformBridge.cs` (Awake: `Store.Service = new PurchaseFlow(...)`, 접속 뒤 Connect, `Restored` → 토스트 `"결제한 상품이 지급되었어요"`); 씬 `Battle.unity` 에 PlatformBridge 를 MCP 로 붙임. `ProductBuy`: 스토어 연결 실패 시 안내.
- [ ] 컴파일(에디터 + Android 정의로 `#if` 확인) → 에디터 플레이: 상점 결제 버튼 → IAP 가짜 스토어 → 서버 거절 → "결제가 완료되지 않았어요", 재화 변화 없음 → 커밋.

### Task 4: Play 게임즈 로그인·계정 연결
**Files:** Create `Assets/Scripts/View/Net/AccountLink.cs` (`IAuthOps` 주입, `Run(Func<Task<string>> authCode)`), `Assets/Scripts/Platform/GpgsAuthCode.cs` (Android: `PlayGamesPlatform.Activate` → Authenticate → RequestServerSideAccess), `UgsSession` 수정(세션이 없고 코드가 있으면 GPGS 로 로그인, 익명 세션이면 Link, 이미 다른 계정에 연결돼 있으면 로그아웃 후 GPGS 로그인), Test `Assets/Tests/EditMode/View/AccountLinkTests.cs`: `NoCode_SignsInAnonymously`, `NoSession_WithCode_SignsInWithGpgs`, `AnonymousSession_LinksGpgs`, `AlreadyLinkedElsewhere_SwitchesToGpgsAccount`, `AlreadyLinked_DoesNothing`, `GpgsFails_FallsBackToAnonymous`.
- [ ] RED → 구현 → 에디터 플레이(익명 그대로) → 커밋.

### Task 5: 확인·마무리
- [ ] 테스트 3종, 콘솔 0, 에디터 결제 흐름, Android 정의 컴파일, 사용자 기기 확인 목록(`docs/ugs-setup.md` 5번 갱신), spec 진행표, 최종 리뷰, push.

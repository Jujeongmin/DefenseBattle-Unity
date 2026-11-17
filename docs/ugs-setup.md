# 서버·스토어 세팅 (사용자가 대시보드에서 할 일)

코드로는 할 수 없고 계정이 필요한 작업만 모았다. 순서대로 하면 된다. 각 단계에서 나오는 ID·키는 채팅에 붙이지 말고,
아래 "어디에 넣나" 칸의 위치에 직접 넣는다 (비밀값은 저장소에 커밋하지 않는다).

## 0. 기본 정보 (확정)

| 항목 | 값 |
| --- | --- |
| 회사명 | JeonJa |
| 앱 이름 | 디펜스 배틀 (Defense Battle) |
| 패키지명 | `com.jeonja.defensebattle` (출시 후 변경 불가) |
| 로그인 | Google Play 로그인 필수 |
| 인터넷 | 필수 (오프라인이면 연결 안내 화면) |

## 1. Google Play Console - 새 앱
1. 앱 만들기 → 이름 "디펜스 배틀", 패키지 `com.jeonja.defensebattle`.
2. **수익 창출 → 인앱 상품**: 아래 상품 ID 로 "관리형 상품"을 만든다. 가격은 원화 가격표 확정 후 (아래 6번).
   `starter_pack`, `monthly_pass`, `ad_remove`, `season_premium`, `gold_booster_pack`,
   `offer_comeback`, `offer_arena`, `offer_chapter`, `unit_ranger`, `unit_frost`, `unit_paladin`, `unit_bomber`, `unit_assassin`,
   `chest_gold`, `chest_magic`, `chest_legend`, `crystal_100`, `crystal_550`, `crystal_1200`, `crystal_2600`, `crystal_7000`, `crystal_15000`
3. **수익 창출 설정 → 라이선스**: "Base64 인코딩 RSA 공개 키"를 복사해 둔다 → 4-4 Cloud Code Secrets 에 넣는다.
4. **Play 게임즈 서비스 → 설정**: 새 게임 만들기 → OAuth 동의 화면 → 사용자 인증 정보:
   - Android 클라이언트 (패키지명 + 업로드 키 SHA-1)
   - **웹 애플리케이션** 클라이언트 → 클라이언트 ID·보안 비밀 → 4-2 UGS 에 넣는다

## 2. AdMob - 새 앱
1. 앱 추가 → Android, Play 스토어 등록 전이면 "아니요"로 만들고 나중에 연결.
2. **보상형 광고 단위 1개** (결과 보상 2배·부활·하루 3회 뽑기권이 같은 광고 단위를 쓴다).
3. 앱 ID(`ca-app-pub-…~…`)와 광고 단위 ID(`ca-app-pub-…/…`)는 저장소에 넣지 않는다 → 7번 `release.local.json` 에 적는다.
   저장소의 `GoogleMobileAdsSettings` 는 Google 테스트 앱 ID 그대로 두고, 릴리스 빌드 동안만 실제 값으로 바뀐다.
4. 개인정보·동의 → GDPR 동의 메시지 만들기 (`Assets/Scripts/Ads/AdConsentManager.cs` 가 UMP 로 띄운다).

## 3. Unity Cloud - 프로젝트 연결 (완료 2026-11-16: 조직 anjshdkdl99, 프로젝트 ID `8dad958b-a0aa-45ae-852d-40249312c36d`, 환경 `production`)
1. <https://cloud.unity.com> → 새 프로젝트 "Defense Battle".
2. Unity 에디터 **Edit → Project Settings → Services** 에서 그 프로젝트로 연결 (조직·프로젝트 선택).

## 4. Unity Gaming Services (대시보드)
진행 (2026-11-16): Cloud Save 동작 확인(EconSync 호출 성공), Relay 사용 가능, 리더보드 `trophies` 생성(높은 순, Latest score, 리셋 없음),
Cloud Code 모듈 `DefenseBattleCloud` 배포 완료 (에디터 메뉴 **Tools → Defense Battle → Deploy Cloud Code**).
남은 것 (Play Console 이후, 사용자): 2번 Google Play 로그인, 4번 라이선스 키 Secret, 5번 기기 확인.
Economy 는 쓰지 않는다 - Unity 가 새 프로젝트의 Economy 가입을 받지 않는다 ("Economy is no longer accepting new project signups").
대신 Cloud Code 가 영수증 서명을 직접 검사한다 (`CloudCode/Module/Logic/GooglePlayReceiptVerifier.cs`).

1. **Cloud Save / Relay**: 켜기만 하면 된다 (별도 설정 없음).
2. **Authentication → Identity providers → Google Play Games**: 1-4 의 웹 클라이언트 ID·보안 비밀 입력.
3. **Leaderboards**: 새 리더보드
   - ID `trophies`, 정렬 높은 순, **업데이트 방식: 마지막 점수 유지(Keep latest)** - 트로피는 내려가기도 해서 '최고 점수'로 하면 안 된다.
4. **Cloud Code → Secrets**: 새 Secret `GOOGLE_PLAY_LICENSE_KEY` = 1-3 의 "Base64 인코딩 RSA 공개 키" (붙여 넣기만, 저장소에 커밋하지 않는다).
   서버가 이 키로 영수증 서명을 확인하고, 패키지명·상품 id·구매 완료 상태, 그리고 `obfuscatedAccountId` 가 그 플레이어의
   `PurchaseTag.ForPlayer(플레이어 id)` 인지 본다 (기기는 결제할 때 이 값을 넣는다 - `Assets/Scripts/Platform/UnityIapBackend.cs`).
   키가 없으면 모든 결제가 거절된다. 환불·취소는 알 수 없다 (필요하면 나중에 Play Developer API 로 보강).
5. **Cloud Code**: 모듈은 Unity 에디터에서 배포한다 (PC 작업).
   - 에디터 메뉴 **Tools → Defense Battle → Deploy Cloud Code** (또는 Services → Deployment 창에서 `DefenseBattleCloud.ccmr` Deploy).
     dotnet 경로가 안 잡히면 Preferences → Cloud Code 에서 `C:\Program Files\dotnet\dotnet.exe` 지정.
   - 모듈 소스: `CloudCode/Module` (게임 규칙은 클라이언트와 같은 `Assets/Scripts/Core` 를 그대로 컴파일).

## 5. 확인
에디터에서 끝난 것 (2026-11-17): Cloud Code 배포(함수 30여 개), 익명 로그인 → EconSync, 가짜 스토어 결제 창(서버가 가짜 영수증을 거절해 지급 없음).
아래는 Play Console 내부 테스트 트랙에 올린 빌드로 기기에서 확인한다 (테스트 계정을 Play Console **라이선스 테스트**에 넣으면 실제 결제 없이 산다).

먼저 Unity 에서 한 번: **Window → Google Play Games → Setup → Android setup** 에 1-4 의 리소스 XML(Play Console Play 게임즈 → 설정 → "리소스 가져오기")과
**웹 클라이언트 ID** 를 넣는다 (서버 인증 코드를 받으려면 웹 클라이언트 ID 가 있어야 한다).

1. **로그인**: 첫 실행에 Play 게임즈 로그인 창 → 로그 `[UGS] 로그인 성공`. Play 게임즈를 거절해도 익명으로 들어가져야 한다.
2. **결제**: 상점에서 `crystal_100` 구매 → 보석 +100, "구매 완료". 같은 주문이 두 번 지급되지 않는지 (상점 다시 열기·앱 재시작).
   라이선스 키 Secret 이 없거나 틀리면 "결제가 완료되지 않았어요" 가 나오고, Google 이 3일 안에 자동 환불한다 (확인하지 않은 주문).
3. **앱이 꺼진 결제**: 결제 승인 직후 앱을 강제 종료 → 다시 켜면 "결제한 상품이 지급되었어요" 와 함께 지급.
4. **한 번만 파는 상품**: `starter_pack` 을 산 뒤 다시 사려 하면 서버가 거절 (스토어엔 소모형으로 등록돼 결제 창은 뜰 수 있다 - 이때 확인하지 않아 자동 환불).
5. **기기 변경**: 기기 A 에서 진행(보석·트로피·닉네임 변경) → 기기 B(또는 앱 데이터 삭제 후)에서 같은 Google 계정으로 로그인 → 같은 PlayerId·보석·트로피·닉네임.
6. **이미 쓰던 익명 계정**: Play 게임즈 없이(로그인 거절) 플레이 → 다음 실행에 로그인 허용 → 그 익명 진행에 Play 게임즈가 연결돼 그대로 유지
   (그 Google 계정이 이미 다른 기기 진행에 연결돼 있으면 그 진행으로 바뀐다).

## 6. 원화 가격 (확정 2026-10-19)
Play Console 인앱 상품에 아래 가격으로 등록한다 (부가세 포함). 코드 값: `Assets/Scripts/Core/Config/Products.cs` (`Krw`).
효율 = 보석 환산 가치 / (가격 ÷ 12원) - 보석 100 = ₩1,200 상품이 100%.

| 상품 ID | 내용 | 가격 | 효율 |
| --- | --- | --- | --- |
| starter_pack | 보석 150 + 뽑기권 1 + 골드 부스터 1 (1회) | ₩1,200 | 300% |
| monthly_pass | 즉시 보석 300 + 30일간 매일 40 | ₩6,500 | 277% |
| ad_remove | 광고 없이 보상 + 3배속 + 보석 500 (1회) | ₩6,500 | - |
| season_premium | PvP 시즌 패스 프리미엄 트랙 | ₩5,900 | - |
| gold_booster_pack | 골드 부스터 5개 | ₩2,500 | 120% |
| offer_comeback | (24시간) 보석 220 + 뽑기권 1 + 골드 부스터 2 | ₩2,500 | 202% |
| offer_arena | (24시간) 보석 700 + 뽑기권 4 | ₩6,500 | 203% |
| offer_chapter | (24시간) 보석 600 + 뽑기권 3 + 골드 부스터 4 | ₩6,500 | 203% |
| unit_ranger | 레인저 해금 + Lv5 + 보석 200 (1회) | ₩3,900 | 277% |
| unit_frost | 빙결술사 해금 + Lv5 (1회) | ₩3,900 | 277% |
| unit_paladin | 성기사 해금 + Lv5 + 보석 300 (1회) | ₩6,500 | 277% |
| unit_bomber | 폭격수 해금 + Lv5 (1회) | ₩6,500 | 295% |
| unit_assassin | 암살자 해금 + Lv5 (1회) | ₩8,900 | 297% |
| chest_gold | 금 상자 (카드 40장+ · 영웅 1장) | ₩3,900 | - |
| chest_magic | 마법 상자 (카드 90장+ · 영웅 4장) | ₩8,900 | - |
| chest_legend | 전설 상자 (전설 카드 1장 보장) | ₩12,000 | - |
| crystal_100 | 보석 100 | ₩1,200 | 100% |
| crystal_550 | 보석 550 | ₩6,500 | 102% |
| crystal_1200 | 보석 1,200 | ₩12,000 | 120% |
| crystal_2600 | 보석 2,600 | ₩25,000 | 125% |
| crystal_7000 | 보석 7,000 | ₩65,000 | 129% |
| crystal_15000 | 보석 15,000 | ₩119,000 | 151% |

원본과 다른 점: 시즌 패스 가격 인하, 한정 상품 보상을 효율 약 200% 로 낮춤, 뽑기권 묶음(`summon_ticket_pack`)·판매 중단 상품(`mythic_ticket`)은 팔지 않음.

## 7. 출시 순서 (사용자)
코드·문서 쪽 준비는 끝 (2026-11-17). 아래는 계정·비밀값이 필요한 일이다.

1. **업로드 키스토어 만들기** - Unity **Edit → Project Settings → Player → Android → Publishing Settings → Keystore Manager** 에서 새로 만들거나
   `keytool -genkeypair -v -keystore defensebattle-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000`.
   저장소 밖에 두고 백업한다 (잃어버리면 Play Console 에서 업로드 키 재설정 요청). 예전 앱 키스토어는 쓰지 않는다.
2. **`release.local.json`** - `docs/release.local.example.json` 을 프로젝트 루트에 복사해 키스토어 경로·별칭, AdMob 앱 ID·보상형 광고 단위 ID 를 채운다 (gitignore 됨).
3. **비밀번호 환경변수** - Windows 사용자 환경변수 `DB_KEYSTORE_PASS` (키 별칭 비밀번호가 다르면 `DB_KEYALIAS_PASS`). Unity 를 다시 켜지 않아도 읽는다.
4. **Play 게임즈 설정 교체** - 1-4 에서 받은 리소스 XML·웹 클라이언트 ID 로 Unity **Window → Google Play Games → Setup → Android setup** 을 다시 한다
   (지금 `ProjectSettings/GooglePlayGameSettings.txt`·`Assets/GPGSIds.cs`·`Assets/Plugins/Android/GooglePlayGamesManifest.androidlib` 은 예전 앱 값이라 덮어써야 한다).
5. **빌드** - Unity 메뉴 **Tools → Defense Battle → Build Release AAB** → `Builds/defensebattle-v1.aab`. 값이 빠지면 빌드를 멈추고 무엇이 없는지 알려 준다.
   (비밀값 없이 빌드만 확인: **Build Test AAB** - 디버그 키·테스트 광고라 올릴 수 없다.)
6. **Play Console** - 내부 테스트 트랙에 AAB 업로드 → 1-2 인앱 상품 만들기(결제 권한이 든 빌드가 있어야 만들 수 있다) → 6번 가격 → 테스터 추가 →
   1-3 라이선스 키를 4-4 Secret 에 → 4-2 Play 게임즈 → 5번 기기 확인 + 아래 실시간 대전 확인.
7. **스토어 등록정보** - 문구 `docs/store-listing.md` (5개 언어), 그래픽 `docs/store/` (아이콘 512, 그래픽 이미지 1024x500, 스크린샷 5장),
   개인정보처리방침 URL: `docs/privacy-policy.html` 을 공개 주소에 올린다 (예: GitHub Pages). 데이터 보안 양식은 방침 2번 표대로.
8. 콘텐츠 등급 설문 · 광고 포함 · 확률형 아이템 고지(`docs/store-listing.md` 끝) → 프로덕션 출시 검토 요청.

### 실시간 대전 기기 확인 (휴대폰 2대, 내부 테스트 빌드)
1. 두 기기에서 같은 시각에 '대전 시작' → 8초 안에 서로 매칭, VS 에 상대 닉네임, 같은 순간 시작.
2. 상대 미니맵이 움직이고, 던진 몹이 상대 판에 떨어진다.
3. 한쪽 판이 무너지면 양쪽에 승패와 트로피가 서버대로 (승 +30 안팎, 패 -20).
4. 판 도중 한쪽이 '나가기' → 다른 쪽 승리. 한쪽이 앱을 내리고 20초 넘게 있으면 남은 쪽 승리, 돌아온 쪽은 패배 결과.
5. 한 기기만 '대전 시작' → 8초 뒤 봇 대전.

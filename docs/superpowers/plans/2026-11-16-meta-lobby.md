# 3단계 메타 (로비·덱·상자·연구·퀘스트·로드·시즌·상점·설정·튜토리얼·5개 언어) Implementation Plan

> 실행: Native (superpowers:executing-plans). 기록: `.superpowers/sdd/2026-11-16-meta-lobby/progress.md`. 각 Task 는 테스트 → 구현 → 플레이 확인 → 커밋 → push.

**Goal:** 새 계정으로 튜토리얼(1-1) → 캠페인 → 덱 편성 → 상자 열기 → 레벨업까지 막힘 없이 (spec 3단계 완료 기준). 3단계부터 출시까지 이어서 진행한다.

**Architecture (사용자 결정 2026-11-16: 메타는 처음부터 서버로):**
- 경제(보석·카드·레벨·상자·연구·덱·별·업적 수령·시즌·트로피)는 서버(UGS Cloud Code `DefenseBattleCloud`) 권위. 기기는 `EconReply` 로 받은 `EconState` 사본(`EconCache`)을 보여 주기만 한다. 오프라인 저장본은 만들지 않는다.
- 원본이 기기에만 두던 것(일일 퀘스트 진행, 최고 웨이브, 본 기능 NEW, 튜토리얼 단계, 제안 표시 시각, 스테이지 연속 실패, 언어·음량)은 `LocalMeta`(PlayerPrefs JSON).
- 씬: Battle.unity 하나 (원본 SPA 처럼 로비 오버레이 ↔ 전투). Boot(접속) 도 오버레이. — Ruling (spec 의 Boot/Lobby/Battle 3씬에서 변경).
- 로비·페이지 UI 는 원본 menu.ts 처럼 열 때 코드로 만든다 (`UiKit` 런타임 도우미 + SpriteCatalog 의 프레임·아이콘). 매 프레임 갱신 없음 → 열 때만 할당.
- 다국어: 원본 `i18n/*.ts` 표를 JSON 으로 바꿔 `Resources/Loc/{lang}.json`, `Loc.Translate` 에 연결. 씬에 고정된 한국어 라벨은 `LocText` 컴포넌트로 실행 중 번역 (2단계 리뷰 경미 7).

## Tasks
1. **Net/Data**: `CloudClient`(UGS 로그인 + `CallModuleEndpointAsync`, 서버 `Json.Settings` 와 같은 Newtonsoft 설정), `EconCache`(EconState 사본 + Changed 이벤트), `LocalMeta`(일일 초기화 포함), Boot 오버레이(로딩 배경·진행·연결 실패 → 다시 시도). 테스트: 서버 JSON 모양 그대로 EconReply 역직렬화, LocalMeta 날짜 바뀌면 일일 초기화.
2. **다국어**: 변환 스크립트(tools/i18n_to_json.py) → `Assets/Resources/Loc/*.json`; `LocTable`(키 조회·@@ 문맥·없으면 원문); 언어 저장·시스템 언어 기본값; `LocText` 컴포넌트; ja/zh 폰트 폴백(FusionPixel SDF). 테스트: 조회·폴백·문맥.
3. **UiKit + 로비 뼈대**: 로비 상단(닉·티어·트로피, 보석, 설정), 제목·덱 행렬, 아레나 카드(로드 바·다음 보상·대전 시작(4단계)·시즌 패스·전적·상자 4칸), 모드 카드(캠페인 다음 스테이지, 무한 최고 웨이브), 하단 메뉴(상점·덱·연구·퀘스트·로드·랭킹) 해금·NEW·점. 해금 규칙은 progress.ts 그대로. 테스트: featureUnlocked / nextStageId / 로드 다음 단계.
4. **판 연결**: 스테이지 선택(챕터·별·별 보상)·스테이지 정보·무한 정보 → 메타 옵션(덱·연구·유닛 레벨·DeckMin·골드 부스터)으로 시작, RunStart/RunEnd, 결과(보상·첫 클리어·해금 유닛·다음 스테이지·2배 광고), 실패 연속, 일일 퀘스트 진행·최고 웨이브·업적 수령(Game 이벤트), 3배속(광고 제거).
5. **덱**: 유닛 32 카드(보유·레벨·카드 수·희귀도), 상세(능력치·레벨업 UpgradeUnit·보석 해금 BuyUnit), 덱 4칸 편성(SetDecks), 몹 카드 덱, 추천 덱.
6. **상자·뽑기·연구**: 상자 칸(열기 시작·열기·보석으로 즉시), 공개 연출(카드 한 장씩), 뽑기(1·10회·뽑기권), 확률표, 연구 5종.
7. **퀘스트·로드·시즌·월정액·프로필·설정·랭킹**: 일일 퀘스트·업적 수령, 트로피 로드, 시즌 패스(무료·프리미엄), 월정액 받기, 닉네임(SetNick), 설정(언어·음악·효과음·튜토리얼 다시), 랭킹(Leaderboard).
8. **상점·특가**: 상품 목록(Products), 특가 팝업(offers.ts 조건), 구매 버튼 → 5단계 IAP 로 (지금은 안내).
9. **튜토리얼·팁**: 1-1 코치(tutorial.ts), 로비 안내(maybeGuide), 판 안 팁(빈틈·늦은 웨이브), 처음 3판 약한 봇은 4단계.
10. **확인**: 새 계정(PlayerPrefs·UGS 익명 계정 초기화)으로 튜토리얼 → 캠페인 → 덱 → 상자 → 레벨업 흐름, 콘솔 에러 0, 할당 점검, spec 갱신, push.

## Review Focus
1. 서버 호출 실패·지연 (오프라인·시간 초과): 버튼 두 번 누름 → 중복 요청, 화면이 멈추지 않고 안내.
2. 서버 응답 후 화면이 새 EconState 로 다시 그려지는지 (보석·카드·상자 수 어긋남).
3. 날짜가 바뀌는 순간 (일일 퀘스트·월정액·광고 횟수).
4. 긴 언어(영어·일본어)에서 글자 넘침.
5. 새 계정 첫 진입 (빈 저장본, 튜토리얼 전 로비).

# 출시 준비 (7단계) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 새 앱으로 올릴 수 있는 상태: 옛 앱의 ID·키스토어 흔적 제거, 비밀값은 저장소 밖에서 빌드 때만 주입, 아이콘·스토어 그림·스크린샷·5개 언어 문구·개인정보처리방침, 릴리스 AAB 빌드 확인, 문서 갱신.

**Spec:** `docs/superpowers/specs/2026-10-19-defense-battle-port-design.md` (7. 출시 준비)

## Global Constraints
- 키스토어·비밀번호·광고 ID 비밀값은 커밋하지 않는다 (CLAUDE.md). 빌드 때 환경변수/로컬 파일(gitignore)에서 읽는다.
- 사용자만 할 수 있는 것(AdMob 앱·광고 단위 만들기, Play Console, 업로드 키스토어 만들기, 개인정보처리방침 호스팅, 업로드)은 문서로 순서를 남긴다.
- 원본 그림(`../DefenseBattle/launch/store`)을 쓴다. 새 그림을 지어내지 않는다.

## Review Focus
1. 로컬 설정 파일이 없을 때 개발 빌드는 Google 테스트 광고로 돌고, 릴리스 빌드는 멈춘다 (테스트 광고로 출시되지 않게).
2. 빌드가 끝나거나 실패해도 주입한 실제 ID 가 저장소 파일에 남지 않는다.
3. 옛 앱 ID·키스토어 경로가 ProjectSettings·코드에 남지 않는다.

---

### Task 1: 릴리스 설정을 저장소 밖으로
**Files:** Create `Assets/Scripts/Ads/AdUnits.cs` (런타임: `Resources/AdUnits` TextAsset 이 있으면 그 보상형 ID, 없으면 Google 테스트 ID; 개발 빌드는 늘 테스트 ID), Modify `AdMobRewardedAdService.cs` (옛 ID 삭제), `Assets/Scripts/Editor/ReleaseBuilder.cs` (메뉴 Tools/Defense Battle, `release.local.json`(gitignore) 또는 환경변수 `DB_KEYSTORE_PATH/DB_KEYSTORE_PASS/DB_KEYALIAS/DB_KEYALIAS_PASS/DB_ADMOB_APP_ID/DB_ADMOB_REWARDED_ID` 읽기, 빌드 동안만 AdMob 앱 ID·`Assets/Resources/AdUnits.txt` 주입 후 finally 로 되돌림, 값이 없으면 빌드 거부, `Tools/Defense Battle/Build Test AAB` = 디버그 키·테스트 광고로 빌드 확인), `.gitignore` (`release.local.json`, `Assets/Resources/AdUnits.txt*`), ProjectSettings (옛 키스토어 경로·별칭 지움, AdMob 앱 ID = Google 테스트 앱 ID), `docs/release.local.example.json`.
- 테스트: `AdUnitsTests` (`Parse` 가 빈 값·주석을 거르고 ID 를 꺼냄, 없으면 테스트 ID).

### Task 2: 아이콘·스토어 그림·스크린샷
**Files:** `Tools/store_art.py` (PIL: 원본 thumbnail.jpg → 앱 아이콘 512·런처 아이콘(제목 없는 가운데), graphic_1024x578 → 그래픽 이미지 1024x500), 결과 `docs/store/` + `Assets/Art/DefenseBattle/Icon/*.png` → Player Settings Android 아이콘(Adaptive 배경·Legacy·Round). 스크린샷: 에디터 플레이 1080x1920 (로비·전투·대전·덱·상점·상자) → `docs/store/screenshots/ko/`.

### Task 3: 스토어 문구·개인정보처리방침
**Files:** `docs/store-listing.md` (일본어·간체·번체 추가, 실시간 대전 반영), `docs/privacy-policy.md` + `.html` (디펜스 배틀, JeonJa, UGS(Authentication·Cloud Save·Cloud Code·Relay·Leaderboards)·Google Play 게임즈·결제·AdMob, 계정 삭제 요청 방법, 5개 언어 중 한·영).

### Task 4: 빌드 확인
- `Build Test AAB` 로 Android AAB 빌드 성공(디버그 키) - 크기·경고 기록. 실패하면 고친다.

### Task 5: 문서·마무리
- README 갱신(지금 상태·폴더·빌드 방법), `docs/ugs-setup.md` 출시 순서(사용자 할 일 체크리스트: AdMob → Play Console → 키스토어 → release.local.json → 빌드 → 내부 테스트 → 기기 확인 목록 → 심사), spec 진행표 7단계. 최종 리뷰(opus), push.

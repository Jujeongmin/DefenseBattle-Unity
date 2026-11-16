# 2단계 전투 화면 (싱글) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 새 `Battle.unity` 에서 캠페인 1-1 과 무한 모드를 처음부터 끝까지 플레이할 수 있게 한다 (전장·유닛·몹·이펙트·HUD·영웅 관리·구역 교환·결과/부활).

**Architecture:** `BattleRunner` 하나가 `Game.Update` 를 0.05초 단위로 부르고, View 들은 그 뒤 매 프레임 `Game` 의 공개 상태를 다시 읽어 그린다 (Core 객체를 프레임 넘어 들고 있지 않음, Id 로 짝짓기).
원본 `render.ts` 의 캔버스 도형(원·고리·타원·선·그라데이션)은 할당 없이 매 프레임 다시 채우는 동적 메시 `ShapeBatch` 로, 스프라이트는 풀링한 `SpriteRenderer` 로, 글자는 풀링한 TextMeshPro 로 그린다.
HUD 는 uGUI + TMP, 씬과 고정 UI 는 에디터 빌더 스크립트(메뉴 실행)로 만든다 (기존 `RetentionUIBuilder` 와 같은 방식 - 씬 YAML 을 손으로 고치지 않음).

**Tech Stack:** Unity 6000.4.0f1, 2D URP, uGUI, TextMeshPro, NUnit EditMode (Unity Test Runner).

**Spec:** `docs/superpowers/specs/2026-10-19-defense-battle-port-design.md` (2단계, "씬 구성안"). 원본: `../DefenseBattle/game/src/render.ts`, `ui.ts`, `main.ts`, `style.css`, `index.html`.

## Global Constraints

- `CLAUDE.md` 의 최적화 규칙: 매 프레임 코드에서 `new`·LINQ·캡처 람다·박싱 foreach·문자열 조합 금지, 버퍼는 필드로 두고 `Clear()` 재사용. `Instantiate`/`Destroy` 를 전투 중에 부르지 않는다 (풀).
- View/UI 에 게임 규칙을 두지 않는다. 입력은 Core 명령(`Summon`, `Merge`, `SellMany`, `Upgrade`, `SwapZones`, `Revive`)으로만.
- 원본 숫자에는 출처 주석 (`// render.ts UNIT_DRAW`). 화면 문구는 `Loc.Tl("한국어 원문")`.
- 씬·프리팹은 에디터 스크립트/MCP 로 만든다 - `.unity`/`.prefab` 손 편집 금지.
- 스크립트를 고칠 때마다 컴파일 대기 → 콘솔 에러/경고 확인. 직렬화 필드 `m_` + `[SerializeField]`, 한국어 `/// <summary>`, 한 클래스 한 파일.
- 좌표: 논리 720×720 (원점 좌상, y 아래로) → 월드 32px = 1 유닛, 전장 중심 = 월드 원점: `wx = x/32 - 11.25`, `wy = 11.25 - y/32`.
- 카메라: Orthographic, Pixel Perfect Camera 는 쓰지 않는다 - 유닛을 원본처럼 62px(32px 시트의 1.94배)로 그려 정수 배율이 안 맞는다. 번짐은 Point 필터로 막는다 (spec 의 "Pixel Perfect Camera" 에서 바꾼 점).
- 실시간 1초 = 게임 `Waves.PlaySpeed`(2)초. 진행: 실제 dt 를 0.1초로 자르고 × PlaySpeed, 0.05 단위로 나눠 `Update` (`main.ts step`).
- 이번 단계는 메타(저장·보상)가 없다: 덱 = `Units.Starter`, 연구 0, 유닛 레벨 1, 보상 표시는 하지 않음 (3·5단계). 오디오는 이번 범위 밖 (후속 작업).

## Review Focus

1. **풀 재사용**: 엔진이 죽은 `Mob` 객체를 다른 Id 로 다시 쓴다 → 이전 몹의 스프라이트·체력바·상태색이 새 몹에 남으면 안 된다. (Task 4 테스트 `MobViews_FollowIdsNotObjects`)
2. **긴 정지 후 복귀** (앱 백그라운드 → 복귀, 에디터 일시정지): 한 프레임에 몇 초가 몰려 몹이 순간이동·대량 스폰되면 안 된다 → 실제 dt 0.1초 상한. (Task 3 `BattleClock_ClampsLongFrames`)
3. **소환 길게 누르기**: 골드가 떨어지거나 손가락이 버튼 밖에서 떼어져도 연속 소환이 멈춰야 한다. 판이 끝나면 멈춘다. (Task 6 `SummonHold_StopsWhenUnaffordableOrReleased`)
4. **구역 드래그 중 이탈**: 손가락이 전장 밖으로 나가거나 두 번째 손가락·판 종료·패널 열림이 생기면 드래그가 취소되고 교환은 일어나지 않는다. (Task 7 `ZoneDrag_CancelsOutsideBoard`)
5. **화면비**: 20:9 휴대폰과 4:3 태블릿 모두 전장이 위/아래 UI 와 겹치지 않고 정사각형 그대로 들어가야 한다 (Safe Area 포함). (Task 3 `BoardFit_SquareBetweenBars`)

---

## 파일 구조

```
Assets/Scripts/View/                      asmdef DefenseBattle.View (Core, Unity.TextMeshPro, UnityEngine.UI 참조)
  BoardSpace.cs        논리 좌표 ↔ 월드, 전장 맞춤(카메라 크기·위치)
  BattleClock.cs       dt 자르기·0.05 분할 (순수 계산, 테스트용)
  BattleLaunch.cs      어떤 판을 열지 (stage id / endless) → GameOptions
  BattleRunner.cs      MonoBehaviour: Game 생성·진행·이벤트 → View 들 호출 순서
  ShapeBatch.cs        동적 메시 도형 그리기 (원·고리·타원·사각·선·호·삼각·방사 그라데이션)
  SpriteCatalog.cs     ScriptableObject: 시트 이름 → Sprite[12], 타일·아이콘
  SpriteAnim.cs        걷기/공격 프레임 선택 (render.ts WALK_SEQ, ATTACK_SEQ)
  BoardView.cs         배경 스프라이트, 구역·빈틈·불 장판·드래그 화살표 (ShapeBatch 아래층)
  ActorViews.cs        유닛·몹 SpriteRenderer 풀 (Id 짝짓기, 정렬), 오라·상태색·체력바 (ShapeBatch)
  EffectsView.cs       이펙트 11종 + 화면 흔들림 + 보스바
  FloatingTextPool.cs  데미지/골드 숫자 (월드 TMP 풀)
  ZoneDragInput.cs     구역 끌어 교환 (상태 = View 쪽, 엔진엔 SwapZones 만)
Assets/Scripts/UI/Battle/                 같은 asmdef
  BattleHud.cs         상단바·골드·진행바·소환·배속·일시정지·로그·배너·보스 타이머 (값 바뀔 때만 갱신)
  SummonButton.cs      탭 1회 / 길게 누르면 0.38초 뒤 0.14초마다
  UnitStacksView.cs    덱 카드 4장 (수량·최고 등급 테두리·상성 ▲▼·Lv·강화)
  HeroPanel.cs         영웅 관리 (합성/판매, 수량 1·10·ALL)
  ResultPanel.cs       결과·부활
  LaunchPanel.cs       임시 시작 화면 (1-1 / 무한) - 3단계 로비가 대신하면 지운다
Assets/Scripts/Editor/Battle/
  BattleAssetsBuilder.cs   SpriteCatalog·필드 배경 PNG 굽기·Galmuri TMP 폰트·아이콘 TMP 스프라이트 에셋
  BattleSceneBuilder.cs    Battle.unity 만들기 (카메라·캔버스 3개·HUD 계층·참조 연결), 다시 실행해도 같은 결과
Assets/Tests/EditMode/View/               asmdef DefenseBattle.View.Tests
```

---

### Task 1: View 어셈블리 + 에셋 준비 (카탈로그·배경·폰트·아이콘)

**Files:**
- Create: `Assets/Scripts/View/DefenseBattle.View.asmdef`, `View/SpriteCatalog.cs`, `Editor/Battle/BattleAssetsBuilder.cs`
- Create: `Assets/Tests/EditMode/View/DefenseBattle.View.Tests.asmdef`, `View/AssetsTests.cs`
- 산출물: `Assets/Art/DefenseBattle/Generated/SpriteCatalog.asset`, `field_bg.png`, `Galmuri11 SDF.asset`, `Galmuri11-Bold SDF.asset`, `Icons.asset`(TMP Sprite Asset)

**Interfaces:**
- Produces: `SpriteCatalog.Unit(Job job, int grade) : Sprite[]` (12장, 인덱스 = 방향*3 + 열), `SpriteCatalog.Mob(Race race, int variant) : Sprite[]` (없는 변형이면 `_0`), `SpriteCatalog.Field : Sprite`, `SpriteCatalog.Icon(string name) : Sprite`, `SpriteCatalog.Coin`.

- [ ] **Step 1: 실패하는 테스트** `AssetsTests`:
  - `Catalog_HasEverySheet`: `Units.All` 32 × 등급 6 모두 12장, `Races.Variants` 만큼 몹 시트 + 카드 전용 변형(`Attacks.All` 의 Race/Variant)도 12장.
  - `Catalog_UnknownMobVariantFallsBackToZero`: `Mob(Race.Troll, 99)` == `Mob(Race.Troll, 0)`.
  - `IconSpriteAsset_CoversLocIcons`: 원본 HUD 가 쓰는 이름 `coin, timer, pause, play, sound, mute, helmet, home, hammer, up, down, star, tv, warning, close` 가 TMP 스프라이트 에셋에 있다.
  - `Field_Is720Square`: 배경 스프라이트 rect 720×720, PPU 32.
- [ ] **Step 2: Test Runner 실행 → 실패 확인** (`run_tests` EditMode, assembly `DefenseBattle.View.Tests`).
- [ ] **Step 3: `BattleAssetsBuilder` 메뉴 `Tools/Defense Battle/Build Battle Assets`**:
  - 카탈로그: `Art/DefenseBattle/Sprites/*.png` 의 잘린 스프라이트(`{시트}_{방향}_{열}`)를 순서대로 담는다.
  - 배경: `render.ts drawTiles + decorate` 를 그대로 옮겨 720×720 PNG 로 굽는다 (TILE 45, SRC 64, 흙길 밴드 70±30, `autoTile`, `hash2` 장식 40%, 비네트 반경 0.25→0.75 알파 0.28). 타일 PNG 는 파일 바이트를 `LoadImage` 로 읽는다.
  - 폰트: `TMP_FontAsset.CreateFontAsset(ttf, 24, 4, GlyphRenderMode.RASTER_HINTED, 1024, 1024, AtlasPopulationMode.Dynamic)` (픽셀 폰트라 Raster, 크기 12의 배수).
  - 아이콘: `UI/Icons/*.png` 로 TMP Sprite Asset (`Loc.Icon` 의 `<sprite name="x">` 와 이름 일치). TMP 기본 Sprite Asset 으로 지정.
- [ ] **Step 4: 메뉴 실행 후 테스트 통과 확인**, 콘솔 에러 0.
- [ ] **Step 5: 커밋** `Add battle asset catalog, baked field background, Galmuri TMP fonts and icon sprites`

### Task 2: BoardSpace + ShapeBatch

**Files:** Create `View/BoardSpace.cs`, `View/ShapeBatch.cs`; Test `Tests/EditMode/View/ShapeBatchTests.cs`, `BoardSpaceTests.cs`

**Interfaces:**
- Produces: `BoardSpace.ToWorld(double x, double y) : Vector3`, `BoardSpace.ToLogical(Vector3 world) : Vector2`, `BoardSpace.Px : float` (= 1/32).
- Produces: `ShapeBatch : MonoBehaviour` (MeshFilter+MeshRenderer, `Sprite-Unlit-Default` 머티리얼, 정렬 레이어/순서 직렬화):
  `Begin()`, `End()` (메시 업로드), `FillCircle(x, y, r, Color32)`, `StrokeCircle(x, y, r, width, Color32)`, `FillEllipse(x, y, rx, ry, Color32)`, `StrokeEllipse(x, y, rx, ry, width, Color32)`, `FillRect(x, y, w, h, Color32)`, `Line(x1, y1, x2, y2, width, Color32)`, `StrokeArc(x, y, r, a0, a1, width, Color32)`, `FillTriangle(Vector2 a, b, c, Color32)`, `RadialCircle(x, y, r, Color32 inner, Color32 outer, float innerFrac)`, `VerticalGradientRect(x, y, w, h, Color32 top, Color32 bottom)`. 인자는 전부 논리 px.
  세그먼트 수는 반지름에 따라 12~48.

- [ ] **Step 1: 실패하는 테스트**
  - `ToWorld_MapsCornersAndCenter`: (0,0)→(-11.25, 11.25), (360,360)→(0,0), (720,720)→(11.25,-11.25); `ToLogical(ToWorld(p)) == p`.
  - `ShapeBatch_CountsVertices`: `FillCircle` 하나(r=20) → 세그먼트+1 정점; `Line` → 4 정점; `Begin()` 하면 0.
  - `ShapeBatch_NoAllocAfterWarmup`: 같은 도형 200개를 두 번째 프레임에 다시 그릴 때 `GC.GetAllocatedBytesForCurrentThread()` 증가 0.
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현** - 정점/색/인덱스는 `List<Vector3>`·`List<Color32>`·`List<int>` 필드, `mesh.SetVertices/SetColors/SetTriangles(list)` + `MarkDynamic()`.
- [ ] **Step 4: 통과 확인**
- [ ] **Step 5: 커밋** `Add BoardSpace mapping and allocation-free ShapeBatch`

### Task 3: Battle 씬 뼈대 + BattleRunner + 전장 맞춤 + BoardView

**Files:** Create `View/BattleClock.cs`, `View/BattleLaunch.cs`, `View/BattleRunner.cs`, `View/BoardView.cs`, `Editor/Battle/BattleSceneBuilder.cs`; Test `BattleClockTests.cs`, `BoardFitTests.cs`; 산출물 `Assets/Scenes/Battle.unity` (Build Settings 맨 앞)

**Interfaces:**
- Produces: `BattleClock.Steps(float realDt, double playSpeed, List<double> outSteps)` (0.1초 상한 × PlaySpeed, 0.05 조각).
- Produces: `BattleLaunch.Current : BattleLaunch` (`Kind` Stage|Endless, `StageId`), `BattleLaunch.MakeOptions() : GameOptions` (Stage: `GameOptions.ForStage(Campaign.Get(id))`; 둘 다 Deck = `Units.Starter`, `UseResearch = false`).
- Produces: `BattleRunner.Game : Game`, `event Action<Game> GameStarted`, `event Action<GameOverData> GameOver`, `Restart()`, `Exit()`. 프레임 순서: Update → `BoardView.Draw` → `ActorViews.Draw` → `EffectsView.Draw` → `BattleHud.Refresh` (Runner 가 직접 부른다, 각자 Update 금지).
- Produces: `BoardSpace.FitCamera(Camera cam, RectTransform battlefieldArea)` - 전장(22.5 유닛 정사각)이 위·아래 바 사이 영역에 꽉 맞게 orthographicSize·위치 설정.

- [ ] **Step 1: 실패하는 테스트**
  - `BattleClock_SplitsInto005Steps`: realDt 0.04, PlaySpeed 2 → [0.05, 0.03].
  - `BattleClock_ClampsLongFrames`: realDt 5 → 합계 0.2 (0.1×2), 조각 4개.
  - `BoardFit_SquareBetweenBars`: 화면 1080×2400 (20:9), 영역 = 위 220px·아래 760px 을 뺀 칸 → 전장의 화면 높이·폭이 같고 칸 안에 들어간다; 1536×2048 (4:3) 도 같은 조건.
  - `Launch_StageOptions`: `Stage "1-1"` → `WaveCount == Campaign.Get("1-1").Waves`, `StageId == "1-1"`; Endless → `StageId == null`, `WaveCount == 0`.
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현**
  - `BoardView`: 배경 SpriteRenderer(정렬 0) + ShapeBatch "Under"(정렬 1): 구역 삼각형(직업 tint 알파 0.10, 없으면 흰 0.02, 테두리 rgba(20,20,20,0.18) 2px), 빨간 빈틈(`Game.PathGaps(buffer)` 를 실시간 0.4초마다, 24px 사각 알파 0.22±0.08 sin(t·4)), 불 장판(`render.ts drawBurns`).
  - `BattleSceneBuilder` 메뉴 `Tools/Defense Battle/Build Battle Scene`: Main Camera (Orthographic, 배경 #1d1b2e), Runner, Board 루트, ShapeBatch 층 4개(Under 1 / MobOver 11 / Over 30 / Top 40), 캔버스 3개 - `HudDynamic`(값 바뀌는 HUD), `HudStatic`(프레임·버튼), `Overlay`(패널) - Screen Space Overlay, 기준 해상도 720×1280, Match 0.5, Safe Area(`MobileSafeAreaLayout` 재사용).
- [ ] **Step 4: 통과 확인** + 플레이 모드: 배경·구역 보이고 웨이브가 진행(콘솔로 `WaveStarted` 확인), 에러 0.
- [ ] **Step 5: 커밋** `Add Battle scene, BattleRunner loop and board background`

### Task 4: 유닛·몹 스프라이트 (ActorViews)

**Files:** Create `View/SpriteAnim.cs`, `View/ActorViews.cs`; Test `SpriteAnimTests.cs`, `ActorViewsTests.cs`

**Interfaces:**
- Consumes: `SpriteCatalog`, `ShapeBatch`, `BoardSpace`.
- Produces: `SpriteAnim.WalkCol(double t, double phase, double period) : int` (`WALK_SEQ = {1,0,1,2}`), `SpriteAnim.AttackCol(double flash) : int` (`ATTACK_SEQ = {1,2,0,1}`, `ATTACK_ANIM_DUR = 0.1`), `SpriteAnim.FrameIndex(int dir, int col) = dir*3 + col`.
- Produces: `ActorViews.Draw(Game g, double t, ShapeBatch under, ShapeBatch over)`; 내부 풀 `Dictionary<int, SpriteRenderer>` (Id → 렌더러), 이번 프레임에 없는 Id 는 풀로.

- [ ] **Step 1: 실패하는 테스트**
  - `WalkCol_FollowsSequence`, `AttackCol_SwingsThenIdle` (flash 0.1 → 1, 0.074 → 2, 0.049 → 0, 0 → 1).
  - `MobViews_FollowIdsNotObjects`: 몹 Id 5 그린 뒤 같은 `Mob` 객체를 Id 9·다른 종족으로 바꿔 그리면 Id 5 렌더러는 풀로 돌아가고 Id 9 는 새 종족 스프라이트·`HitFlash` 0 상태 (헤드리스: `ActorViews` 를 GameObject 에 붙여 직접 호출).
  - `ActorViews_NoAllocSteadyState`: 몹 60·유닛 20 상태에서 두 번째 Draw 의 할당 0.
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현** (`render.ts drawMobs/drawMobAura/drawUnits/drawUnitAuras/drawUnitSparkles` 그대로)
  - 유닛 크기 `UNIT_DRAW = 62`px, 몹 크기 `m.Size`. 정렬: 몹 `sortingOrder` 10, 유닛 20 (원본처럼 몹 전부 → 유닛 전부). 같은 층 안은 URP 2D Renderer 의 Transparency Sort Mode = Custom Axis (0,1,0) 로 아래쪽이 앞 (씬 빌더가 렌더러 에셋에 설정).
  - 투명도: 피격 0.65, 유령 0.6±0.1, 은신 ×0.22. 피격 흰 번쩍임(`lighter` 0.35)은 같은 프레임 스프라이트를 가산 머티리얼 렌더러로 겹쳐 그린다 (몹 풀에 보조 렌더러 1개).
  - 오라·그림자(Under), 상태색·체력바·둔화 화살표·이름표(BOSS/거인·킹·마왕 - `AttackDef.HpMult >= 5 && Gold == null`) (MobOver). 이름표 글자는 `FloatingTextPool` 의 고정 라벨 칸 사용.
- [ ] **Step 4: 통과 확인** + 플레이 모드에서 소환(임시: 콘솔 `execute_code` 로 `Game.Summon()`)한 유닛이 공격 스윙, 몹이 걷기·코너 정지.
- [ ] **Step 5: 커밋** `Draw units and mobs with pooled sprites, auras and health bars`

### Task 5: 이펙트·숫자·흔들림·보스바 (EffectsView)

**Files:** Create `View/EffectsView.cs`, `View/FloatingTextPool.cs`; Test `EffectsViewTests.cs`

**Interfaces:**
- Produces: `EffectsView.Draw(Game g, double t, ShapeBatch over, ShapeBatch top)`, `FloatingTextPool.Show(int slot, string text, Vector2 logical, float size, Color32 fill, float alpha)` (프레임마다 `BeginFrame()`→`Show`…→`EndFrame()` 로 안 쓴 칸 숨김, TMP `SetText` 는 값이 바뀔 때만), `EffectsView.ShortNumber(double v, char[] buffer) : int` (`12.3K`, `4.5M` - `render.ts short`, 문자열 할당 없이).

- [ ] **Step 1: 실패하는 테스트**
  - `ShortNumber_MatchesOriginal`: 999→"999", 12345→"12.3K", 4_500_000→"4.5M".
  - `Effects_EveryTypeDrawsWithoutThrow`: `EffectType` 11종을 하나씩 넣은 가짜 리스트로 Draw.
  - `Effects_NoAllocSteadyState`: 이펙트 240개(상한) 두 번째 Draw 할당 0.
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현** `render.ts drawEffects/drawProj/drawBurst/drawDrop/drawBossBar` 를 ShapeBatch 호출로 1:1 (값·색 그대로). 화면 흔들림: `g.Shake > 0.2` 면 Board 루트를 ±shake/2 px 무작위 이동 (UI 는 안 흔듦). 보스바: 전장 위쪽 14px, 폭 62%, 빨강→주황, `BOSS  {pct}%   {초}s` (TMP 1칸).
- [ ] **Step 4: 통과 확인** + 플레이 모드 눈 확인 (화살·마법구·검기·폭발·데미지 숫자·골드 +N).
- [ ] **Step 5: 커밋** `Draw projectiles, hit effects, damage numbers, shake and boss bar`

### Task 6: HUD (상단바·골드·진행바·소환·덱 카드)

**Files:** Create `UI/Battle/BattleHud.cs`, `UI/Battle/SummonButton.cs`, `UI/Battle/UnitStacksView.cs`; Modify `Editor/Battle/BattleSceneBuilder.cs` (HUD 계층); Test `HudTests.cs`

**Interfaces:**
- Consumes: `BattleRunner.Game`.
- Produces: `BattleHud.Refresh(Game g)` (매 프레임, 값이 바뀐 것만 `SetText`), `SummonButton` (`OnPointerDown/Up/Exit`, `Tick(float realDt)`), `UnitStacksView.Rebuild(Game g)` (`RosterChanged`·종족 변경·합성 가능 목록 변경 때만), `UnitStacksView.RefreshButtons(Game g)` (강화 버튼 활성만, 매 프레임).

- [ ] **Step 1: 실패하는 테스트**
  - `SummonHold_StopsWhenUnaffordableOrReleased`: 누르고 0.38초 → 1회, 이후 0.14초마다 1회; 골드 < 비용이 되면 멈추고 다시 골드가 생겨도 손을 떼기 전엔 재개 안 함; `OnPointerExit` 도 멈춤; `Game.Over` 면 멈춤.
  - `Tap_SummonsOnce_HoldReleaseDoesNotDoubleCount`: 길게 눌러 연속 소환 후 뗄 때 click 으로 1회가 더 생기지 않는다 (원본 `held` 플래그).
  - `Hud_TextOnlyUpdatesOnChange`: 같은 상태로 Refresh 두 번 → TMP `SetText` 호출 카운터 증가 0 (래퍼로 측정).
  - `WaveLabel_Format`: 스테이지 `Wave 3/10`, 무한 `Wave 3` (원본 `ui.ts update`).
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현** (`index.html` 순서·`style.css` 색)
  - 상단: 몹 초상화(`Mob(race,0)` 정면 프레임)·종족명 / `stage-label` (1-1 / `Loc.Tl("무한")`) · Wave · 보스 타이머(`{timer} {초:0.0}s`, `BossTimer/PlaySpeed`) · 소리(임시: `AudioListener.volume` 토글)·일시정지(`Game.Paused`)·배속(`[1, 2]` 순환, 3배속은 광고 제거 후 - 3단계).
  - 전장 위 오버레이: 소환 로그 6줄, 골드 배지(부족하면 1.3배·빨강 0.15초), 신화 배너.
  - 진행바: `Mobs.Count/100`, 70% 이상 경고색, `n / 100`.
  - 하단: 영웅 버튼(합성 가능 점) · 소환 버튼(`소환` + `{coin}{SummonCost}`, 골드 부족·종료 시 비활성) · 나가기 / 덱 카드 4장.
- [ ] **Step 4: 통과 확인** + 플레이 모드: 소환·강화·배속·일시정지 동작, 에러 0.
- [ ] **Step 5: 커밋** `Add battle HUD with hold-to-summon and deck stack cards`

### Task 7: 영웅 관리 패널 + 구역 끌어 교환

**Files:** Create `UI/Battle/HeroPanel.cs`, `View/ZoneDragInput.cs`; Modify `BoardView.cs` (드래그 하이라이트·화살표·교환 표시), `BattleSceneBuilder.cs`; Test `ZoneDragTests.cs`, `HeroPanelTests.cs`

**Interfaces:**
- Produces: `ZoneDragInput.ZoneAt(Vector2 logical) : int` (-1 = 없음, `ui.ts zoneAt`), `ZoneDragInput.State : DragState? { From, To, X, Y }` (BoardView 가 읽음), `HeroPanel.Open(Job? job, bool mergeMode)`.

- [ ] **Step 1: 실패하는 테스트**
  - `ZoneAt_Quadrants`: (360,100)→0 위, (620,360)→1 오른, (360,620)→2 아래, (100,360)→3 왼, 정대각선 (100,100)→-1, 전장 밖 →-1.
  - `ZoneDrag_SwapsOnRelease`: 0 에서 눌러 2 에서 떼면 `SwapZones(0,2)` 1회 (`Game.Zones[0].Job` 이 바뀜).
  - `ZoneDrag_CancelsOutsideBoard`: 전장 밖에서 떼기·두 번째 터치·`Game.Over`·패널 열림 → 교환 없음, `State == null`.
  - `HeroPanel_SellQty`: 1·10·ALL 이 `SellMany(job, grade, qty)` 로 (ALL = 그 등급 수량), 합성 모드에서 3장 미만이면 `Loc.Tl("같은 등급 {0}개가 필요해요", 3)` 토스트.
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현** - 드래그 그리기는 `render.ts drawZones/drawDrag/drawSwapMark` (개미행렬 점선은 ShapeBatch 짧은 선 조각, 오프셋 -t·40). 패널은 `ui.ts openHeroes` 구성 (직업 탭·합성 점·등급 카드 6장·모드 탭), `RosterChanged` 때 다시 그림.
- [ ] **Step 4: 통과 확인** + 플레이 모드 확인.
- [ ] **Step 5: 커밋** `Add hero merge/sell panel and drag-to-swap zones`

### Task 8: 결과·부활 + 임시 시작 화면

**Files:** Create `UI/Battle/ResultPanel.cs`, `UI/Battle/LaunchPanel.cs`; Modify `BattleRunner.cs`, `BattleSceneBuilder.cs`; Test `ResultFlowTests.cs`

**Interfaces:**
- Consumes: `IRewardedAdService` (`AdMobRewardedAdService.Shared`, 에디터에선 `IsReady=false` 면 부활 버튼 숨김 - 원본 `rewardedAvailable()`), `Campaign.StarsFor(peakMobs)`.
- Produces: `ResultPanel.Show(ResultView v)`; `ResultView { Win, Title, Sub, Stars (int? ), Buttons[] {Label, Style, Action} }`.

- [ ] **Step 1: 실패하는 테스트**
  - `Defeat_OffersReviveOnceWhenAdReady`: 지면 `위기!` + `{tv} 부활하고 계속하기` / `포기하기`; 부활 후 다시 지면 부활 버튼 없음 (`Game.CanRevive`).
  - `StageClear_ShowsStars`: 1-1 클리어, PeakMobs 20 → 별 = `Campaign.StarsFor(20)`, 문구 `{0} · 최대 필드 몹 {1}마리`.
  - `StageFail_SubShowsWave`: `{reason} · Wave {w}/{waves}`.
  - `Endless_GameOverTitle`: 클리어 `50웨이브 클리어!`, 아니면 `게임 오버`.
- [ ] **Step 2: 실패 확인**
- [ ] **Step 3: 구현** (`main.ts onGameOver/finishSolo` 의 문구·버튼 순서). 버튼: 다시 하기(실패 시) / 다음 스테이지는 3단계 / 로비로 = 시작 화면으로. 보상 줄은 비워 둔다. `LaunchPanel`: `1-1` / `무한` 버튼 → `BattleLaunch.Current` 설정 후 `Restart()`.
- [ ] **Step 4: 통과 확인**
- [ ] **Step 5: 커밋** `Add result and revive flow and a temporary launch panel`

### Task 9: 끝까지 플레이 확인 + 성능 점검

**Files:** Create `View/DebugAutoPlay.cs` (에디터 전용 `#if UNITY_EDITOR`, `BotPlayer` 로 자동 조작 + 배속), Modify spec 진행표.

- [ ] **Step 1:** 플레이 모드에서 1-1 을 손으로 처음부터 클리어 (소환·합성·강화·구역 교환 사용), 결과 화면 별 확인.
- [ ] **Step 2:** 무한 모드를 `DebugAutoPlay`(BotPlayer skill 0.85, `Time.timeScale` 4)로 게임 오버 또는 50웨이브까지 → 결과 화면, 부활 흐름 1회.
- [ ] **Step 3:** Profiler(`manage_profiler`) 로 전투 중 GC Alloc / 프레임: 0B 근처 (TMP `SetText` 변경분 제외), 콘솔 에러·경고 0.
- [ ] **Step 4:** `dotnet test CoreTests/Tests`, Unity EditMode 전체 통과, `Sim fingerprint` 변화 없음 (Core 안 건드림 확인).
- [ ] **Step 5:** spec 진행표 2단계 완료 기록, 커밋 `Finish stage 2: battle screen playable for 1-1 and endless`.

---

## 범위 밖 (후속)
- 오디오 (`audio.ts` - Mixkit 클립·BGM 4트랙, 기존 `GameAudioManager` 정리) - 별도 작업.
- 1-1 튜토리얼·인게임 팁(`maybeTip`)·3배속 해금·보상·다음 스테이지·골드 부스터 - 3단계(메타).
- PvP HUD(상대 미니맵·몹 카드·카운트다운) - 4단계.
- 옛 `MainScene`/`GameScene`·매니저 삭제 - 새 씬 확인 후 사용자 확인 받고.

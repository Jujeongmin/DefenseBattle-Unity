# 내 PC에서 Claude Code + Unity MCP 세팅

Claude Code가 Unity 에디터를 직접 조작(오브젝트 생성·씬 편집·콘솔 에러 확인·플레이 모드 실행)할 수 있게 연결하는 방법.
클라우드 세션에서는 Unity를 설치할 수 없어서, 실제 이식 작업은 이 구성으로 PC에서 진행한다.

## 1. 준비물

| 항목 | 버전 / 비고 |
| --- | --- |
| Unity | 6000.4.0f1 (이 프로젝트 버전, Unity Hub로 설치) |
| Claude Code | 데스크톱 앱 또는 CLI (`npm install -g @anthropic-ai/claude-code`) |
| Python | 3.10 이상 |
| uv | Python 패키지 매니저. Windows PowerShell: `powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 \| iex"` · macOS: `curl -LsSf https://astral.sh/uv/install.sh \| sh` |
| Git | 두 저장소 클론용. Unity Package Manager 가 MCP 패키지를 git 으로 받으므로 PATH 에 있어야 한다 |

## 2. 저장소 두 개를 나란히 클론

이식 작업 중 Defense Battle 원본 코드를 계속 참고하므로 **같은 부모 폴더**에 둔다. 작업은 `main` 에서 한다.

```bash
mkdir JunJa && cd JunJa
git clone https://github.com/Jujeongmin/DefenseBattle-Unity.git
git clone https://github.com/Jujeongmin/DefenseBattle.git
```

`Assets/Down/` 서드파티 팩(README의 "클론 시 주의")은 Defense Battle 픽셀아트로 교체할 예정이라
새 앱 작업에는 필요 없다. 다만 교체가 끝나기 전까지 기존 씬을 열면 참조가 깨진 채로 보일 수 있다.

## 3. Unity 에서 프로젝트 열기

MCP for Unity 패키지(`com.coplaydev.unity-mcp` v9.7.3)는 `Packages/manifest.json` 에 이미 들어 있어서 따로 설치할 필요가 없다.

1. Unity Hub로 `DefenseBattle-Unity` 프로젝트를 연다. 처음 열 때 패키지를 받고 컴파일하느라 몇 분 걸린다.
2. **Window → MCP for Unity** 창이 생긴다. 처음엔 설치 마법사가 Python·uv 를 확인한다.
3. 창의 **Advanced Settings → Auto-Start on Editor Load** 를 켜 둔다. 그러면 에디터를 열 때마다
   MCP 서버(`http://127.0.0.1:8080`, 처음엔 `uvx` 가 `mcpforunityserver` 를 받는다)가 켜지고 에디터가 붙는다.
   켜지 않으면 창에서 **Start Server** 를 누른다.
4. 콘솔에 `MCP-FOR-UNITY: Session connected` 가 보이면 서버 연결 완료.

## 4. Claude Code 연결

**자동:** `Window → MCP for Unity` 창에서 **Configure All Detected Clients** 클릭 → Claude Code가 감지되면 자동 등록.

**수동 (자동이 안 될 때):** 프로젝트 폴더에서 터미널을 열고 실행한다 (HTTP 모드 기본값).

```bash
claude mcp add --scope local --transport http UnityMCP http://127.0.0.1:8080/mcp
claude mcp list        # UnityMCP 가 Connected 로 보이면 성공
```

등록은 그 PC·그 폴더에 한 번만 하면 된다. **이미 켜져 있던 Claude Code 세션에는 Unity 도구가 붙지 않으니** 새 세션을 연다.

## 5. 확인

1. Unity 에디터를 켜 둔 채로, `DefenseBattle-Unity` 폴더에서 Claude Code 새 세션을 연다.
2. `/mcp` 를 입력해 `UnityMCP` 가 연결돼 있는지 본다.
3. 시험 삼아 이렇게 말해 본다: *"빈 씬에 큐브를 하나 만들고, 확인했으면 지워 줘."*
   에디터에 큐브가 생겼다 사라지면 끝.

## 6. 이식 작업 시작

Claude Code에 이렇게 말하면 된다.

> `docs/superpowers/specs/2026-10-19-defense-battle-port-design.md` 를 읽고, README 진행 상황에서 다음 단계부터 진행해 줘.

저장소 루트의 `CLAUDE.md` 에 작업 규칙이 있어서 Claude Code가 자동으로 읽는다.

## 문제 해결

- **연결이 안 됨:** Unity 에디터가 켜져 있는지, MCP for Unity 창에서 서버가 실행 중인지 확인 → Claude Code 재시작.
- **uv 를 못 찾음:** 터미널을 새로 열거나 PC 재부팅 후 `uv --version` 확인. MCP for Unity 창의 Advanced Settings에서 uvx 경로 지정 가능.
- **WSL에서 Claude Code를 쓰는 경우:** `127.0.0.1` 대신 Windows 호스트 IP로 등록해야 한다 (MCP for Unity 문서의 troubleshooting 참고).
- 더 자세한 내용: <https://github.com/CoplayDev/unity-mcp>

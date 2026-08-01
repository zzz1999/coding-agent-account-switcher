# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Codex, Claude Code 및 OpenCode를 위한 비공식 로컬 우선 Windows 계정/API 사이트 전환 도구입니다.

Coding Agent Account Switcher는 로컬 자격 증명과 필요한 API 공급자 필드만 암호화 스냅샷으로 저장합니다. 다른 설정, MCP, 스킬, 플러그인 및 프로젝트 기록은 원래 위치에 그대로 둡니다.

> [!IMPORTANT]
> 이 프로젝트는 OpenAI 또는 Anthropic과 제휴하거나 이들로부터 승인 또는 후원을 받지 않았습니다. 구독을 이전하거나, 로그인 요구 사항을 우회하거나, 계정을 공유하거나, 공급자 또는 조직 정책을 회피하지 않습니다.

“iOS 18에서 영감을 받음”은 일반적인 시각적 방향만을 의미합니다. Apple은 이 프로젝트와 관련이 없으며 Apple 글꼴, 심볼, 아트워크 또는 상표를 포함하지 않습니다.

## 다운로드

현재 빌드는 [latest 릴리스](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest)에서 다운로드하십시오.

- **권장 설치 프로그램:** `coding-agent-account-switcher-setup-win-x64.exe`는 관리자 권한 없이 현재 Windows 사용자용으로 설치하며 시작 메뉴 및 제거 항목을 만듭니다.
- **포터블 실행 파일:** `coding-agent-account-switcher-portable-win-x64.exe`을 직접 다운로드해 실행할 수 있으며 설치가 필요하지 않습니다.
- 릴리스에는 각 실행 파일에 해당하는 SHA-256 체크섬 파일이 포함됩니다.

설치 프로그램은 **Windows 시작 시 실행**을 자동으로 활성화하지 않으며 Codex, Claude Code 또는 OpenCode 인증이나 구성 파일을 건드리지 않습니다. 앱을 제거해도 암호화된 계정 스냅샷과 앱 설정은 보존되어 다시 설치한 뒤에도 사용할 수 있습니다. 설치 프로그램과 포터블 실행 파일은 현재 서명되지 않았으므로 Windows SmartScreen에서 평판 경고를 표시할 수 있습니다.

## 기능

- iOS 18에서 영감을 받은 글래스 카드 디자인의 Windows 네이티브 WPF 인터페이스.
- 영어, 중국어 간체, 중국어 번체, 스페인어, 프랑스어, 독일어, 일본어, 한국어, 브라질 포르투갈어, 러시아어, 아랍어 및 힌디어 인터페이스 내장.
- 표시 언어와 현재 사용자의 Windows 시작 시 실행 여부를 선택할 수 있는 앱 설정.
- Codex, Claude Code 및 OpenCode용 개인, 업무 및 API 사이트 프로필에 이름을 지정하여 저장.
- 관련 응용 프로그램이 닫힐 때까지 전환을 차단하는 프로세스 보호.
- 인증 파일은 불투명한 바이트로 유지합니다. 아래 관리 대상 필드만 분석하고 병합하며 비밀은 표시하거나 기록하지 않습니다.
- 현재 Windows 사용자의 Windows DPAPI로 프로필 스냅샷 암호화.
- 동일 디렉터리에서 관리 파일을 원자적으로 교체하며 롤백 지원.
- 변경된 라이브 로그인이 마지막 선택 프로필 스냅샷을 덮어쓰기 전에 명시적 확인 요구.
- 마지막 선택 프로필을 위한 안전한 **스냅샷 복원** 작업. 확인은 교체될 라이브 인증의 정확한 바이트에 연결됩니다.
- 분석 또는 원격 측정 없는 로컬 전용 동작.
- `main`에서 자동으로 생성되는 Windows용 롤링 `latest` 빌드.

## 지원되는 계정 및 API 구성

| 공급자 | 관리 파일 | 선택적으로 관리되는 구성 |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` 및 `config.toml` | `model_provider`, `openai_base_url`, `model`, `review_model`, `model_reasoning_effort`, `disable_response_storage`, 선택된 활성 `model_providers` 테이블, `features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` 및 `settings.json` | `env.ANTHROPIC_BASE_URL`, `env.ANTHROPIC_API_KEY`, `env.ANTHROPIC_AUTH_TOKEN`, `env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`, 호환 필드 `env.CLAUDE_CODE_ATTRIBUTION_HEADER`만 |
| OpenCode | 선택적 `%USERPROFILE%\.local\share\opencode\auth.json`, 전역 `config.json`, `opencode.json`, `opencode.jsonc`, 설정 시 마지막에 `OPENCODE_CONFIG` | 불투명한 `/connect` 자격 증명과 유효 `provider`, `model`, `small_model` API 프로필 |

앱은 전체 파일을 교체하지 않고 이 필드만 현재 구성에 병합합니다. Codex의 `network_access`, `windows_wsl_setup_acknowledged`, `features.goals`, `cli_auth_credentials_store`, MCP, 스킬, 세션 및 다른 모든 값은 보존됩니다. Claude Code의 다른 `env`, `.claude.json`, 플러그인, MCP, 프로젝트 설정 및 기록도 보존됩니다. OpenCode에서는 참여하는 각 전역/사용자 지정 파일의 관련 없는 의미 값을 보존합니다.

OpenCode의 기본 구성 루트는 `%USERPROFILE%\.config`이고 기본 데이터 루트는 `%USERPROFILE%\.local\share`입니다. `XDG_CONFIG_HOME` 또는 `XDG_DATA_HOME`이 절대 경로일 때만 해당 기본 루트를 대체하며, 빈 값은 대체하지 않습니다. 비어 있지 않은 모든 OpenCode 경로 재정의(`XDG_CONFIG_HOME`, `XDG_DATA_HOME`, `OPENCODE_CONFIG`, `OPENCODE_CONFIG_DIR`)는 절대 경로여야 하며, 상대 경로 값이 있으면 파일을 쓰기 전에 캡처와 전환을 차단합니다. 전역 구성은 구성 루트 아래의 `opencode\config.json`, `opencode.json`, `opencode.jsonc` 순서로 읽으며 뒤의 계층이 우선합니다. `OPENCODE_CONFIG`는 마지막에 읽습니다. 앱은 유효한 `provider`, `model`, `small_model`을 하나의 API 프로필로 캡처합니다. `/connect` 인증은 데이터 루트 아래의 `opencode\auth.json`에서 읽습니다.

OpenCode는 `auth.json`의 `/connect` 자격 증명과 `provider` 개체에 포함된 API 키를 모두 지원하며 스냅샷은 존재하는 방식 또는 둘 다를 포함합니다.

적용할 때 오래된 하위 엔드포인트가 우선하지 않도록 다른 전역 계층에서 `provider`, `model`, `small_model`을 제거합니다. `OPENCODE_CONFIG`가 있으면 대상 값을 거기에 쓰고 세 전역 계층을 정리합니다. 없으면 비어 있지 않은 대상 값을 전역 `opencode.jsonc`로 정규화하고, 이전에 JSON/레거시 파일만 있었어도 `config.json`과 `opencode.json`에서 제거합니다. 인증만 있거나 관리 대상이 빈 스냅샷도 유효하며 기존 값은 지우지만 빈 `opencode.jsonc`를 새로 만들지는 않습니다.

선택적 병합은 관련 없는 의미 값을 보존하지만 JSON 또는 TOML을 다시 직렬화할 때 형식이나 주석의 바이트 단위 보존을 보장하지 않습니다.

OpenCode를 캡처하거나 전환하기 전에 앱은 알려진 고우선순위 환경 재정의를 읽기 전용으로 검사합니다. 공백이 아닌 `OPENCODE_AUTH_CONTENT` 값이 있으면 작업이 차단됩니다. `OPENCODE_CONFIG_CONTENT`는 유효한 JSON/JSONC이고 최상위 관리 키 `provider`, `model`, `small_model`을 포함하지 않을 때만 허용됩니다. 잘못된 내용이나 관리 키가 있으면 작업이 차단됩니다. `OPENCODE_CONFIG_DIR`이 설정된 경우 그 안의 `opencode.json`과 `opencode.jsonc`를 검사합니다. 읽을 수 없거나 잘못된 파일 또는 관리 키를 포함한 파일이 있으면 작업이 차단됩니다. 관련 없는 키만 포함한 인라인 또는 디렉터리 구성은 허용됩니다. 앱은 이러한 환경 제공 소스를 수정하지 않습니다.

`CODEX_HOME`과 `CLAUDE_CONFIG_DIR`은 각 루트를 변경합니다. OpenCode 프로젝트 구성, 중앙 관리 소스, 공급자별 환경 변수는 계속 관리 대상이 아니며 전환 후 선택한 전역 프로필을 재정의할 수 있습니다. 앱은 이를 검색하거나 수정하지 않습니다. 프로세스 보호는 `opencode`와 `opencode-cli`를 확인합니다. 호환 필드 표기는 모든 최신 버전에 문서화되어 있다는 의미가 아닙니다.

Codex는 파일 기반 자격 증명 저장소를 사용해야 합니다. 설치 환경에서 운영 체제 자격 증명 저장소를 사용하는 경우 활성 Codex 루트(기본값 `%USERPROFILE%\.codex`, 설정된 경우 `CODEX_HOME`)의 `config.toml`에 다음 설정을 추가하십시오.

```toml
cli_auth_credentials_store = "file"
```

현재 저장 계약은 공식 [Codex 인증 문서](https://developers.openai.com/codex/auth)와 [Claude Code 인증 문서](https://code.claude.com/docs/en/authentication)를 참조하십시오.

## 작동 방식

1. 공식 절차로 로그인하거나 일반 공급자 파일에 지원되는 API 사이트를 구성합니다.
2. Codex, Claude Code, OpenCode 및 관련 클라이언트나 확장 기능을 완전히 닫습니다.
3. 계정과 관리 대상 API 설정을 `Personal` 같은 이름으로 저장합니다.
4. 다른 계정 또는 API 사이트를 구성하고 `Work` 같은 이름으로 저장합니다.
5. 프로필을 선택합니다. 관련 프로세스가 실행 중이면 전환을 차단하고 관리 파일을 수정하지 않습니다.
6. 콘텐츠에 연결된 확인 후 갱신 토큰과 의도적인 API 변경을 보존하도록 현재 관리 스냅샷을 마지막 선택 암호화 프로필에 저장합니다. 전환 전의 정확한 스냅샷도 트랜잭션 전용 DPAPI 복구 블롭에 보존합니다.

앱은 이 프로필을 “검증된 현재 상태”가 아니라 **마지막 선택**으로 표시합니다. 라이브 파일이 저장된 스냅샷과 더 이상 일치하지 않으면 쓰기 전에 전환을 중지합니다. 변경이 같은 계정의 새로 고침인 경우에만 확인하십시오. 앱 외부에서 다른 계정에 로그인했다면 먼저 **새로 저장**을 선택하거나, 올바른 이름의 기존 프로필을 명시적으로 교체하십시오.

마지막 선택 카드의 **스냅샷 복원** 버튼은 라이브 파일이 저장된 스냅샷과 여전히 일치하는지 확인합니다. 다르면 저장되지 않은 현재 로그인이 교체된다는 경고를 표시하고 승인을 해당 바이트에 정확히 연결합니다. 다른 라이브 로그인은 이전 확인을 재사용할 수 없습니다. 복원 중에는 트랜잭션 전용 DPAPI 암호화 복구 자격 증명이 작업이 커밋되거나 롤백될 때까지 복원 전 바이트를 보존합니다.

각 복합 또는 다중 파일 트랜잭션은 현재 관리 스냅샷이 저장된 원본과 일치하더라도 저널보다 먼저 그 정확한 스냅샷을 암호화 복구 블롭에 저장합니다. 따라서 일부 파일만 쓰인 상태를 정확히 롤백하고 자격 증명만 있던 이전 원본 프로필을 안전하게 업그레이드할 수 있습니다. 중단 후 복구는 이 전환 전 스냅샷을 우선하며 복원 시 원본 프로필도 동기화합니다. 블롭이 없는 이전 저널은 저장된 원본으로 대체합니다. 라이브 관리 상태가 원본과 대상 어느 쪽에도 일치하지 않으면 저널과 블롭을 수동 복구용으로 유지합니다.

다중 파일 커밋은 실패 시 닫힘 방식입니다. 별도 인증을 먼저 제거하고 구성을 원자적으로 병합한 다음 대상 인증을 마지막에 설치합니다. 중단 시 인증이 잠시 없을 수는 있지만 자격 증명이 반대 프로필의 API 엔드포인트와 결합되지는 않으며, 암호화된 전환 전 스냅샷으로 안전하게 완료하거나 롤백합니다.

이전의 원시 Codex 및 Claude Code 프로필은 자격 증명과 빈 관리 대상 API 구성으로 해석됩니다. 이를 활성화하면 이전 프로필의 타사 엔드포인트를 재사용하지 않도록 관리 대상 API 경로와 모델 필드를 지웁니다. 이후 원하는 모델/API를 설정하고 프로필을 다시 캡처하십시오. 이전 형식의 활성 원본은 다른 프로필로 전환할 때 결합 형식으로 업그레이드됩니다.

앱은 영구 로그인을 보장하지 않습니다. 공급자 측 취소, 조직 정책, SSO, MFA 또는 토큰 만료로 인해 공식 클라이언트에서 정상적으로 다시 로그인해야 할 수 있습니다.

프로필 이름 변경과 삭제는 로컬 암호화 스냅샷 보관소에만 적용됩니다. 이름 변경은 저장된 레이블과 메타데이터만 바꾸며 스냅샷 내용은 바꾸지 않습니다. 삭제는 선택한 로컬 암호화 스냅샷만 제거합니다. 마지막으로 선택한 프로필을 삭제하면 앱의 활성 프로필 연결도 지워지지만, 로그아웃하거나 공급자의 실제 인증 또는 구성 파일을 변경하지 않습니다. 다시 전환하기 전에 현재 계정을 저장하십시오. 중단된 전환 트랜잭션이 복구 대기 중이면 두 작업 모두 거부됩니다.

## 설정

앱 창에서 **설정**을 열어 표시 언어를 선택하거나 앱이 Windows와 함께 시작할지 제어할 수 있습니다. 선택한 언어는 현재 Windows 사용자에 대해 로컬로 저장되며 언제든 변경할 수 있습니다.

**Windows 시작 시 실행**은 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 아래에 이 앱의 항목을 추가합니다. 현재 Windows 사용자에게만 적용되며 관리자 권한이 필요하지 않습니다. 옵션을 끄면 Coding Agent Account Switcher가 소유한 시작 항목만 제거하며 다른 시작 앱은 변경하지 않습니다.

**업데이트 확인**은 사용자가 명시적으로 시작하는 작업입니다. 클릭한 경우에만 이 저장소의 공식 GitHub API로 익명의 HTTPS `GET` 요청을 한 번 보냅니다. 시작 시, 백그라운드 또는 주기적인 확인은 없으며 자격 증명, 설정, 프로필 이름, 장치 식별자 또는 텔레메트리를 업로드하지 않습니다. 앱은 릴리스 메타데이터만 비교하며 설치 프로그램이나 포터블 빌드를 자동으로 다운로드하거나 실행하지 않습니다.

## 보안 모델

- 암호화된 프로필 데이터는 `%LOCALAPPDATA%\CodingAgentAccountSwitcher` 아래에 저장됩니다.
- 정규화된 관리 파일 집합마다 독립 보관소, 상태, 복구 저널 및 뮤텍스가 있습니다. `CODEX_HOME`, `CLAUDE_CONFIG_DIR` 또는 `OPENCODE_CONFIG`를 변경하면 다른 프로필 집합이 시작됩니다.
- DPAPI `CurrentUser`는 다른 Windows 계정이 프로필을 직접 복호화하는 것을 막지만, 이미 같은 Windows 사용자로 실행 중인 악성 소프트웨어로부터 보호할 수는 없습니다.
- 공급자의 일반 라이브 인증 파일을 제외하면, 복호화된 스냅샷 바이트는 캡처 또는 전환 중 메모리와 동일 디렉터리 원자적 교체 과정에 잠깐만 존재합니다.
- 인증 교체 임시 파일과 백업 파일은 복구 트랜잭션 ID를 사용합니다. 정상 완료 시 두 개의 정확한 파일을 모두 삭제합니다. 중단 후에는 암호화된 원본 스냅샷 또는 트랜잭션 전용 복구 자격 증명에서 누락된 라이브 파일을 복원한 다음, 저널을 삭제하기 전에 해당 트랜잭션이 소유한 모든 정확한 스테이징 파일을 제거합니다. 암호화 복구 블롭은 저널을 삭제한 뒤에만 최선의 방식으로 삭제를 시도합니다.
- 앱은 자격 증명을 업로드하지 않으며 로그, 이슈, 충돌 보고서, 테스트 픽스처 또는 저장소 커밋에 자격 증명을 절대 포함해서는 안 됩니다.
- 프로세스 감지는 방어적인 최선의 노력입니다. 작업이 끝날 때까지 Codex, Claude Code 또는 OpenCode를 실행하지 마십시오.
- 업무 계정이 조직에서 관리되는 경우 추가 암호화 로컬 인증 스냅샷을 보관하기 전에 승인을 받으십시오.

자격 증명 처리 코드를 변경하기 전에 [SECURITY.md](SECURITY.md)와 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)를 읽으십시오.

## 소스에서 빌드

요구 사항:

- Windows 10 또는 Windows 11
- .NET SDK 8.0.400 또는 더 최신 .NET 8 feature band

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

자체 포함 Windows x64 빌드 만들기:

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## 롤링 latest 릴리스

`main`에 푸시할 때마다 `.github/workflows/latest-release.yml`이 실행됩니다.

1. 솔루션을 복원하고 테스트합니다.
2. 자체 포함 포터블 Windows x64 빌드를 게시합니다.
3. 사용자별 Windows x64 설치 프로그램을 빌드합니다.
4. 포터블 실행 파일과 두 실행 파일의 SHA-256 체크섬을 준비합니다.
5. 이전의 `latest`라는 릴리스와 태그만 삭제합니다.
6. 현재 커밋용 새 `latest` 릴리스에 설치 프로그램, 포터블 실행 파일 및 체크섬을 게시합니다. 워크플로의 단일 `APP_VERSION` 값으로 실행 파일 버전을 지정하고 릴리스 노트에 다음의 정확한 기계 판독 가능 마커를 기록합니다: `<!-- coding-agent-account-switcher-version: 0.1.N -->`.

`latest` 태그는 롤링 방식이므로 사용자가 명시적으로 업데이트를 확인할 때만 앱이 공식 GitHub Releases API 응답에서 이 마커를 읽습니다. 백그라운드에서 확인하거나 릴리스 자산을 자동으로 다운로드 또는 실행하지 않습니다.

이 워크플로는 버전이 지정된 릴리스를 삭제하지 않습니다. 롤링 `latest` 태그에서는 GitHub의 **immutable releases** 옵션을 비활성화해야 하며 브랜치 또는 태그 규칙에서 워크플로가 `latest`를 삭제할 수 있도록 허용해야 합니다. 변경 불가능한 릴리스가 필요한 저장소는 워크플로를 고유한 빌드 태그로 변경해야 합니다.

롤링 설치 프로그램과 포터블 실행 파일은 현재 서명되지 않았으므로 Windows SmartScreen에서 평판 경고를 표시할 수 있습니다. 실행하기 전에 소스를 검토하고 해당 게시된 SHA-256 체크섬을 확인하십시오.

## 기여

기여를 환영합니다. [CONTRIBUTING.md](CONTRIBUTING.md)를 읽으십시오. 테스트는 임시 합성 자격 증명과 구성을 사용하고 실제 Codex, Claude Code 또는 OpenCode 파일에 접근해서는 안 됩니다.

## 라이선스

[MIT](LICENSE)

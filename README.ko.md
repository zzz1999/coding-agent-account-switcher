# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Codex와 Claude Code를 위한 비공식 로컬 우선 Windows 계정 전환 도구입니다.

Coding Agent Account Switcher는 Codex와 Claude Code가 사용하는 로컬 인증 파일의 이름 있는 암호화 스냅샷을 저장합니다. 인증 스냅샷만 전환하며, 일반 설정, MCP 구성, 스킬, 플러그인 및 프로젝트 기록은 원래 위치에 그대로 둡니다.

> [!IMPORTANT]
> 이 프로젝트는 OpenAI 또는 Anthropic과 제휴하거나 이들로부터 승인 또는 후원을 받지 않았습니다. 구독을 이전하거나, 로그인 요구 사항을 우회하거나, 계정을 공유하거나, 공급자 또는 조직 정책을 회피하지 않습니다.

“iOS 18에서 영감을 받음”은 일반적인 시각적 방향만을 의미합니다. Apple은 이 프로젝트와 관련이 없으며 Apple 글꼴, 심볼, 아트워크 또는 상표를 포함하지 않습니다.

## 다운로드

현재 빌드는 [latest 릴리스](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest)에서 다운로드하십시오.

- **권장 설치 프로그램:** `coding-agent-account-switcher-setup-win-x64.exe`는 관리자 권한 없이 현재 Windows 사용자용으로 설치하며 시작 메뉴 및 제거 항목을 만듭니다.
- **포터블 패키지:** `coding-agent-account-switcher-win-x64.zip`은 설치하지 않고 압축을 풀어 실행할 수 있습니다.
- 릴리스에는 각 패키지에 해당하는 SHA-256 체크섬 파일이 포함됩니다.

설치 프로그램은 **Windows 시작 시 실행**을 자동으로 활성화하지 않으며 Codex 또는 Claude Code 인증이나 구성 파일을 건드리지 않습니다. 앱을 제거해도 암호화된 계정 스냅샷과 앱 설정은 보존되어 다시 설치한 뒤에도 사용할 수 있습니다. 설치 프로그램과 포터블 실행 파일은 현재 서명되지 않았으므로 Windows SmartScreen에서 평판 경고를 표시할 수 있습니다.

## 기능

- iOS 18에서 영감을 받은 글래스 카드 디자인의 Windows 네이티브 WPF 인터페이스.
- 영어, 중국어 간체, 중국어 번체, 스페인어, 프랑스어, 독일어, 일본어, 한국어, 브라질 포르투갈어, 러시아어, 아랍어 및 힌디어 인터페이스 내장.
- 표시 언어와 현재 사용자의 Windows 시작 시 실행 여부를 선택할 수 있는 앱 설정.
- Codex와 Claude Code용 개인 및 업무 프로필에 이름을 지정하여 저장.
- 관련 응용 프로그램이 닫힐 때까지 전환을 차단하는 프로세스 보호.
- 자격 증명을 불투명한 바이트로 처리: 토큰 분석, 이메일 추출 또는 자격 증명 로깅 없음.
- 현재 Windows 사용자의 Windows DPAPI로 프로필 스냅샷 암호화.
- 동일 디렉터리에서 자격 증명을 원자적으로 교체하며 롤백 지원.
- 변경된 라이브 로그인이 마지막 선택 프로필 스냅샷을 덮어쓰기 전에 명시적 확인 요구.
- 마지막 선택 프로필을 위한 안전한 **스냅샷 복원** 작업. 확인은 교체될 라이브 인증의 정확한 바이트에 연결됩니다.
- 분석 또는 원격 측정 없는 로컬 전용 동작.
- `main`에서 자동으로 생성되는 Windows용 롤링 `latest` 빌드.

## 지원되는 인증 파일

| 공급자 | 기본적으로 전환되는 인증 파일 | 변경하지 않는 구성 |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`, 스킬, MCP, 세션 및 기타 상태 |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`, `.claude.json`, 플러그인, MCP, 프로젝트 설정 및 세션 기록 |

`CODEX_HOME` 또는 `CLAUDE_CONFIG_DIR`이 설정된 경우 앱은 공급자별 인증 루트를 따릅니다. 이 경우에도 `auth.json` 또는 `.credentials.json`만 전환하며 인접한 구성 파일은 변경하지 않습니다.

Codex는 파일 기반 자격 증명 저장소를 사용해야 합니다. 설치 환경에서 운영 체제 자격 증명 저장소를 사용하는 경우 활성 Codex 루트(기본값 `%USERPROFILE%\.codex`, 설정된 경우 `CODEX_HOME`)의 `config.toml`에 다음 설정을 추가하십시오.

```toml
cli_auth_credentials_store = "file"
```

현재 저장 계약은 공식 [Codex 인증 문서](https://developers.openai.com/codex/auth)와 [Claude Code 인증 문서](https://code.claude.com/docs/en/authentication)를 참조하십시오.

## 작동 방식

1. 공급자의 공식 로그인 절차를 통해 첫 번째 계정에 로그인합니다.
2. Codex/Claude Code와 관련된 모든 로컬 클라이언트 또는 확장 기능을 완전히 닫습니다.
3. 현재 로그인을 `Personal`과 같이 사용자가 선택한 이름으로 저장합니다.
4. 두 번째 계정에 로그인하고 `Work`와 같은 다른 이름으로 저장합니다.
5. 저장된 프로필을 선택합니다. 앱은 변경 전에 관련 프로세스를 확인합니다. 하나라도 실행 중이면 전환을 차단하며 자격 증명 파일을 수정하지 않습니다.
6. 다른 프로필로 전환할 때 바이트에 연결된 확인 후, 갱신된 토큰을 유지하도록 현재 인증 파일을 마지막 선택 암호화 프로필에 다시 저장합니다.

앱은 이 프로필을 “검증된 현재 상태”가 아니라 **마지막 선택**으로 표시합니다. 라이브 파일이 저장된 스냅샷과 더 이상 일치하지 않으면 쓰기 전에 전환을 중지합니다. 변경이 같은 계정의 새로 고침인 경우에만 확인하십시오. 앱 외부에서 다른 계정에 로그인했다면 먼저 **새로 저장**을 선택하거나, 올바른 이름의 기존 프로필을 명시적으로 교체하십시오.

마지막 선택 카드의 **스냅샷 복원** 버튼은 라이브 파일이 저장된 스냅샷과 여전히 일치하는지 확인합니다. 다르면 저장되지 않은 현재 로그인이 교체된다는 경고를 표시하고 승인을 해당 바이트에 정확히 연결합니다. 다른 라이브 로그인은 이전 확인을 재사용할 수 없습니다. 복원 중에는 트랜잭션 전용 DPAPI 암호화 복구 자격 증명이 작업이 커밋되거나 롤백될 때까지 복원 전 바이트를 보존합니다.

앱은 영구 로그인을 보장하지 않습니다. 공급자 측 취소, 조직 정책, SSO, MFA 또는 토큰 만료로 인해 공식 클라이언트에서 정상적으로 다시 로그인해야 할 수 있습니다.

## 설정

앱 창에서 **설정**을 열어 표시 언어를 선택하거나 앱이 Windows와 함께 시작할지 제어할 수 있습니다. 선택한 언어는 현재 Windows 사용자에 대해 로컬로 저장되며 언제든 변경할 수 있습니다.

**Windows 시작 시 실행**은 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 아래에 이 앱의 항목을 추가합니다. 현재 Windows 사용자에게만 적용되며 관리자 권한이 필요하지 않습니다. 옵션을 끄면 Coding Agent Account Switcher가 소유한 시작 항목만 제거하며 다른 시작 앱은 변경하지 않습니다.

## 보안 모델

- 암호화된 프로필 데이터는 `%LOCALAPPDATA%\CodingAgentAccountSwitcher` 아래에 저장됩니다.
- 정규화된 인증 파일 위치마다 해시 범위로 분리된 보관소, 활성 상태, 복구 저널 및 작업 뮤텍스가 있습니다. 따라서 `CODEX_HOME` 또는 `CLAUDE_CONFIG_DIR`을 변경하면 다른 위치의 활성 계정을 재사용하지 않고 독립 프로필 세트가 시작됩니다.
- DPAPI `CurrentUser`는 다른 Windows 계정이 프로필을 직접 복호화하는 것을 막지만, 이미 같은 Windows 사용자로 실행 중인 악성 소프트웨어로부터 보호할 수는 없습니다.
- 공급자의 일반 라이브 인증 파일을 제외하면, 복호화된 스냅샷 바이트는 캡처 또는 전환 중 메모리와 동일 디렉터리 원자적 교체 과정에 잠깐만 존재합니다.
- 인증 교체 임시 파일과 백업 파일은 복구 트랜잭션 ID를 사용합니다. 정상 완료 시 두 개의 정확한 파일을 모두 삭제합니다. 중단 후에는 암호화된 원본 스냅샷 또는 트랜잭션 전용 복구 자격 증명에서 누락된 라이브 파일을 복원한 다음, 저널을 삭제하기 전에 해당 트랜잭션이 소유한 모든 정확한 스테이징 파일을 제거합니다.
- 앱은 자격 증명을 업로드하지 않으며 로그, 이슈, 충돌 보고서, 테스트 픽스처 또는 저장소 커밋에 자격 증명을 절대 포함해서는 안 됩니다.
- 프로세스 감지는 방어적인 최선의 노력입니다. 새로 시작된 프로세스가 전환과 경합할 수 있으므로 작업이 끝날 때까지 Codex 또는 Claude Code를 실행하지 마십시오.
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
4. 포터블 ZIP과 두 패키지의 SHA-256 체크섬을 생성합니다.
5. 이전의 `latest`라는 릴리스와 태그만 삭제합니다.
6. 현재 커밋용 새 `latest` 릴리스에 설치 프로그램, 포터블 패키지 및 체크섬을 게시합니다.

이 워크플로는 버전이 지정된 릴리스를 삭제하지 않습니다. 롤링 `latest` 태그에서는 GitHub의 **immutable releases** 옵션을 비활성화해야 하며 브랜치 또는 태그 규칙에서 워크플로가 `latest`를 삭제할 수 있도록 허용해야 합니다. 변경 불가능한 릴리스가 필요한 저장소는 워크플로를 고유한 빌드 태그로 변경해야 합니다.

롤링 설치 프로그램과 포터블 실행 파일은 현재 서명되지 않았으므로 Windows SmartScreen에서 평판 경고를 표시할 수 있습니다. 실행하기 전에 소스를 검토하고 해당 게시된 SHA-256 체크섬을 확인하십시오.

## 기여

기여를 환영합니다. [CONTRIBUTING.md](CONTRIBUTING.md)를 읽으십시오. 테스트는 임시 가짜 자격 증명 파일을 사용해야 하며 개발자의 실제 Codex 또는 Claude Code 인증 파일에 절대 접근해서는 안 됩니다.

## 라이선스

[MIT](LICENSE)

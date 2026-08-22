# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Coding Agent Account Switcher 아이콘">
</p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

저장된 Codex, Claude Code, OpenCode 계정 또는 API 사이트 설정을 간편하게
전환하는 Windows 앱입니다.

프로필에 저장된 계정 정보와 지원되는 API 설정만 전환합니다. MCP 서버, 스킬,
플러그인, 프로젝트 설정 및 기록은 원래 위치에 그대로 남습니다.

> [!IMPORTANT]
> 이 앱은 비공식 커뮤니티 프로젝트이며 OpenAI, Anthropic, Apple 또는 OpenCode
> 프로젝트와 제휴하지 않습니다. 구독을 이전하거나 로그인 요구 사항을 우회하거나
> 조직 정책을 무시할 수 없습니다.

## 다운로드

현재 버전은
[최신 릴리스](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest)
에서 받을 수 있습니다.

- **설치 프로그램:** <code>CAAS-vX.Y.Z-Setup-x64.exe</code>
- **포터블 앱:** <code>CAAS-vX.Y.Z-Portable-x64.exe</code>
- **체크섬:** 각 실행 파일에 맞는 SHA-256 파일이 제공됩니다

설치는 현재 사용자에게만 적용되며 관리자 권한이 필요하지 않습니다. 사용자가
선택하지 않으면 **Windows 시작 시 실행**도 켜지지 않습니다. 현재 빌드는 서명되지
않았으므로 Windows SmartScreen이 경고를 표시할 수 있습니다.

## 주요 기능

- 개인, 업무 및 API 사이트 프로필을 알기 쉬운 이름으로 저장.
- 몇 번의 클릭으로 Codex, Claude Code 및 OpenCode 계정 전환.
- 지원되는 API 주소, 키 및 공급자 라우팅을 올바른 프로필에 저장.
- 프로필 이름을 두 번 클릭해 변경하거나 로컬 스냅샷 삭제.
- 전환 후 현재 프로필 이름을 명확하게 확인.
- 관련 앱이 열려 있는 동안 전환 차단.
- 12가지 내장 언어 사용.
- 라이트/다크 테마와 언어를 다음 실행에도 유지.
- 같은 Windows 로그인 세션에서 앱을 다시 실행하면 새 창을 만들지 않고 기존 창을
  복원해 앞으로 가져옵니다.
- Windows 시작 실행을 선택하고 업데이트를 수동 확인.

모든 데이터는 PC에 남습니다. 분석 기능이나 텔레메트리가 없습니다.

## 지원 앱

| 앱 | 전환되는 내용 | 변경되지 않는 내용 |
| --- | --- | --- |
| Codex | 로그인 및 선택한 API 공급자 연결 설정 | 모델, 검토/추론 옵션, 기능, MCP, 스킬, 세션, 기록 및 기타 설정 |
| Claude Code | 로그인 및 지원되는 API 주소/키 설정 | 플러그인, MCP, 프로젝트, 기록 및 기타 설정 |
| OpenCode | 저장된 로그인 및 지원되는 공급자/모델 설정 | 프로젝트 구성 및 기타 설정 |

이 프로젝트가 지원하는 계정 관련 필드만 변경합니다. 정확한 목록은
[아키텍처](docs/ARCHITECTURE.md)를 참고하십시오.

## 빠른 시작

1. 평소와 같이 로그인하거나 사용할 API 사이트를 설정합니다.
2. 앱을 열고 해당 공급자를 선택합니다.
3. **현재 계정 저장**을 선택하고 <code>Personal</code> 같은 이름을 지정합니다.
4. 두 번째 계정으로 로그인하거나 다른 API 사이트를 설정합니다.
5. <code>Work</code> 같은 이름으로 저장합니다.
6. 전환할 때는 관련 앱을 완전히 닫은 다음 저장된 프로필을 선택합니다.

계정을 저장할 때는 관련 앱을 닫지 않아도 됩니다. 전환 전에는 실행 중인 프로세스를
확인합니다. 무언가 열려 있으면 먼저 닫도록 안내하며 파일을 변경하지 않습니다.

저장한 뒤 현재 계정이 바뀌었다면 확인을 요청합니다. 실제로 다른 계정이라면 먼저
새 프로필로 저장하십시오.

## 프로필 및 설정

- **이름 변경:** 프로필 이름을 두 번 클릭합니다.
- **삭제:** 휴지통 버튼을 사용합니다. 로컬 암호화 스냅샷만 삭제하며 계정을
  삭제하거나 로그아웃하지 않습니다.
- **마지막 선택:** 이 앱이 가장 최근에 활성화한 프로필을 나타냅니다.
- **손상된 스냅샷:** 누락되거나 읽을 수 없는 항목은 숨기고 정상 프로필은 계속 사용할 수 있습니다.
- **테마 및 언어:** 현재 Windows 사용자용으로 기억합니다.
- **Windows 시작 시 실행:** 선택 사항이며 현재 사용자에게만 적용됩니다.
- **업데이트 확인:** 클릭할 때만 실행하며 자동 다운로드나 설치를 하지 않습니다.

## 개인정보 보호 및 보안

- 프로필은 현재 Windows 사용자의 DPAPI로 암호화됩니다.
- 자격 증명과 API 키는 표시하거나 앱 로그에 기록하지 않습니다.
- 프로필은 <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code>에 저장됩니다.
- 보호된 파일 교체와 복구로 불완전한 변경 위험을 줄입니다.
- 자격 증명, 프로필 이름, 장치 식별자 또는 텔레메트리를 전송하지 않습니다.
- 업무 계정에는 조직 정책이 적용될 수 있습니다. 추가 로컬 로그인 복사본을
  저장하기 전에 허가를 받으십시오.

보안 문제를 신고하기 전에 [SECURITY.md](SECURITY.md)를 읽어 주십시오.

## 제한 사항

- 공급자 측 로그아웃, 토큰 만료, SSO, MFA 또는 조직 정책으로 인해 정상 로그인이
  다시 필요할 수 있습니다.
- 기존 Codex 대화는 생성할 때 사용한 공급자에 연결됩니다. 공급자를 전환한 후에는
  새 대화를 시작하거나, 기존 대화를 계속하려면 원래 공급자로 다시 전환하세요.
- 구독을 이전하거나 영구 로그인을 보장할 수 없습니다.
- 현재 Windows 10 및 Windows 11 x64만 지원합니다.

## 소스에서 빌드

Windows와 .NET SDK 8.0.400 이상의 .NET 8 feature band가 필요합니다.

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

<code>main</code>에 푸시할 때마다 GitHub Actions가 새 버전 Release를
게시합니다. 설치 프로그램과 포터블 앱은 최신 Release에만 유지됩니다.

## 기여

기여를 환영합니다. [CONTRIBUTING.md](CONTRIBUTING.md)를 읽어 주십시오.
실제 자격 증명이나 API 키를 이슈, 로그, 테스트 또는 커밋에 포함하지 마십시오.

## 서드 파티 고지

이 앱은 BSD 2-Clause 라이선스의
[Tomlyn](https://github.com/xoofx/Tomlyn)을 사용합니다. 전체 저작권 및
라이선스 고지는 [영문 README](README.md#third-party-notices)를 참조하세요.

## 라이선스

[MIT](LICENSE)

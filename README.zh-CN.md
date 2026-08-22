# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Coding Agent Account Switcher 图标">
</p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

一个简单的 Windows 工具，用来在已保存的 Codex、Claude Code 和 OpenCode
账号或 API 站点配置之间快速切换。

它只切换资料中保存的账号信息和受支持的 API 设置。MCP 服务器、技能、插件、
项目设置和历史记录都保留在原处。

> [!IMPORTANT]
> 这是非官方社区项目，与 OpenAI、Anthropic、Apple 或 OpenCode 项目没有隶属
> 或赞助关系。它不能转移订阅、绕过登录要求或覆盖组织政策。

## 下载

请从 [latest Release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest)
下载当前版本：

- **安装版：** <code>coding-agent-account-switcher-setup-win-x64.exe</code>
- **便携版：** <code>coding-agent-account-switcher-portable-win-x64.exe</code>
- **校验文件：** 每个可执行文件都附带对应的 SHA-256 文件

安装版只为当前用户安装，无需管理员权限，也不会自动开启“随 Windows 启动”。
当前版本尚未签名，因此 Windows SmartScreen 可能显示警告。

## 主要功能

- 保存个人、工作和 API 站点资料，并使用清晰的名称区分。
- 几次点击即可切换 Codex、Claude Code 和 OpenCode 账号。
- 将受支持的 API 地址、密钥和服务商路由保存在对应资料中。
- 双击资料名称即可重命名，也可以删除本地保存的快照。
- 切换成功后明确显示当前资料名称。
- 相关软件仍在运行时自动阻止切换。
- 内置 12 种界面语言。
- 记住明暗主题和语言选择。
- 在同一个 Windows 登录会话中再次启动软件时，会恢复并唤醒已有窗口，不会重复
  打开新窗口。
- 可选择随 Windows 启动，并可在设置中手动检查更新。

所有资料都保存在本机，软件不包含分析或遥测。

## 支持的软件

| 软件 | 会切换的内容 | 保持不变的内容 |
| --- | --- | --- |
| Codex | 登录信息及所选 API 服务商连接配置 | 模型、审查/推理选项、功能、MCP、技能、会话、历史和其他设置 |
| Claude Code | 登录信息及受支持的 API 地址/密钥设置 | 插件、MCP、项目、历史和其他设置 |
| OpenCode | 已保存的登录信息及受支持的提供商/模型设置 | 项目配置和其他设置 |

软件只切换本项目支持的账号相关字段。精确的文件和字段列表请参阅
[架构文档](docs/ARCHITECTURE.md)。

## 快速开始

1. 正常登录账号，或配置要使用的 API 站点。
2. 打开本软件并选择对应的应用。
3. 点击 **保存当前账号**，将其命名为 <code>Personal</code> 等名称。
4. 登录第二个账号或配置另一个 API 站点。
5. 将其保存为 <code>Work</code> 等名称。
6. 需要切换时，先完全关闭相关应用，再选择已保存的资料。

保存账号时无需关闭相关应用。切换前软件会检查相关进程；若仍有应用在运行，会先
提示关闭，并且不会修改文件。

如果当前账号从上次保存后发生变化，软件会先要求确认。如果实际上已经登录了
另一个账号，请先将其保存为新资料。

## 资料与设置

- **重命名：** 双击资料名称。
- **删除：** 点击垃圾桶按钮。只删除本软件的本地加密快照，不会删除账号或退出登录。
- **上次选择：** 表示最近一次由本软件启用的资料。
- **损坏快照：** 缺失或无法读取的快照不会显示为可切换账号，其他正常资料仍可使用。
- **主题和语言：** 都会为当前 Windows 用户记住。
- **随 Windows 启动：** 可选，仅当前用户生效，不需要管理员权限。
- **检查更新：** 只有主动点击时才运行，不会自动下载或安装更新。

## 隐私与安全

- 保存的资料使用当前 Windows 用户的 DPAPI 加密。
- 凭据和 API 密钥不会显示在界面中，也不会写入应用日志。
- 资料保存在 <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code>。
- 切换时使用受保护的文件替换和恢复机制，减少中途失败造成的问题。
- 软件不会上传凭据、资料名称、设备标识符或遥测数据。
- 工作账号可能受组织政策约束，保存额外的本地登录快照前请先获得许可。

报告安全问题前请阅读 [SECURITY.md](SECURITY.md)。

## 使用限制

- 服务端退出、令牌过期、SSO、MFA 或组织政策仍可能要求重新正常登录。
- 现有 Codex 会话会绑定创建时使用的服务商。切换服务商后请新建会话；如需
  继续原会话，请切回原服务商。
- 软件不能在账号之间转移订阅，也不能保证账号永久保持登录。
- 目前仅支持 Windows 10 和 Windows 11 x64。

## 从源代码构建

需要 Windows 和 .NET SDK 8.0.400 或更新的 .NET 8 feature band。

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

每次推送到 <code>main</code> 后，GitHub Actions 都会发布一个带版本号的新
Release。只有最新 Release 保留安装版和便携版下载文件。

## 参与贡献

欢迎贡献，请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。请勿在 Issue、日志、
测试或提交中包含真实凭据或 API 密钥。

## 许可证

[MIT](LICENSE)

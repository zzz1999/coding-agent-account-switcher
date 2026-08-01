# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

一个非官方、以本地使用为核心的 Windows 版 Codex 和 Claude Code 账号切换工具。

Coding Agent Account Switcher 会为 Codex 和 Claude Code 使用的本地认证文件保存具名、加密的快照。它只切换认证快照；常规设置、MCP 配置、技能、插件和项目历史仍保留在原来的位置。

> [!IMPORTANT]
> 本项目与 OpenAI 或 Anthropic 无隶属、认可或赞助关系。它不会转移订阅、绕过登录要求、共享账号，也不会规避服务提供商或组织的政策。

“仿 iOS 18”仅用于描述总体视觉方向。Apple 与本项目无关，项目也没有捆绑任何 Apple 字体、符号、美术资源或商标。

## 下载

请从 [latest Release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest) 下载当前版本：

- **推荐安装程序：** `coding-agent-account-switcher-setup-win-x64.exe`，仅为当前 Windows 用户安装，无需管理员权限，并会创建开始菜单快捷方式和卸载入口。
- **便携版：** 直接下载并运行 `coding-agent-account-switcher-portable-win-x64.exe`，无需安装。
- Release 中为两个可执行文件分别提供对应的 SHA-256 校验文件。

安装程序不会自动启用**开机启动**，也不会触碰 Codex 或 Claude Code 的认证文件与配置文件。卸载软件时会保留加密的账户快照和软件设置，以便重新安装后继续使用。安装程序和便携版可执行文件目前均无数字签名，因此 Windows SmartScreen 可能显示信誉警告。

## 功能

- Windows 原生 WPF 界面，采用仿 iOS 18 的玻璃卡片设计。
- 内置英文、简体中文、繁体中文、西班牙语、法语、德语、日语、韩语、巴西葡萄牙语、俄语、阿拉伯语和印地语界面。
- 可在应用设置中选择显示语言，并选择是否随当前用户的 Windows 会话启动。
- 为 Codex 和 Claude Code 保存具名的个人与工作资料。
- 进程保护：相关应用关闭前不会执行切换。
- 将凭据作为不透明字节处理：不解析令牌、不提取电子邮件，也不记录凭据。
- 使用当前 Windows 用户的 Windows DPAPI 加密资料快照。
- 在同一目录中原子替换凭据，并支持回滚。
- 当实时登录发生变化、即将覆盖“上次选择”的资料快照时要求明确确认。
- 为“上次选择”的资料提供安全的 **恢复快照** 操作，确认与将被替换的实时认证文件的精确字节绑定。
- 完全在本地运行，不含分析或遥测。
- 每次从 `main` 自动生成滚动更新的 Windows `latest` 版本。

## 支持的认证文件

| 提供商 | 要切换的默认认证文件 | 保持不变的配置 |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`、技能、MCP、会话及其他状态 |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`、`.claude.json`、插件、MCP、项目设置及会话历史 |

如果设置了 `CODEX_HOME` 或 `CLAUDE_CONFIG_DIR`，应用会采用相应提供商的认证根目录。它仍然只切换 `auth.json` 或 `.credentials.json`，相邻的配置文件不会改变。

Codex 必须使用文件型凭据存储。如果你的安装使用操作系统凭据存储，请在当前 Codex 根目录（默认是 `%USERPROFILE%\.codex`，设置了 `CODEX_HOME` 时则使用该目录）的 `config.toml` 中添加：

```toml
cli_auth_credentials_store = "file"
```

有关当前的存储约定，请参阅官方 [Codex 认证文档](https://developers.openai.com/codex/auth)和 [Claude Code 认证文档](https://code.claude.com/docs/en/authentication)。

## 工作方式

1. 通过提供商的官方登录流程登录第一个账号。
2. 完全关闭 Codex/Claude Code 以及任何相关的本地客户端或扩展。
3. 使用自定义标签（例如 `Personal`）保存当前登录。
4. 登录第二个账号，并使用另一个标签（例如 `Work`）保存。
5. 选择一个已保存的资料。应用会在做出任何更改前检查相关进程；如果发现进程仍在运行，切换会被阻止，凭据文件不会被修改。
6. 切换到其他资料时，在进行与字节绑定的确认后，当前认证文件会保存回上次选择的加密资料，以保留刷新后的令牌。在恢复日志持久化之前，这些经确认的切换前精确字节也会保存到一个仅用于该事务、经 DPAPI 加密的恢复数据块中。

应用将此资料标为 **上次选择**，而不是“已验证为当前账号”。如果实时文件与保存的快照不再一致，切换会在任何写入前暂停。仅当变化是同一账号的令牌刷新时才应确认。如果你在应用外登录了其他账号，请先选择 **另存为新资料**（或明确替换名称正确的现有资料）。

“上次选择”卡片上的 **恢复快照** 按钮会检查实时文件是否仍与保存的快照一致。如果不一致，应用会警告当前未保存的登录将被替换，并将批准与这些精确字节绑定。其他实时登录无法复用之前的确认。恢复过程中，一份仅用于事务、经 DPAPI 加密的恢复凭据会保存恢复前的字节，直到操作提交或回滚。

当经确认的实时登录与其保存的来源快照不一致时，普通资料切换也会使用相同的加密恢复证据。发生中断后，恢复流程会优先使用这些切换前的精确字节；若使用它们完成还原，也会同步更新来源快照。没有恢复数据块的旧版日志仍然兼容，系统会回退到已保存的来源快照。如果实时文件既不匹配保留的来源，也不匹配目标，日志和加密恢复数据块会继续保留，以供手动恢复。

应用不保证登录永久有效。服务端撤销、组织政策、SSO、MFA 或令牌过期仍可能要求你通过官方客户端正常登录。

## 设置

从应用窗口打开 **设置**，即可选择显示语言或控制应用是否随 Windows 启动。所选语言仅为当前 Windows 用户保存在本地，并可随时再次更改。

**随 Windows 启动** 会在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下添加本应用的条目。它只对当前 Windows 用户生效，不需要管理员权限。关闭此选项只会删除 Coding Agent Account Switcher 自己的启动条目，不会修改其他启动应用。

## 安全模型

- 加密资料数据存储在 `%LOCALAPPDATA%\CodingAgentAccountSwitcher` 下。
- 每个规范化的认证文件位置都有独立的哈希作用域资料库、活动状态、恢复日志和操作互斥锁。因此，改变 `CODEX_HOME` 或 `CLAUDE_CONFIG_DIR` 会启动一套独立资料，而不会复用其他位置的活动账号。
- DPAPI `CurrentUser` 可防止其他 Windows 账号直接解密资料，但无法抵御已经以同一 Windows 用户身份运行的恶意软件。
- 除提供商正常使用的实时认证文件外，解密后的快照字节只会在捕获或切换期间短暂存在于内存和同目录原子替换过程中。
- 认证替换的临时文件和备份文件使用恢复事务 ID。正常完成后会删除这两个精确文件。中断后，恢复流程会从加密的来源快照或仅用于事务的恢复凭据还原缺失的实时文件，随后在删除日志前移除所有由该事务拥有的暂存文件。加密恢复数据块只会在日志删除后以尽力而为的方式删除。
- 应用不会上传凭据；日志、Issue、崩溃报告、测试夹具或仓库提交中也绝不能包含凭据。
- 进程检测是防御性的尽力而为。新启动的进程可能与切换竞争，因此在操作完成前不要启动 Codex 或 Claude Code。
- 如果工作账号由组织管理，请在保留额外的加密本地认证快照前取得批准。

修改凭据处理代码前，请阅读 [SECURITY.md](SECURITY.md) 和 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 从源代码构建

要求：

- Windows 10 或 Windows 11
- .NET SDK 8.0.400 或更新的 .NET 8 feature band

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

创建自包含的 Windows x64 构建：

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## 滚动 latest 版本

每次推送到 `main` 时，`.github/workflows/latest-release.yml` 都会运行：

1. 还原依赖并测试解决方案。
2. 发布自包含的 Windows x64 便携版。
3. 构建面向当前用户的 Windows x64 安装程序。
4. 准备便携版可执行文件，并为安装程序和便携版分别生成 SHA-256 校验和。
5. 只删除先前名为 `latest` 的 Release 和标签。
6. 为当前提交创建新的 `latest` Release，并发布安装程序、便携版可执行文件和校验文件。

此工作流绝不会删除带版本号的 Release。滚动 `latest` 标签要求关闭 GitHub 的 **immutable releases** 选项，并且分支或标签规则必须允许工作流删除 `latest`。要求不可变 Release 的仓库应把工作流改为使用唯一的构建标签。

滚动版本中的安装程序和便携版可执行文件目前均无数字签名，因此 Windows SmartScreen 可能显示信誉警告。运行前请检查源代码，并验证对应的 SHA-256 校验和。

## 参与贡献

欢迎贡献。请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。测试必须使用临时的虚假凭据文件，绝不能访问开发者真实的 Codex 或 Claude Code 认证文件。

## 许可证

[MIT](LICENSE)

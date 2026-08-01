# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

一个非官方、以本地使用为核心的 Windows 版 Codex、Claude Code 与 OpenCode 账号及 API 站点切换工具。

Coding Agent Account Switcher 会将本地凭据与重新连接同一资料所需的少量 API 提供商字段一起保存为具名、加密的快照。其他设置、MCP 配置、技能、插件和项目历史仍保留在原来的位置。

> [!IMPORTANT]
> 本项目与 OpenAI 或 Anthropic 无隶属、认可或赞助关系。它不会转移订阅、绕过登录要求、共享账号，也不会规避服务提供商或组织的政策。

“仿 iOS 18”仅用于描述总体视觉方向。Apple 与本项目无关，项目也没有捆绑任何 Apple 字体、符号、美术资源或商标。

## 下载

请从 [latest Release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest) 下载当前版本：

- **推荐安装程序：** `coding-agent-account-switcher-setup-win-x64.exe`，仅为当前 Windows 用户安装，无需管理员权限，并会创建开始菜单快捷方式和卸载入口。
- **便携版：** 直接下载并运行 `coding-agent-account-switcher-portable-win-x64.exe`，无需安装。
- Release 中为两个可执行文件分别提供对应的 SHA-256 校验文件。

安装程序不会自动启用**开机启动**，也不会触碰 Codex、Claude Code 或 OpenCode 的认证文件与配置文件。卸载软件时会保留加密的账户快照和软件设置，以便重新安装后继续使用。安装程序和便携版可执行文件目前均无数字签名，因此 Windows SmartScreen 可能显示信誉警告。

## 功能

- Windows 原生 WPF 界面，采用仿 iOS 18 的玻璃卡片设计。
- 内置英文、简体中文、繁体中文、西班牙语、法语、德语、日语、韩语、巴西葡萄牙语、俄语、阿拉伯语和印地语界面。
- 可在应用设置中选择显示语言，并选择是否随当前用户的 Windows 会话启动。
- 为 Codex、Claude Code 和 OpenCode 保存具名的个人、工作及 API 站点资料。
- 进程保护：相关应用关闭前不会执行切换。
- 认证文件始终按不透明字节处理；仅解析并合并下方列出的受管配置字段，秘密值绝不会显示或写入日志。
- 使用当前 Windows 用户的 Windows DPAPI 加密资料快照。
- 在同一目录中原子替换受管文件，并支持回滚。
- 当实时登录发生变化、即将覆盖“上次选择”的资料快照时要求明确确认。
- 为“上次选择”的资料提供安全的 **恢复快照** 操作，确认与将被替换的实时认证文件的精确字节绑定。
- 完全在本地运行，不含分析或遥测。
- 每次从 `main` 自动生成滚动更新的 Windows `latest` 版本。

## 支持的账号及 API 配置

| 提供商 | 受管文件 | 选择性管理的配置 |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` 和 `config.toml` | `model_provider`、`openai_base_url`、`model`、`review_model`、`model_reasoning_effort`、`disable_response_storage`、当前选中的 `model_providers` 表，以及 `features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` 和 `settings.json` | 仅 `env.ANTHROPIC_BASE_URL`、`env.ANTHROPIC_API_KEY`、`env.ANTHROPIC_AUTH_TOKEN`、`env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`，以及兼容字段 `env.CLAUDE_CODE_ATTRIBUTION_HEADER` |
| OpenCode | 可选的 `%USERPROFILE%\.local\share\opencode\auth.json`；全局 `config.json`、`opencode.json`、`opencode.jsonc`；设置时再加载 `OPENCODE_CONFIG` | 不透明的 `/connect` 凭据，以及合并后生效的顶层 `provider`、`model` 和 `small_model` API 资料 |

应用会把这些字段合并到实时配置中，而不是替换整个文件。Codex 的 `network_access`、`windows_wsl_setup_acknowledged`、`features.goals`、`cli_auth_credentials_store`、MCP、技能、会话及其他值均保持不变；Claude Code 的其他 `env` 项、`.claude.json`、插件、MCP、项目设置和会话历史保持不变；OpenCode 每个参与合并的全局或自定义文件中的无关语义值均保持不变。

OpenCode 的默认配置根目录是 `%USERPROFILE%\.config`，默认数据根目录是 `%USERPROFILE%\.local\share`。仅当 `XDG_CONFIG_HOME` 或 `XDG_DATA_HOME` 是绝对路径时，应用才会用它替换相应的默认根目录；空值不会替换默认目录。所有非空 OpenCode 路径覆盖项（`XDG_CONFIG_HOME`、`XDG_DATA_HOME`、`OPENCODE_CONFIG`、`OPENCODE_CONFIG_DIR`）都必须是绝对路径；相对路径会在写入任何文件前阻止捕获和切换。全局配置从配置根目录下的 `opencode\config.json`、`opencode.json`、`opencode.jsonc` 依次加载，后者覆盖前者；设置 `OPENCODE_CONFIG` 时最后加载该文件。应用会把最终生效的 `provider`、`model` 和 `small_model` 捕获为一个 API 资料；`/connect` 凭据从数据根目录下的 `opencode\auth.json` 读取。

OpenCode 同时支持两种凭据形式：由 `/connect` 写入单独 `auth.json` 的凭据，以及嵌入受管 `provider` 对象中的 API 密钥。快照会包含实际存在的一种或两种形式，同时保留其他 OpenCode 配置键。

应用 OpenCode 资料时，会从其他全局层清除 `provider`、`model` 和 `small_model`，避免陈旧低层端点覆盖目标。设置 `OPENCODE_CONFIG` 时，目标值写入该文件并清理三个全局层；未设置时，非空目标统一写入全局 `opencode.jsonc`，并从 `config.json` 和 `opencode.json` 清除，即使之前只有 JSON 或旧版文件。仅含认证或受管配置为空的快照仍有效：它会清除已有受管值，但不会新建空的 `opencode.jsonc`。

选择性合并会保留无关配置的语义值；如果 JSON 或 TOML 必须重新序列化，则不保证逐字节保留原来的排版与注释。

捕获或切换 OpenCode 前，应用会只读检查已知的高优先级环境覆盖项。只要 `OPENCODE_AUTH_CONTENT` 不是空白内容，操作就会被阻止。`OPENCODE_CONFIG_CONTENT` 只有在内容是有效 JSON/JSONC 且不含顶层 `provider`、`model` 或 `small_model` 时才允许使用；无效内容或任一受管键都会阻止操作。设置 `OPENCODE_CONFIG_DIR` 时，应用会检查其中的 `opencode.json` 和 `opencode.jsonc`；文件不可读取、格式无效或包含任一受管键都会阻止操作。只包含无关键的内联配置或目录配置可以保留，应用不会修改这些环境变量提供的来源。

如果设置了 `CODEX_HOME` 或 `CLAUDE_CONFIG_DIR`，应用会采用相应提供商的根目录。OpenCode 的项目级配置、集中管理来源和提供商专用环境变量仍不受应用管理，切换后仍可能覆盖选中的全局资料；应用不会搜索或修改它们。OpenCode 进程保护会同时检查 `opencode` 和 `opencode-cli`。

`disable_response_storage`、`features.responses_websockets_v2` 和 `CLAUDE_CODE_ATTRIBUTION_HEADER` 是为仍使用它们的 API 站点保留的兼容字段；列在这里并不表示每个提供商的当前版本都正式记录了这些字段。

Codex 必须使用文件型凭据存储。如果你的安装使用操作系统凭据存储，请在当前 Codex 根目录（默认是 `%USERPROFILE%\.codex`，设置了 `CODEX_HOME` 时则使用该目录）的 `config.toml` 中添加：

```toml
cli_auth_credentials_store = "file"
```

有关当前的存储约定，请参阅官方 [Codex 认证文档](https://developers.openai.com/codex/auth)和 [Claude Code 认证文档](https://code.claude.com/docs/en/authentication)。

## 工作方式

1. 通过提供商的官方流程登录，或在提供商的常规文件中配置支持的 API 站点。
2. 完全关闭 Codex、Claude Code、OpenCode 以及任何相关的本地客户端或扩展。
3. 使用自定义标签（例如 `Personal`）保存当前账号和受管 API 设置。
4. 登录第二个账号或配置另一个 API 站点，并使用另一个标签（例如 `Work`）保存。
5. 选择一个已保存的资料。应用会在做出任何更改前检查相关进程；如果发现进程仍在运行，切换会被阻止，受管文件不会被修改。
6. 切换到其他资料时，在进行与内容绑定的确认后，当前受管快照会保存回上次选择的加密资料，以保留刷新后的令牌和有意进行的 API 更改。恢复日志持久化前，切换前的精确快照也会保存到仅用于该事务、经 DPAPI 加密的恢复数据块中。

旧版仅含原始凭据的 Codex 和 Claude Code 资料仍可读取，并会被解释为“凭据 + 空的受管 API 配置”。激活这类资料会清除当前受管的 API 路由和模型字段，避免旧凭据继续使用上一个资料的第三方端点。激活后请设置所需的模型/API 配置并重新捕获资料；当活动的旧版来源资料被切换离开时，它会升级为组合格式。

应用将此资料标为 **上次选择**，而不是“已验证为当前账号”。如果实时文件与保存的快照不再一致，切换会在任何写入前暂停。仅当变化是同一账号的令牌刷新时才应确认。如果你在应用外登录了其他账号，请先选择 **另存为新资料**（或明确替换名称正确的现有资料）。

“上次选择”卡片上的 **恢复快照** 按钮会检查实时文件是否仍与保存的快照一致。如果不一致，应用会警告当前未保存的登录将被替换，并将批准与这些精确字节绑定。其他实时登录无法复用之前的确认。恢复过程中，一份仅用于事务、经 DPAPI 加密的恢复凭据会保存恢复前的字节，直到操作提交或回滚。

每次组合或多文件事务都会在写入日志前，将当前受管快照原样写入加密恢复数据块，即使它与已保存的来源相同。这样既能精确回滚部分文件写入，也能安全升级旧版仅含凭据的来源资料。中断后，恢复流程会优先使用这份切换前快照；若使用它完成还原，也会同步来源资料。没有恢复数据块的旧版日志仍会回退到已保存的来源。如果实时受管状态既不匹配保留的来源，也不匹配目标，日志和加密恢复数据块会继续保留，以供手动恢复。

多文件提交采用失效关闭顺序：先删除单独的实时认证文件，再原子合并配置，最后安装目标认证文件。中断时认证可能暂时缺失，但不会出现某个资料的凭据与另一个资料的 API 端点配对；恢复流程会使用加密的切换前快照安全完成或回滚。

应用不保证登录永久有效。服务端撤销、组织政策、SSO、MFA 或令牌过期仍可能要求你通过官方客户端正常登录。

## 设置

从应用窗口打开 **设置**，即可选择显示语言或控制应用是否随 Windows 启动。所选语言仅为当前 Windows 用户保存在本地，并可随时再次更改。

**随 Windows 启动** 会在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下添加本应用的条目。它只对当前 Windows 用户生效，不需要管理员权限。关闭此选项只会删除 Coding Agent Account Switcher 自己的启动条目，不会修改其他启动应用。

## 安全模型

- 加密资料数据存储在 `%LOCALAPPDATA%\CodingAgentAccountSwitcher` 下。
- 每组规范化的受管文件位置都有独立的哈希作用域资料库、活动状态、恢复日志和操作互斥锁。因此，改变 `CODEX_HOME`、`CLAUDE_CONFIG_DIR` 或 `OPENCODE_CONFIG` 会启动一套独立资料，而不会复用其他位置的活动账号。
- DPAPI `CurrentUser` 可防止其他 Windows 账号直接解密资料，但无法抵御已经以同一 Windows 用户身份运行的恶意软件。
- 除提供商正常使用的实时文件外，解密后的快照字节只会在捕获或切换期间短暂存在于内存和同目录原子替换过程中。
- 认证替换的临时文件和备份文件使用恢复事务 ID。正常完成后会删除这两个精确文件。中断后，恢复流程会从加密的来源快照或仅用于事务的恢复凭据还原缺失的实时文件，随后在删除日志前移除所有由该事务拥有的暂存文件。加密恢复数据块只会在日志删除后以尽力而为的方式删除。
- 应用不会上传凭据；日志、Issue、崩溃报告、测试夹具或仓库提交中也绝不能包含凭据。
- 进程检测是防御性的尽力而为。新启动的进程可能与切换竞争，因此在操作完成前不要启动 Codex、Claude Code 或 OpenCode。
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

欢迎贡献。请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。测试必须使用临时的合成凭据和配置文件，绝不能访问开发者真实的 Codex、Claude Code 或 OpenCode 文件。

## 许可证

[MIT](LICENSE)

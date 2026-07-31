# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

一個非官方、以本機使用為核心的 Windows 版 Codex 與 Claude Code 帳號切換工具。

Coding Agent Account Switcher 會為 Codex 與 Claude Code 使用的本機驗證檔案儲存具名、加密的快照。它只會切換驗證快照；一般設定、MCP 設定、技能、外掛程式與專案歷程都會留在原本的位置。

> [!IMPORTANT]
> 本專案與 OpenAI 或 Anthropic 沒有隸屬、背書或贊助關係。它不會轉移訂閱、繞過登入要求、共享帳號，也不會規避服務供應商或組織的政策。

「仿 iOS 18」僅描述整體視覺方向。Apple 與本專案無關，專案也未附帶任何 Apple 字型、符號、美術資源或商標。

## 下載

請從 [latest Release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest) 下載目前版本：

- **建議安裝程式：** `coding-agent-account-switcher-setup-win-x64.exe`，只為目前的 Windows 使用者安裝，不需要系統管理員權限，並會建立開始功能表捷徑及解除安裝項目。
- **可攜版：** `coding-agent-account-switcher-win-x64.zip`，解壓縮後即可執行，不需要安裝。
- Release 會為兩個套件分別提供對應的 SHA-256 總和檢查檔。

安裝程式不會自動啟用**隨 Windows 啟動**，也不會碰觸 Codex 或 Claude Code 的驗證檔案與設定檔。解除安裝時會保留加密的帳號快照和應用程式設定，重新安裝後仍可使用。安裝程式及可攜版執行檔目前皆未經數位簽章，因此 Windows SmartScreen 可能顯示信譽警告。

## 功能

- Windows 原生 WPF 介面，採用仿 iOS 18 的玻璃卡片設計。
- 內建英文、簡體中文、繁體中文、西班牙文、法文、德文、日文、韓文、巴西葡萄牙文、俄文、阿拉伯文與印地文介面。
- 可在應用程式設定中選擇顯示語言，並選擇是否隨目前使用者的 Windows 工作階段啟動。
- 為 Codex 與 Claude Code 儲存具名的個人及工作設定檔。
- 程序防護：相關應用程式關閉前不會執行切換。
- 將認證當作不透明位元組處理：不剖析權杖、不擷取電子郵件，也不記錄認證。
- 使用目前 Windows 使用者的 Windows DPAPI 加密設定檔快照。
- 在同一目錄內以不可分割方式取代認證，並支援回復。
- 當即時登入已變更、將覆寫「上次選取」的設定檔快照時要求明確確認。
- 為「上次選取」的設定檔提供安全的 **還原快照** 動作，確認會綁定到將被取代的即時驗證檔案之精確位元組。
- 完全在本機運作，不含分析或遙測。
- 每次從 `main` 自動產生滾動更新的 Windows `latest` 版本。

## 支援的驗證檔案

| 供應商 | 預設切換的驗證檔案 | 保持不變的設定 |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`、技能、MCP、工作階段及其他狀態 |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`、`.claude.json`、外掛程式、MCP、專案設定及工作階段歷程 |

若已設定 `CODEX_HOME` 或 `CLAUDE_CONFIG_DIR`，應用程式會採用對應供應商的驗證根目錄。它仍只會切換 `auth.json` 或 `.credentials.json`，相鄰的設定檔不會變更。

Codex 必須使用檔案型認證儲存。如果你的安裝使用作業系統認證存放區，請在目前 Codex 根目錄（預設為 `%USERPROFILE%\.codex`；設定 `CODEX_HOME` 時則使用該目錄）的 `config.toml` 加入：

```toml
cli_auth_credentials_store = "file"
```

目前的儲存契約請參閱官方 [Codex 驗證文件](https://developers.openai.com/codex/auth)及 [Claude Code 驗證文件](https://code.claude.com/docs/en/authentication)。

## 運作方式

1. 透過供應商的官方登入流程登入第一個帳號。
2. 完全關閉 Codex/Claude Code 以及任何相關的本機用戶端或擴充功能。
3. 使用自訂標籤（例如 `Personal`）儲存目前登入。
4. 登入第二個帳號，再以另一個標籤（例如 `Work`）儲存。
5. 選取已儲存的設定檔。應用程式會在任何變更前檢查相關程序；若仍有程序執行，切換會遭阻擋，認證檔案不會被修改。
6. 切換到其他設定檔時，在完成綁定位元組的確認後，目前驗證檔案會存回上次選取的加密設定檔，以保留更新後的權杖。

應用程式會將此設定檔標示為 **上次選取**，而非「已驗證為目前帳號」。如果即時檔案與儲存的快照不再相符，切換會在任何寫入前暫停。只有在變更是同一帳號的權杖更新時才應確認。若你在應用程式外登入了其他帳號，請先選擇 **另存為新設定檔**（或明確取代名稱正確的現有設定檔）。

「上次選取」卡片上的 **還原快照** 按鈕會檢查即時檔案是否仍與儲存的快照相符。若不相符，應用程式會警告目前未儲存的登入將被取代，並將核准綁定到這些精確位元組。其他即時登入無法重複使用先前的確認。還原期間，只供交易使用且經 DPAPI 加密的復原認證會保留還原前的位元組，直到操作提交或回復。

應用程式不保證登入永久有效。服務端撤銷、組織政策、SSO、MFA 或權杖到期仍可能要求你透過官方用戶端正常登入。

## 設定

從應用程式視窗開啟 **設定**，即可選擇顯示語言或控制應用程式是否隨 Windows 啟動。選取的語言只會為目前 Windows 使用者儲存在本機，並可隨時再次變更。

**隨 Windows 啟動** 會在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下新增本應用程式的項目。它只對目前 Windows 使用者生效，不需要系統管理員權限。關閉此選項只會移除 Coding Agent Account Switcher 自己的啟動項目，不會修改其他啟動應用程式。

## 安全模型

- 加密設定檔資料儲存在 `%LOCALAPPDATA%\CodingAgentAccountSwitcher` 下。
- 每個正規化的驗證檔案位置都有獨立的雜湊範圍保存庫、作用中狀態、復原日誌及操作互斥鎖。因此，變更 `CODEX_HOME` 或 `CLAUDE_CONFIG_DIR` 會建立一組獨立設定檔，而不會重複使用其他位置的作用中帳號。
- DPAPI `CurrentUser` 可防止其他 Windows 帳號直接解密設定檔，但無法防範已經以相同 Windows 使用者身分執行的惡意軟體。
- 除供應商正常使用的即時驗證檔案外，解密後的快照位元組只會在擷取或切換期間短暫存在於記憶體及同目錄不可分割取代程序中。
- 驗證取代的暫存及備份檔案使用復原交易 ID。正常完成後會移除這兩個精確檔案。中斷後，復原程序會從加密的來源快照或僅供交易使用的復原認證還原缺少的即時檔案，之後在刪除日誌前移除所有屬於該交易的暫存檔案。
- 應用程式不會上傳認證；記錄、Issue、損毀報告、測試資料或存放庫提交中也絕不能包含認證。
- 程序偵測是防禦性的盡力而為。新啟動的程序可能與切換產生競爭，因此在操作完成前不要啟動 Codex 或 Claude Code。
- 若工作帳號由組織管理，保留額外的加密本機驗證快照前請先取得核准。

修改認證處理程式碼前，請閱讀 [SECURITY.md](SECURITY.md) 與 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 從原始碼建置

需求：

- Windows 10 或 Windows 11
- .NET SDK 8.0.400 或更新的 .NET 8 feature band

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

建立自包含的 Windows x64 組建：

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## 滾動 latest 版本

每次推送至 `main` 時，`.github/workflows/latest-release.yml` 都會執行：

1. 還原相依套件並測試方案。
2. 發佈自包含的 Windows x64 可攜版。
3. 建置目前使用者層級的 Windows x64 安裝程式。
4. 建立可攜版 ZIP，並分別為安裝程式與可攜版產生 SHA-256 總和檢查碼。
5. 只刪除先前名為 `latest` 的 Release 及標籤。
6. 為目前提交建立新的 `latest` Release，並發佈安裝程式、可攜版及總和檢查檔。

此工作流程絕不會刪除有版本號的 Release。滾動 `latest` 標籤必須停用 GitHub 的 **immutable releases** 選項，而且分支或標籤規則必須允許工作流程刪除 `latest`。要求不可變 Release 的存放庫應將工作流程改為使用唯一的組建標籤。

滾動版本中的安裝程式與可攜版執行檔目前皆未經數位簽章，因此 Windows SmartScreen 可能顯示信譽警告。執行前請檢查原始碼，並驗證對應的 SHA-256 總和檢查碼。

## 參與貢獻

歡迎貢獻。請閱讀 [CONTRIBUTING.md](CONTRIBUTING.md)。測試必須使用暫時的虛假認證檔案，絕不能存取開發者真實的 Codex 或 Claude Code 驗證檔案。

## 授權條款

[MIT](LICENSE)

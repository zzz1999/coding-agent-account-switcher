# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

一個非官方、以本機使用為核心的 Windows 版 Codex、Claude Code 與 OpenCode 帳號及 API 站點切換工具。

Coding Agent Account Switcher 會將本機認證與重新連線相同設定檔所需的少量 API 供應商欄位一起儲存為具名、加密的快照。其他設定、MCP 設定、技能、外掛程式與專案歷程仍留在原本的位置。

> [!IMPORTANT]
> 本專案與 OpenAI 或 Anthropic 沒有隸屬、背書或贊助關係。它不會轉移訂閱、繞過登入要求、共享帳號，也不會規避服務供應商或組織的政策。

「仿 iOS 18」僅描述整體視覺方向。Apple 與本專案無關，專案也未附帶任何 Apple 字型、符號、美術資源或商標。

## 下載

請從 [latest Release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest) 下載目前版本：

- **建議安裝程式：** `coding-agent-account-switcher-setup-win-x64.exe`，只為目前的 Windows 使用者安裝，不需要系統管理員權限，並會建立開始功能表捷徑及解除安裝項目。
- **可攜版：** 直接下載並執行 `coding-agent-account-switcher-portable-win-x64.exe`，不需要安裝。
- Release 會為兩個執行檔分別提供對應的 SHA-256 總和檢查檔。

安裝程式不會自動啟用**隨 Windows 啟動**，也不會碰觸 Codex、Claude Code 或 OpenCode 的驗證檔案與設定檔。解除安裝時會保留加密的帳號快照和應用程式設定，重新安裝後仍可使用。安裝程式及可攜版執行檔目前皆未經數位簽章，因此 Windows SmartScreen 可能顯示信譽警告。

## 功能

- Windows 原生 WPF 介面，採用仿 iOS 18 的玻璃卡片設計。
- 內建英文、簡體中文、繁體中文、西班牙文、法文、德文、日文、韓文、巴西葡萄牙文、俄文、阿拉伯文與印地文介面。
- 可在應用程式設定中選擇顯示語言，並選擇是否隨目前使用者的 Windows 工作階段啟動。
- 為 Codex、Claude Code 與 OpenCode 儲存具名的個人、工作及 API 站點設定檔。
- 程序防護：相關應用程式關閉前不會執行切換。
- 驗證檔案始終視為不透明位元組；只會剖析並合併下方列出的受管理設定欄位，秘密值絕不顯示或寫入記錄。
- 使用目前 Windows 使用者的 Windows DPAPI 加密設定檔快照。
- 在同一目錄內以不可分割方式取代受管理檔案，並支援回復。
- 當即時登入已變更、將覆寫「上次選取」的設定檔快照時要求明確確認。
- 為「上次選取」的設定檔提供安全的 **還原快照** 動作，確認會綁定到將被取代的即時驗證檔案之精確位元組。
- 完全在本機運作，不含分析或遙測。
- 每次從 `main` 自動產生滾動更新的 Windows `latest` 版本。

## 支援的帳號及 API 設定

| 供應商 | 受管理檔案 | 選擇性管理的設定 |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` 與 `config.toml` | `model_provider`、`openai_base_url`、`model`、`review_model`、`model_reasoning_effort`、`disable_response_storage`、目前選取的 `model_providers` 資料表，以及 `features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` 與 `settings.json` | 僅 `env.ANTHROPIC_BASE_URL`、`env.ANTHROPIC_API_KEY`、`env.ANTHROPIC_AUTH_TOKEN`、`env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`，以及相容欄位 `env.CLAUDE_CODE_ATTRIBUTION_HEADER` |
| OpenCode | 選用的 `%USERPROFILE%\.local\share\opencode\auth.json`；全域 `config.json`、`opencode.json`、`opencode.jsonc`；設定時再載入 `OPENCODE_CONFIG` | 不透明的 `/connect` 認證，以及合併後生效的最上層 `provider`、`model` 與 `small_model` API 設定檔 |

應用程式會將這些欄位合併至即時設定，而不是取代整個檔案。Codex 的 `network_access`、`windows_wsl_setup_acknowledged`、`features.goals`、`cli_auth_credentials_store`、MCP、技能、工作階段及其他值皆保持不變；Claude Code 的其他 `env` 項目、`.claude.json`、外掛程式、MCP、專案設定及工作階段歷程保持不變；OpenCode 每個參與合併的全域或自訂檔案中的無關語意值均保持不變。

OpenCode 的預設設定根目錄為 `%USERPROFILE%\.config`，預設資料根目錄為 `%USERPROFILE%\.local\share`。只有當 `XDG_CONFIG_HOME` 或 `XDG_DATA_HOME` 是絕對路徑時，應用程式才會用它取代對應的預設根目錄；空值不會取代預設目錄。所有非空 OpenCode 路徑覆寫（`XDG_CONFIG_HOME`、`XDG_DATA_HOME`、`OPENCODE_CONFIG`、`OPENCODE_CONFIG_DIR`）都必須是絕對路徑；相對路徑會在寫入任何檔案前阻擋擷取與切換。全域設定會依序從設定根目錄下的 `opencode\config.json`、`opencode.json`、`opencode.jsonc` 載入，後者覆寫前者；設定 `OPENCODE_CONFIG` 時最後載入該檔案。應用程式會把最終生效的 `provider`、`model` 與 `small_model` 擷取為一個 API 設定檔；`/connect` 認證則從資料根目錄下的 `opencode\auth.json` 讀取。

OpenCode 同時支援兩種認證形式：由 `/connect` 寫入獨立 `auth.json` 的認證，以及嵌入受管理 `provider` 物件中的 API 金鑰。快照會包含實際存在的一種或兩種形式，同時保留其他 OpenCode 設定鍵。

套用 OpenCode 設定檔時，會從其他全域層清除 `provider`、`model` 與 `small_model`，避免陳舊低層端點覆寫目標。設定 `OPENCODE_CONFIG` 時，目標值寫入該檔案並清理三個全域層；未設定時，非空目標統一寫入全域 `opencode.jsonc`，並從 `config.json` 與 `opencode.json` 清除，即使先前只有 JSON 或舊版檔案。僅含認證或受管理設定為空的快照仍有效：它會清除既有受管理值，但不會新建空的 `opencode.jsonc`。

選擇性合併會保留無關設定的語意值；若 JSON 或 TOML 必須重新序列化，則不保證逐位元組保留原有排版與註解。

擷取或切換 OpenCode 前，應用程式會以唯讀方式檢查已知的高優先級環境覆寫。只要 `OPENCODE_AUTH_CONTENT` 不是空白內容，操作就會被阻擋。`OPENCODE_CONFIG_CONTENT` 只有在內容是有效 JSON/JSONC 且不含最上層 `provider`、`model` 或 `small_model` 時才允許使用；無效內容或任何受管理鍵都會阻擋操作。設定 `OPENCODE_CONFIG_DIR` 時，應用程式會檢查其中的 `opencode.json` 與 `opencode.jsonc`；檔案無法讀取、格式無效或包含任何受管理鍵都會阻擋操作。只含無關鍵的內嵌或目錄設定可以保留，應用程式不會修改這些由環境變數提供的來源。

若已設定 `CODEX_HOME` 或 `CLAUDE_CONFIG_DIR`，應用程式會採用相應供應商的根目錄。OpenCode 的專案層級設定、集中管理來源及供應商專用環境變數仍不受應用程式管理，切換後仍可能覆寫選取的全域設定檔；應用程式不會搜尋或修改它們。OpenCode 程序防護會同時檢查 `opencode` 與 `opencode-cli`。

`disable_response_storage`、`features.responses_websockets_v2` 與 `CLAUDE_CODE_ATTRIBUTION_HEADER` 是為仍使用它們的 API 站點保留的相容欄位；列在此處並不表示每個供應商的目前版本都正式記載了這些欄位。

Codex 必須使用檔案型認證儲存。如果你的安裝使用作業系統認證存放區，請在目前 Codex 根目錄（預設為 `%USERPROFILE%\.codex`；設定 `CODEX_HOME` 時則使用該目錄）的 `config.toml` 加入：

```toml
cli_auth_credentials_store = "file"
```

目前的儲存契約請參閱官方 [Codex 驗證文件](https://developers.openai.com/codex/auth)及 [Claude Code 驗證文件](https://code.claude.com/docs/en/authentication)。

## 運作方式

1. 透過供應商的官方流程登入，或在供應商的一般檔案中設定支援的 API 站點。
2. 完全關閉 Codex、Claude Code、OpenCode 以及任何相關的本機用戶端或擴充功能。
3. 使用自訂標籤（例如 `Personal`）儲存目前帳號與受管理 API 設定。
4. 登入第二個帳號或設定另一個 API 站點，再以另一個標籤（例如 `Work`）儲存。
5. 選取已儲存的設定檔。應用程式會在任何變更前檢查相關程序；若仍有程序執行，切換會遭阻擋，受管理檔案不會被修改。
6. 切換到其他設定檔時，在完成綁定內容的確認後，目前受管理快照會存回上次選取的加密設定檔，以保留更新後的權杖及刻意進行的 API 變更。復原日誌持久化前，切換前的精確快照也會保存到僅供該交易使用、經 DPAPI 加密的復原資料區塊中。

舊版僅含原始認證的 Codex 與 Claude Code 設定檔仍可讀取，並會解讀為「認證 + 空的受管理 API 設定」。啟用這類設定檔會清除目前受管理的 API 路由及模型欄位，避免舊認證沿用上一個設定檔的第三方端點。啟用後請設定所需模型/API 並重新擷取；當作用中的舊版來源設定檔被切換離開時，它會升級為組合格式。

應用程式會將此設定檔標示為 **上次選取**，而非「已驗證為目前帳號」。如果即時檔案與儲存的快照不再相符，切換會在任何寫入前暫停。只有在變更是同一帳號的權杖更新時才應確認。若你在應用程式外登入了其他帳號，請先選擇 **另存為新設定檔**（或明確取代名稱正確的現有設定檔）。

「上次選取」卡片上的 **還原快照** 按鈕會檢查即時檔案是否仍與儲存的快照相符。若不相符，應用程式會警告目前未儲存的登入將被取代，並將核准綁定到這些精確位元組。其他即時登入無法重複使用先前的確認。還原期間，只供交易使用且經 DPAPI 加密的復原認證會保留還原前的位元組，直到操作提交或回復。

每次組合或多檔案交易都會在寫入日誌前，將目前受管理快照原樣寫入加密復原資料區塊，即使它與已儲存的來源相同。這樣既能精確回復部分檔案寫入，也能安全升級舊版僅含認證的來源設定檔。中斷後，復原程序會優先使用這份切換前快照；若以它完成還原，也會同步來源設定檔。沒有復原資料區塊的舊版日誌仍會回復使用已儲存的來源。若即時受管理狀態既不符合來源，也不符合目標，日誌與加密復原資料區塊會繼續保留，以供手動復原。

多檔案提交採用失效關閉順序：先刪除獨立的即時驗證檔案，再以不可分割方式合併設定，最後安裝目標驗證檔案。中斷時驗證可能暫時缺少，但不會把某個設定檔的認證與另一個設定檔的 API 端點配對；復原程序會使用加密的切換前快照安全完成或回復。

應用程式不保證登入永久有效。服務端撤銷、組織政策、SSO、MFA 或權杖到期仍可能要求你透過官方用戶端正常登入。

重新命名及刪除設定檔只會操作本機加密快照保存庫。重新命名只會變更已儲存的名稱與中繼資料，不會變更快照內容；刪除只會移除選取的本機加密快照。刪除「上次選取」的設定檔也會清除應用程式內的作用中設定檔關聯，但不會登出，也不會修改供應商的即時驗證或設定檔案。再次切換前請先儲存目前帳號。若中斷的切換交易仍待復原，這兩項操作都會遭到拒絕。

## 設定

從應用程式視窗開啟 **設定**，即可選擇顯示語言或控制應用程式是否隨 Windows 啟動。選取的語言只會為目前 Windows 使用者儲存在本機，並可隨時再次變更。

**隨 Windows 啟動** 會在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下新增本應用程式的項目。它只對目前 Windows 使用者生效，不需要系統管理員權限。關閉此選項只會移除 Coding Agent Account Switcher 自己的啟動項目，不會修改其他啟動應用程式。

**檢查更新** 完全由使用者主動觸發。只有按下此按鈕後，應用程式才會向本存放庫的官方 GitHub API 傳送一次匿名 HTTPS `GET` 請求；啟動時、背景或定期都不會檢查更新。請求不會上傳認證、設定、設定檔名稱、裝置識別碼或應用程式遙測。應用程式只比較 Release 中繼資料，絕不會自動下載或執行安裝程式或可攜版。

## 安全模型

- 加密設定檔資料儲存在 `%LOCALAPPDATA%\CodingAgentAccountSwitcher` 下。
- 每組正規化的受管理檔案位置都有獨立的雜湊範圍保存庫、作用中狀態、復原日誌及操作互斥鎖。因此，變更 `CODEX_HOME`、`CLAUDE_CONFIG_DIR` 或 `OPENCODE_CONFIG` 會建立一組獨立設定檔，而不會重複使用其他位置的作用中帳號。
- DPAPI `CurrentUser` 可防止其他 Windows 帳號直接解密設定檔，但無法防範已經以相同 Windows 使用者身分執行的惡意軟體。
- 除供應商正常使用的即時檔案外，解密後的快照位元組只會在擷取或切換期間短暫存在於記憶體及同目錄不可分割取代程序中。
- 驗證取代的暫存及備份檔案使用復原交易 ID。正常完成後會移除這兩個精確檔案。中斷後，復原程序會從加密的來源快照或僅供交易使用的復原認證還原缺少的即時檔案，之後在刪除日誌前移除所有屬於該交易的暫存檔案。加密復原資料區塊只會在日誌刪除後，以盡力而為的方式移除。
- 應用程式不會上傳認證；記錄、Issue、損毀報告、測試資料或存放庫提交中也絕不能包含認證。
- 程序偵測是防禦性的盡力而為。新啟動的程序可能與切換產生競爭，因此在操作完成前不要啟動 Codex、Claude Code 或 OpenCode。
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
4. 準備可攜版執行檔，並分別為安裝程式與可攜版產生 SHA-256 總和檢查碼。
5. 只刪除先前名為 `latest` 的 Release 及標籤。
6. 為目前提交建立新的 `latest` Release，並發佈安裝程式、可攜版執行檔及總和檢查檔。工作流程使用同一個 `APP_VERSION` 設定執行檔版本，並在 Release 說明中寫入精確的機器可讀標記：`<!-- coding-agent-account-switcher-version: 0.1.N -->`。

由於 `latest` 標籤會滾動更新，只有使用者主動檢查更新時，應用程式才會從官方 GitHub Release API 回應中讀取該標記。它不會在背景檢查，也不會自動下載或執行任何發佈資產。

此工作流程絕不會刪除有版本號的 Release。滾動 `latest` 標籤必須停用 GitHub 的 **immutable releases** 選項，而且分支或標籤規則必須允許工作流程刪除 `latest`。要求不可變 Release 的存放庫應將工作流程改為使用唯一的組建標籤。

滾動版本中的安裝程式與可攜版執行檔目前皆未經數位簽章，因此 Windows SmartScreen 可能顯示信譽警告。執行前請檢查原始碼，並驗證對應的 SHA-256 總和檢查碼。

## 參與貢獻

歡迎貢獻。請閱讀 [CONTRIBUTING.md](CONTRIBUTING.md)。測試必須使用暫時的合成認證與設定檔，絕不能存取開發者真實的 Codex、Claude Code 或 OpenCode 檔案。

## 授權條款

[MIT](LICENSE)

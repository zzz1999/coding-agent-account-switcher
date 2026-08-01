# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Coding Agent Account Switcher 圖示">
</p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

一個簡單的 Windows 工具，用來在已儲存的 Codex、Claude Code 與 OpenCode
帳號或 API 站點設定之間快速切換。

它只切換設定檔內儲存的帳號資訊與支援的 API 設定。MCP 伺服器、技能、外掛程式、
專案設定與歷史記錄都保留在原處。

> [!IMPORTANT]
> 這是非官方社群專案，與 OpenAI、Anthropic、Apple 或 OpenCode 專案沒有隸屬
> 或贊助關係。它不能轉移訂閱、繞過登入要求或覆寫組織政策。

## 下載

請從 [latest Release](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest)
下載目前版本：

- **安裝版：** <code>coding-agent-account-switcher-setup-win-x64.exe</code>
- **可攜版：** <code>coding-agent-account-switcher-portable-win-x64.exe</code>
- **校驗檔：** 每個執行檔都附有對應的 SHA-256 檔案

安裝版只為目前使用者安裝，不需要系統管理員權限，也不會自動開啟「隨 Windows
啟動」。目前版本尚未簽署，因此 Windows SmartScreen 可能顯示警告。

## 主要功能

- 儲存個人、工作與 API 站點設定檔，並以清楚的名稱區分。
- 幾次點擊即可切換 Codex、Claude Code 與 OpenCode 帳號。
- 將支援的 API 位址、金鑰、供應商與模型選擇保存在對應設定檔中。
- 按兩下設定檔名稱即可重新命名，也可以刪除本機儲存的快照。
- 切換成功後清楚顯示目前設定檔名稱。
- 相關軟體仍在執行時自動阻止切換。
- 內建 12 種介面語言。
- 記住明暗主題與語言選擇。
- 可選擇隨 Windows 啟動，並可在設定中手動檢查更新。

所有設定檔都保存在本機，軟體不包含分析或遙測。

## 支援的軟體

| 軟體 | 會切換的內容 | 保持不變的內容 |
| --- | --- | --- |
| Codex | 登入資訊及支援的 API 供應商/模型設定 | MCP、技能、工作階段、歷史與其他設定 |
| Claude Code | 登入資訊及支援的 API 位址/金鑰設定 | 外掛程式、MCP、專案、歷史與其他設定 |
| OpenCode | 已儲存的登入資訊及支援的供應商/模型設定 | 專案設定與其他設定 |

軟體只切換本專案支援的帳號相關欄位。精確的檔案與欄位清單請參閱
[架構文件](docs/ARCHITECTURE.md)。

## 快速開始

1. 正常登入帳號，或設定要使用的 API 站點。
2. 完全關閉 Codex、Claude Code、OpenCode 與相關擴充功能。
3. 開啟本軟體並選擇對應的應用程式。
4. 按下 **儲存目前帳號**，將其命名為 <code>Personal</code> 等名稱。
5. 登入第二個帳號或設定另一個 API 站點。
6. 再次關閉相關軟體，並將其儲存為 <code>Work</code> 等名稱。
7. 之後選擇已儲存的設定檔即可切換。

切換前軟體會檢查相關程序。若仍有軟體在執行，會先提示關閉，且不會修改檔案。

如果目前帳號自上次儲存後發生變化，軟體會先要求確認。如果實際上已登入另一個
帳號，請先將其儲存為新設定檔。

## 設定檔與設定

- **重新命名：** 按兩下設定檔名稱。
- **刪除：** 按下垃圾桶按鈕。只刪除本軟體的本機加密快照，不會刪除帳號或登出。
- **上次選取：** 表示最近一次由本軟體啟用的設定檔。
- **損壞快照：** 遺失或無法讀取的快照不會顯示為可切換帳號，其他正常設定檔仍可使用。
- **主題與語言：** 都會為目前 Windows 使用者記住。
- **隨 Windows 啟動：** 可選，僅對目前使用者生效，不需要系統管理員權限。
- **檢查更新：** 只有主動按下時才執行，不會自動下載或安裝更新。

## 隱私與安全

- 儲存的設定檔使用目前 Windows 使用者的 DPAPI 加密。
- 認證與 API 金鑰不會顯示在介面中，也不會寫入應用程式記錄。
- 設定檔保存在 <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code>。
- 切換時使用受保護的檔案取代與復原機制，減少中途失敗造成的問題。
- 軟體不會上傳認證、設定檔名稱、裝置識別碼或遙測資料。
- 工作帳號可能受組織政策約束，儲存額外的本機登入快照前請先取得許可。

回報安全問題前請閱讀 [SECURITY.md](SECURITY.md)。

## 使用限制

- 服務端登出、權杖到期、SSO、MFA 或組織政策仍可能要求重新正常登入。
- 軟體不能在帳號之間轉移訂閱，也不能保證帳號永久保持登入。
- 目前僅支援 Windows 10 與 Windows 11 x64。

## 從原始碼建置

需要 Windows 與 .NET SDK 8.0.400 或更新的 .NET 8 feature band。

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

每次推送到 <code>main</code> 後，GitHub Actions 都會重新建置滾動更新的
<code>latest</code> 安裝版與可攜版。

## 參與貢獻

歡迎貢獻，請閱讀 [CONTRIBUTING.md](CONTRIBUTING.md)。請勿在 Issue、記錄、
測試或提交中包含真實認證或 API 金鑰。

## 授權條款

[MIT](LICENSE)

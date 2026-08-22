# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Coding Agent Account Switcher アイコン">
</p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

保存済みの Codex、Claude Code、OpenCode アカウントや API サイト設定を
簡単に切り替えるための Windows アプリです。

プロファイルに保存されたアカウント情報と対応 API 設定だけを切り替えます。
MCP サーバー、スキル、プラグイン、プロジェクト設定、履歴はそのまま残ります。

> [!IMPORTANT]
> これは非公式のコミュニティプロジェクトです。OpenAI、Anthropic、Apple、
> OpenCode プロジェクトとは提携していません。サブスクリプションの移転、
> サインイン要件の回避、組織ポリシーの上書きはできません。

## ダウンロード

現在のバージョンは
[最新リリース](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest)
から入手できます。

- **インストーラー:** <code>coding-agent-account-switcher-setup-win-x64.exe</code>
- **ポータブル版:** <code>coding-agent-account-switcher-portable-win-x64.exe</code>
- **チェックサム:** 各実行ファイルに対応する SHA-256 ファイルがあります

インストールは現在のユーザーだけが対象で、管理者権限は不要です。
許可なく **Windows と同時に起動** を有効にすることもありません。現在の
ビルドは未署名のため、Windows SmartScreen が警告を表示する場合があります。

## 主な機能

- 個人用、仕事用、API サイト用のプロファイルを分かりやすい名前で保存。
- Codex、Claude Code、OpenCode のアカウントを数回のクリックで切り替え。
- 対応する API アドレス、キー、プロバイダー経路設定を正しいプロファイルに保存。
- プロファイル名をダブルクリックして変更し、ローカルスナップショットを削除。
- 切り替え後に有効なプロファイル名を明確に確認。
- 関連アプリが開いている間は切り替えを停止。
- 12 種類の内蔵言語を利用可能。
- ライト／ダークテーマと言語を次回起動時も保持。
- 同じ Windows ログインセッションで再度起動すると、新しいウィンドウを増やさず、
  既存のウィンドウを復元して前面に表示。
- Windows 自動起動を選択し、更新を手動で確認。

すべてのデータは PC 内に残ります。分析やテレメトリはありません。

## 対応アプリ

| アプリ | 切り替える内容 | 変更しない内容 |
| --- | --- | --- |
| Codex | ログインと選択した API プロバイダーの接続設定 | モデル、レビュー／推論オプション、機能、MCP、スキル、セッション、履歴、その他の設定 |
| Claude Code | ログインと対応する API アドレス／キー設定 | プラグイン、MCP、プロジェクト、履歴、その他の設定 |
| OpenCode | 保存済みログインと対応するプロバイダー／モデル設定 | プロジェクト構成とその他の設定 |

本プロジェクトが対応するアカウント関連フィールドだけを変更します。正確な一覧は
[アーキテクチャ](docs/ARCHITECTURE.md)を参照してください。

## クイックスタート

1. 通常どおりサインインするか、使用する API サイトを設定します。
2. このアプリを開き、対応するプロバイダーを選びます。
3. **現在のアカウントを保存** を選び、<code>Personal</code> などの名前を付けます。
4. 2 つ目のアカウントにサインインするか、別の API サイトを設定します。
5. <code>Work</code> などの名前で保存します。
6. 切り替えるときは関連アプリを完全に閉じてから、保存済みプロファイルを選択します。

アカウントの保存時は関連アプリを閉じる必要はありません。切り替え前には実行中の
プロセスを確認します。何かが開いていれば、閉じるよう案内し、ファイルは変更しません。

保存後に現在のアカウントが変わっている場合は確認を求めます。実際に別の
アカウントへ変更した場合は、先に新しいプロファイルとして保存してください。

## プロファイルと設定

- **名前変更:** プロファイル名をダブルクリックします。
- **削除:** ごみ箱ボタンを使います。ローカルの暗号化スナップショットだけを
  削除し、アカウントの削除やサインアウトは行いません。
- **最後に選択:** このアプリが直近に有効化したプロファイルを示します。
- **破損したスナップショット:** 欠落または読めないものは一覧から隠し、正常な
  プロファイルは引き続き使用できます。
- **テーマと言語:** 現在の Windows ユーザー用に記憶します。
- **Windows と同時に起動:** 任意で、現在のユーザーだけが対象です。
- **更新を確認:** クリックした場合だけ実行し、自動ダウンロードやインストールはしません。

## プライバシーとセキュリティ

- プロファイルは現在の Windows ユーザーの DPAPI で暗号化します。
- 資格情報と API キーは表示せず、アプリのログにも書きません。
- プロファイルは <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code> に保存します。
- 保護されたファイル置換と回復により、不完全な変更のリスクを減らします。
- 資格情報、プロファイル名、端末識別子、テレメトリは送信しません。
- 仕事用アカウントには組織ポリシーが適用される場合があります。追加のローカル
  ログインコピーを保存する前に許可を得てください。

セキュリティ問題を報告する前に [SECURITY.md](SECURITY.md) をお読みください。

## 制限

- プロバイダー側のログアウト、トークン期限切れ、SSO、MFA、組織ポリシーにより
  通常のサインインが必要になる場合があります。
- 既存の Codex 会話は、作成時に使用したプロバイダーに関連付けられています。
  プロバイダーを切り替えた後は新しい会話を開始するか、元の会話を続けるために
  元のプロバイダーへ戻してください。
- サブスクリプションの移転や、永続的なログインの保証はできません。
- 現在は Windows 10／Windows 11 x64 のみ対応しています。

## ソースからビルド

Windows と .NET SDK 8.0.400 以降の .NET 8 feature band が必要です。

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

<code>main</code> への各プッシュで、GitHub Actions が新しいバージョン付き
Release を公開します。インストーラーとポータブル版は最新 Release のみに残ります。

## コントリビューション

貢献を歓迎します。[CONTRIBUTING.md](CONTRIBUTING.md)をお読みください。
実際の資格情報や API キーを Issue、ログ、テスト、コミットに含めないでください。

## ライセンス

[MIT](LICENSE)

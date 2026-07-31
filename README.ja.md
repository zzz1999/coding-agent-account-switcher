# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Codex と Claude Code に対応した、非公式かつローカルファーストの Windows 用アカウント切り替えツールです。

Coding Agent Account Switcher は、Codex と Claude Code が使用するローカル認証ファイルの、名前付きで暗号化されたスナップショットを保存します。切り替えるのは認証スナップショットのみです。通常の設定、MCP 構成、スキル、プラグイン、プロジェクト履歴は元の場所に残ります。

> [!IMPORTANT]
> 本プロジェクトは OpenAI または Anthropic と提携しておらず、承認や後援も受けていません。サブスクリプションの移転、サインイン要件の回避、アカウント共有、プロバイダーや組織のポリシーの迂回を行うものではありません。

「iOS 18 に着想を得た」という表現は、一般的なビジュアルの方向性だけを示します。Apple は本プロジェクトと無関係であり、Apple のフォント、シンボル、アートワーク、商標は同梱していません。

## ダウンロード

現在のビルドは [latest リリース](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest)からダウンロードできます。

- **推奨インストーラー:** `coding-agent-account-switcher-setup-win-x64.exe` は管理者権限なしで現在の Windows ユーザー向けにインストールし、スタートメニューとアンインストールの項目を作成します。
- **ポータブル版:** `coding-agent-account-switcher-win-x64.zip` は展開するだけで、インストールせずに実行できます。
- リリースには、各パッケージに対応する SHA-256 チェックサムファイルが含まれます。

インストーラーは **Windows と同時に起動**を自動的に有効化せず、Codex または Claude Code の認証ファイルや設定ファイルには触れません。アンインストールしても暗号化されたアカウントスナップショットとアプリ設定は保持され、再インストール後も使用できます。インストーラーとポータブル版の実行ファイルは現在未署名のため、Windows SmartScreen が評価警告を表示する場合があります。

## 機能

- iOS 18 に着想を得たガラスカードデザインの Windows ネイティブ WPF インターフェイス。
- 英語、簡体字中国語、繁体字中国語、スペイン語、フランス語、ドイツ語、日本語、韓国語、ブラジルポルトガル語、ロシア語、アラビア語、ヒンディー語を内蔵。
- 表示言語と、現在のユーザー向けの Windows 自動起動を設定画面で選択可能。
- Codex と Claude Code の個人用・仕事用プロファイルを任意の名前で保存。
- 関連アプリケーションを閉じるまで切り替えを禁止するプロセスガード。
- 資格情報を不透明なバイト列として処理し、トークンの解析、メールアドレスの抽出、資格情報のログ記録は行いません。
- 現在の Windows ユーザーに対する Windows DPAPI でプロファイルスナップショットを暗号化。
- 同一ディレクトリ内での資格情報のアトミック置換とロールバック対応。
- 変更されたライブログインで「最後に選択した」プロファイルのスナップショットを上書きする前に、明示的な確認を要求。
- 「最後に選択した」プロファイルを安全に戻す **スナップショットを復元** 操作。確認は、置き換え対象となるライブ認証の正確なバイト列に紐づきます。
- 分析やテレメトリのない、完全なローカル動作。
- `main` から Windows 用のローリング `latest` ビルドを自動生成。

## 対応する認証ファイル

| プロバイダー | 切り替える既定の認証ファイル | 変更しない構成 |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`、スキル、MCP、セッション、その他の状態 |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`、`.claude.json`、プラグイン、MCP、プロジェクト設定、セッション履歴 |

`CODEX_HOME` または `CLAUDE_CONFIG_DIR` が設定されている場合、アプリはプロバイダー固有の認証ルートに従います。その場合も切り替えるのは `auth.json` または `.credentials.json` だけで、隣接する構成ファイルは変更しません。

Codex ではファイルベースの資格情報ストレージを使用する必要があります。OS の資格情報ストアを使用している場合は、アクティブな Codex ルート（既定は `%USERPROFILE%\.codex`、設定されている場合は `CODEX_HOME`）内の `config.toml` に次の設定を追加してください。

```toml
cli_auth_credentials_store = "file"
```

現在の保存仕様については、公式の [Codex 認証ドキュメント](https://developers.openai.com/codex/auth)と [Claude Code 認証ドキュメント](https://code.claude.com/docs/en/authentication)を参照してください。

## 使い方

1. プロバイダーの公式ログインフローで最初のアカウントにサインインします。
2. Codex/Claude Code と、関連するローカルクライアントや拡張機能を完全に閉じます。
3. 現在のログインを `Personal` など任意の名前で保存します。
4. 2 つ目のアカウントにサインインし、`Work` など別の名前で保存します。
5. 保存済みプロファイルを選びます。アプリは変更前に関連プロセスを確認します。いずれかが実行中なら切り替えを禁止し、資格情報ファイルは変更しません。
6. 別のプロファイルに切り替える際、バイト列に紐づく確認を行った後、更新されたトークンを保持するため、現在の認証ファイルを最後に選択した暗号化プロファイルへ保存します。

アプリはこのプロファイルを「検証済みの現在」ではなく、**最後に選択** と表示します。ライブファイルが保存済みスナップショットと一致しない場合、書き込み前に切り替えを停止します。同じアカウントの更新である場合だけ確認してください。アプリ外で別のアカウントにサインインした場合は、先に **新規保存** を選ぶか、正しい名前の既存プロファイルを明示的に置き換えてください。

最後に選択したカードの **スナップショットを復元** ボタンは、ライブファイルが保存済みスナップショットと一致するか確認します。異なる場合は、保存されていない現在のログインを置き換える警告を表示し、その正確なバイト列に承認を紐づけます。別のライブログインが過去の確認を再利用することはできません。復元中は、トランザクション専用の DPAPI 暗号化された回復資格情報が、コミットまたはロールバックまで復元前のバイト列を保持します。

アプリはログインが永続することを保証しません。プロバイダー側での失効、組織ポリシー、SSO、MFA、トークンの期限切れにより、公式クライアントでの通常のサインインが必要になる場合があります。

## 設定

アプリウィンドウから **設定** を開き、表示言語や Windows と同時に起動するかどうかを選択できます。選択した言語は現在の Windows ユーザー用にローカル保存され、いつでも変更できます。

**Windows と同時に起動** は `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` に本アプリのエントリを追加します。現在の Windows ユーザーだけに適用され、管理者権限は不要です。オフにすると Coding Agent Account Switcher が所有する起動エントリだけを削除し、他のスタートアップアプリは変更しません。

## セキュリティモデル

- 暗号化プロファイルは `%LOCALAPPDATA%\CodingAgentAccountSwitcher` 以下に保存されます。
- 正規化された認証ファイルの場所ごとに、ハッシュで分離された独自の保管庫、アクティブ状態、回復ジャーナル、操作ミューテックスがあります。そのため `CODEX_HOME` や `CLAUDE_CONFIG_DIR` を変更すると、別の場所のアクティブアカウントを再利用せず、独立したプロファイルセットが開始されます。
- DPAPI `CurrentUser` により別の Windows アカウントから直接復号することは防げますが、同じ Windows ユーザーとしてすでに動作している悪意あるソフトウェアからは保護できません。
- プロバイダーの通常のライブ認証ファイルを除き、復号されたスナップショットのバイト列は、取得または切り替え時にメモリと同一ディレクトリ内のアトミック置換処理に短時間だけ存在します。
- 認証置換用の一時ファイルとバックアップファイルには回復トランザクション ID を使用します。正常完了時は両方の正確なファイルを削除します。中断後は、暗号化されたソーススナップショットまたはトランザクション専用の回復資格情報から欠落したライブファイルを復元し、そのトランザクションが所有するすべての正確なステージングファイルを削除してからジャーナルを削除します。
- アプリは資格情報をアップロードしません。ログ、Issue、クラッシュレポート、テストフィクスチャ、リポジトリのコミットに資格情報を含めてはいけません。
- プロセス検出は防御的なベストエフォートです。新たに開始したプロセスが切り替えと競合する可能性があるため、操作完了まで Codex や Claude Code を起動しないでください。
- 仕事用アカウントが組織管理の場合、追加の暗号化されたローカル認証スナップショットを保持する前に承認を得てください。

資格情報処理コードを変更する前に、[SECURITY.md](SECURITY.md) と [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) をお読みください。

## ソースからビルド

要件：

- Windows 10 または Windows 11
- .NET SDK 8.0.400、またはより新しい .NET 8 feature band

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

自己完結型 Windows x64 ビルドの作成：

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## ローリング latest リリース

`main` に push するたびに `.github/workflows/latest-release.yml` が実行されます。

1. ソリューションを復元してテストします。
2. 自己完結型のポータブル Windows x64 ビルドを発行します。
3. ユーザー単位の Windows x64 インストーラーをビルドします。
4. ポータブル ZIP と、両パッケージの SHA-256 チェックサムを作成します。
5. 以前の `latest` という名前の Release とタグだけを削除します。
6. 現在のコミット用に、インストーラー、ポータブル版、チェックサムを含む新しい `latest` Release を公開します。

このワークフローはバージョン付き Release を削除しません。ローリング `latest` タグでは GitHub の **immutable releases** オプションを無効にし、ブランチまたはタグルールでワークフローによる `latest` の削除を許可する必要があります。不変 Release が必要なリポジトリでは、ワークフローを固有のビルドタグに変更してください。

ローリング版のインストーラーとポータブル実行ファイルは現在未署名のため、Windows SmartScreen が評価警告を表示する場合があります。実行前にソースを確認し、対応する公開済み SHA-256 チェックサムを検証してください。

## コントリビューション

コントリビューションを歓迎します。[CONTRIBUTING.md](CONTRIBUTING.md) をお読みください。テストでは一時的な偽の資格情報ファイルを使い、開発者の実際の Codex または Claude Code 認証ファイルへ絶対にアクセスしないでください。

## ライセンス

[MIT](LICENSE)

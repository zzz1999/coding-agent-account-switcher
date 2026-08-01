# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Codex、Claude Code、OpenCode に対応した、非公式かつローカルファーストの Windows 用アカウント／API サイト切り替えツールです。

Coding Agent Account Switcher は、ローカル資格情報と必要な API プロバイダーフィールドだけを暗号化スナップショットとして保存します。その他の設定、MCP、スキル、プラグイン、プロジェクト履歴は元の場所に残ります。

> [!IMPORTANT]
> 本プロジェクトは OpenAI または Anthropic と提携しておらず、承認や後援も受けていません。サブスクリプションの移転、サインイン要件の回避、アカウント共有、プロバイダーや組織のポリシーの迂回を行うものではありません。

「iOS 18 に着想を得た」という表現は、一般的なビジュアルの方向性だけを示します。Apple は本プロジェクトと無関係であり、Apple のフォント、シンボル、アートワーク、商標は同梱していません。

## ダウンロード

現在のビルドは [latest リリース](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest)からダウンロードできます。

- **推奨インストーラー:** `coding-agent-account-switcher-setup-win-x64.exe` は管理者権限なしで現在の Windows ユーザー向けにインストールし、スタートメニューとアンインストールの項目を作成します。
- **ポータブル実行ファイル:** `coding-agent-account-switcher-portable-win-x64.exe` を直接ダウンロードして実行できます。インストールは不要です。
- リリースには、各実行ファイルに対応する SHA-256 チェックサムファイルが含まれます。

インストーラーは **Windows と同時に起動**を自動的に有効化せず、Codex、Claude Code、OpenCode の認証ファイルや設定ファイルには触れません。アンインストールしても暗号化されたアカウントスナップショットとアプリ設定は保持され、再インストール後も使用できます。インストーラーとポータブル版の実行ファイルは現在未署名のため、Windows SmartScreen が評価警告を表示する場合があります。

## 機能

- iOS 18 に着想を得たガラスカードデザインの Windows ネイティブ WPF インターフェイス。
- 英語、簡体字中国語、繁体字中国語、スペイン語、フランス語、ドイツ語、日本語、韓国語、ブラジルポルトガル語、ロシア語、アラビア語、ヒンディー語を内蔵。
- 表示言語と、現在のユーザー向けの Windows 自動起動を設定画面で選択可能。
- Codex、Claude Code、OpenCode の個人用・仕事用・API サイト用プロファイルを任意の名前で保存。
- 関連アプリケーションを閉じるまで切り替えを禁止するプロセスガード。
- 認証ファイルは不透明なバイト列のまま扱います。下記の管理対象フィールドだけを解析・マージし、秘密を表示またはログ記録しません。
- 現在の Windows ユーザーに対する Windows DPAPI でプロファイルスナップショットを暗号化。
- 同一ディレクトリ内での管理対象ファイルのアトミック置換とロールバック対応。
- 変更されたライブログインで「最後に選択した」プロファイルのスナップショットを上書きする前に、明示的な確認を要求。
- 「最後に選択した」プロファイルを安全に戻す **スナップショットを復元** 操作。確認は、置き換え対象となるライブ認証の正確なバイト列に紐づきます。
- 分析やテレメトリのない、完全なローカル動作。
- `main` から Windows 用のローリング `latest` ビルドを自動生成。

## 対応するアカウントと API 構成

| プロバイダー | 管理対象ファイル | 選択的に管理する構成 |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` と `config.toml` | `model_provider`、`openai_base_url`、`model`、`review_model`、`model_reasoning_effort`、`disable_response_storage`、選択中の有効な `model_providers` テーブル、`features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` と `settings.json` | `env.ANTHROPIC_BASE_URL`、`env.ANTHROPIC_API_KEY`、`env.ANTHROPIC_AUTH_TOKEN`、`env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`、互換フィールド `env.CLAUDE_CODE_ATTRIBUTION_HEADER` のみ |
| OpenCode | 任意の `%USERPROFILE%\.local\share\opencode\auth.json`、グローバル `config.json`、`opencode.json`、`opencode.jsonc`、設定時は最後に `OPENCODE_CONFIG` | 不透明な `/connect` 資格情報と、実効的な `provider`、`model`、`small_model` API プロファイル |

アプリはファイル全体を置き換えず、これらのフィールドだけを現在の構成へマージします。Codex の `network_access`、`windows_wsl_setup_acknowledged`、`features.goals`、`cli_auth_credentials_store`、MCP、スキル、セッション、その他すべての値は保持されます。Claude Code のその他の `env`、`.claude.json`、プラグイン、MCP、プロジェクト設定、履歴も保持されます。OpenCode では、参加する各グローバル／カスタムファイルの無関係な意味上の値を保持します。

OpenCode の既定の構成ルートは `%USERPROFILE%\.config`、既定のデータルートは `%USERPROFILE%\.local\share` です。`XDG_CONFIG_HOME` または `XDG_DATA_HOME` が絶対パスの場合だけ、対応する既定ルートを置き換えます。空の値では置き換えません。OpenCode の空でないパスオーバーライド（`XDG_CONFIG_HOME`、`XDG_DATA_HOME`、`OPENCODE_CONFIG`、`OPENCODE_CONFIG_DIR`）はすべて絶対パスでなければならず、相対値がある場合はファイルを書き込む前に取得と切り替えをブロックします。グローバル構成は構成ルート配下の `opencode\config.json`、`opencode.json`、`opencode.jsonc` の順に読み込まれ、後の層が優先されます。`OPENCODE_CONFIG` は最後です。アプリは実効的な `provider`、`model`、`small_model` を 1 つの API プロファイルとして取得します。`/connect` 認証はデータルート配下の `opencode\auth.json` から読み込みます。

OpenCode は `auth.json` 内の `/connect` 資格情報と、`provider` オブジェクト内の API キーの両方に対応します。スナップショットには存在する方式、または両方が含まれます。

適用時は古い下位エンドポイントが優先されないよう、他のグローバル層から `provider`、`model`、`small_model` を削除します。`OPENCODE_CONFIG` があれば対象値をそこへ書き、3 つのグローバル層を消去します。なければ空でない対象値をグローバル `opencode.jsonc` に正規化し、以前 JSON／旧ファイルしかなくても `config.json` と `opencode.json` から削除します。認証のみ、または管理対象が空のスナップショットも有効で、既存値は消去しますが空の `opencode.jsonc` は新規作成しません。

選択的マージでは無関係な意味上の値を保持しますが、JSON／TOML の再シリアル化時に書式やコメントをバイト単位で保持する保証はありません。

OpenCode の取得または切り替え前に、既知の高優先度環境オーバーライドを読み取り専用で検査します。空白以外の `OPENCODE_AUTH_CONTENT` があると操作をブロックします。`OPENCODE_CONFIG_CONTENT` は、有効な JSON/JSONC であり、最上位の管理対象キー `provider`、`model`、`small_model` を含まない場合だけ許可されます。無効な内容またはいずれかの管理対象キーがあると操作をブロックします。`OPENCODE_CONFIG_DIR` が設定されている場合は、その `opencode.json` と `opencode.jsonc` を検査します。読み取れないファイル、無効なファイル、またはいずれかの管理対象キーを含むファイルがあると操作をブロックします。無関係なキーだけを含むインライン構成やディレクトリ構成は許可されます。アプリはこれらの環境由来ソースを変更しません。

`CODEX_HOME` と `CLAUDE_CONFIG_DIR` はそれぞれのルートを変更します。OpenCode のプロジェクト構成、集中管理ソース、プロバイダー固有の環境変数は管理対象外のままであり、切り替え後も選択したグローバルプロファイルを上書きできます。アプリはそれらを検索・変更しません。プロセスガードは `opencode` と `opencode-cli` を確認します。互換フィールドの記載は現行の全バージョンで文書化されているという意味ではありません。

Codex ではファイルベースの資格情報ストレージを使用する必要があります。OS の資格情報ストアを使用している場合は、アクティブな Codex ルート（既定は `%USERPROFILE%\.codex`、設定されている場合は `CODEX_HOME`）内の `config.toml` に次の設定を追加してください。

```toml
cli_auth_credentials_store = "file"
```

現在の保存仕様については、公式の [Codex 認証ドキュメント](https://developers.openai.com/codex/auth)と [Claude Code 認証ドキュメント](https://code.claude.com/docs/en/authentication)を参照してください。

## 使い方

1. 公式フローでサインインするか、通常のプロバイダーファイルに対応 API サイトを設定します。
2. Codex、Claude Code、OpenCode と関連するクライアントや拡張機能を完全に閉じます。
3. アカウントと管理対象 API 設定を `Personal` などの名前で保存します。
4. 別のアカウントまたは API サイトを設定し、`Work` などの名前で保存します。
5. プロファイルを選びます。関連プロセスが実行中なら切り替えを禁止し、管理対象ファイルは変更しません。
6. 内容に紐づく確認後、更新トークンと意図的な API 変更を保持するため、現在の管理対象スナップショットを最後に選択した暗号化プロファイルへ保存します。切り替え前の正確なスナップショットもトランザクション専用の DPAPI 回復ブロブに保存します。

アプリはこのプロファイルを「検証済みの現在」ではなく、**最後に選択** と表示します。ライブファイルが保存済みスナップショットと一致しない場合、書き込み前に切り替えを停止します。同じアカウントの更新である場合だけ確認してください。アプリ外で別のアカウントにサインインした場合は、先に **新規保存** を選ぶか、正しい名前の既存プロファイルを明示的に置き換えてください。

最後に選択したカードの **スナップショットを復元** ボタンは、ライブファイルが保存済みスナップショットと一致するか確認します。異なる場合は、保存されていない現在のログインを置き換える警告を表示し、その正確なバイト列に承認を紐づけます。別のライブログインが過去の確認を再利用することはできません。復元中は、トランザクション専用の DPAPI 暗号化された回復資格情報が、コミットまたはロールバックまで復元前のバイト列を保持します。

複合または複数ファイルの各トランザクションは、保存済みの切り替え元と一致する場合でも、ジャーナルより先に現在の管理対象スナップショットをそのまま暗号化回復ブロブへ保存します。これにより部分書き込みを正確に戻し、資格情報だけの旧プロファイルも安全に更新できます。中断後はこの切り替え前スナップショットを優先し、復元した場合は切り替え元プロファイルも同期します。ブロブのない旧ジャーナルは保存済みの切り替え元へフォールバックします。ライブ管理状態が切り替え元にも切り替え先にも一致しない場合、ジャーナルとブロブを手動回復用に保持します。

複数ファイルのコミットはフェイルクローズです。独立した認証を先に削除し、構成をアトミックにマージしてから、切り替え先の認証を最後に配置します。中断時に認証が一時的に存在しないことはありますが、資格情報が反対側の API エンドポイントと組み合わさることはなく、暗号化された切り替え前スナップショットから安全に完了またはロールバックします。

旧版の生の Codex／Claude Code プロファイルは、資格情報と空の管理対象 API 構成として解釈されます。これを有効にすると、前のプロファイルの第三者エンドポイントを再利用しないよう、管理対象の API ルートとモデルフィールドを消去します。その後、必要なモデル/API を設定してプロファイルを再取得してください。旧形式のアクティブな切り替え元は、そこから切り替える際に複合形式へ更新されます。

アプリはログインが永続することを保証しません。プロバイダー側での失効、組織ポリシー、SSO、MFA、トークンの期限切れにより、公式クライアントでの通常のサインインが必要になる場合があります。

## 設定

アプリウィンドウから **設定** を開き、表示言語や Windows と同時に起動するかどうかを選択できます。選択した言語は現在の Windows ユーザー用にローカル保存され、いつでも変更できます。

**Windows と同時に起動** は `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` に本アプリのエントリを追加します。現在の Windows ユーザーだけに適用され、管理者権限は不要です。オフにすると Coding Agent Account Switcher が所有する起動エントリだけを削除し、他のスタートアップアプリは変更しません。

## セキュリティモデル

- 暗号化プロファイルは `%LOCALAPPDATA%\CodingAgentAccountSwitcher` 以下に保存されます。
- 正規化された管理対象ファイル一式ごとに、独自の保管庫、状態、回復ジャーナル、ミューテックスがあります。`CODEX_HOME`、`CLAUDE_CONFIG_DIR`、`OPENCODE_CONFIG` を変更すると別のプロファイルセットになります。
- DPAPI `CurrentUser` により別の Windows アカウントから直接復号することは防げますが、同じ Windows ユーザーとしてすでに動作している悪意あるソフトウェアからは保護できません。
- プロバイダーの通常のライブ認証ファイルを除き、復号されたスナップショットのバイト列は、取得または切り替え時にメモリと同一ディレクトリ内のアトミック置換処理に短時間だけ存在します。
- 認証置換用の一時ファイルとバックアップファイルには回復トランザクション ID を使用します。正常完了時は両方の正確なファイルを削除します。中断後は、暗号化されたソーススナップショットまたはトランザクション専用の回復資格情報から欠落したライブファイルを復元し、そのトランザクションが所有するすべての正確なステージングファイルを削除してからジャーナルを削除します。暗号化回復ブロブは、ジャーナルの削除後にのみベストエフォートで削除されます。
- アプリは資格情報をアップロードしません。ログ、Issue、クラッシュレポート、テストフィクスチャ、リポジトリのコミットに資格情報を含めてはいけません。
- プロセス検出は防御的なベストエフォートです。操作完了まで Codex、Claude Code、OpenCode を起動しないでください。
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
4. ポータブル実行ファイルと、両実行ファイルの SHA-256 チェックサムを準備します。
5. 以前の `latest` という名前の Release とタグだけを削除します。
6. 現在のコミット用に、インストーラー、ポータブル実行ファイル、チェックサムを含む新しい `latest` Release を公開します。

このワークフローはバージョン付き Release を削除しません。ローリング `latest` タグでは GitHub の **immutable releases** オプションを無効にし、ブランチまたはタグルールでワークフローによる `latest` の削除を許可する必要があります。不変 Release が必要なリポジトリでは、ワークフローを固有のビルドタグに変更してください。

ローリング版のインストーラーとポータブル実行ファイルは現在未署名のため、Windows SmartScreen が評価警告を表示する場合があります。実行前にソースを確認し、対応する公開済み SHA-256 チェックサムを検証してください。

## コントリビューション

コントリビューションを歓迎します。[CONTRIBUTING.md](CONTRIBUTING.md) をお読みください。テストでは一時的な合成資格情報と構成を使い、実際の Codex、Claude Code、OpenCode ファイルへアクセスしないでください。

## ライセンス

[MIT](LICENSE)

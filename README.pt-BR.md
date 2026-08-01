# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Um alternador local e não oficial de contas e sites de API para Codex, Claude Code e OpenCode no Windows.

O Coding Agent Account Switcher armazena instantâneos criptografados das credenciais locais junto somente dos campos necessários do provedor de API. As demais configurações, MCP, habilidades, plugins e histórico permanecem nos locais originais.

> [!IMPORTANT]
> Este projeto não é afiliado, endossado nem patrocinado pela OpenAI ou pela Anthropic. Ele não transfere assinaturas, contorna requisitos de login, compartilha contas nem burla políticas de provedores ou organizações.

A descrição “inspirado no iOS 18” se refere apenas à direção visual geral. A Apple não é afiliada a este projeto, e nenhuma fonte, símbolo, arte ou marca da Apple é incluída.

## Download

Baixe a build atual na [release latest](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Instalador recomendado:** `coding-agent-account-switcher-setup-win-x64.exe` instala para o usuário atual do Windows sem privilégios de administrador e cria entradas no menu Iniciar e de desinstalação.
- **Executável portátil:** baixe `coding-agent-account-switcher-portable-win-x64.exe` e execute-o diretamente, sem necessidade de instalação.
- A release inclui um arquivo de soma SHA-256 correspondente para cada executável.

O instalador não ativa automaticamente **Iniciar com o Windows** e não altera arquivos de autenticação ou configuração do Codex, Claude Code ou OpenCode. A desinstalação preserva os snapshots de contas criptografados e as configurações do aplicativo para que continuem disponíveis após uma reinstalação. O instalador e o executável portátil não são assinados no momento, portanto o Windows SmartScreen pode exibir um alerta de reputação.

## Recursos

- Interface WPF nativa do Windows com design de cartões de vidro inspirado no iOS 18.
- Idiomas integrados: inglês, chinês simplificado, chinês tradicional, espanhol, francês, alemão, japonês, coreano, português do Brasil, russo, árabe e hindi.
- Configurações no aplicativo para o idioma de exibição e a inicialização opcional com o Windows para o usuário atual.
- Perfis pessoais, profissionais e de sites de API para Codex, Claude Code e OpenCode.
- Proteção de processos que bloqueia a alternância até que os aplicativos relacionados sejam fechados.
- Os arquivos de autenticação permanecem bytes opacos. Somente os campos gerenciados listados abaixo são analisados e mesclados; segredos nunca são exibidos nem registrados.
- Instantâneos de perfil criptografados com o Windows DPAPI para o usuário atual do Windows.
- Substituição atômica dos arquivos gerenciados no mesmo diretório, com suporte a reversão.
- Confirmação explícita antes que um login ativo alterado possa sobrescrever o instantâneo do último perfil selecionado.
- Ação segura **Restaurar instantâneo** para o último perfil selecionado, com confirmação vinculada aos bytes exatos da autenticação ativa que serão substituídos.
- Operação exclusivamente local, sem análise ou telemetria.
- Build contínua `latest` para Windows produzida automaticamente a partir de `main`.

## Configuração de conta e API compatível

| Provedor | Arquivos gerenciados | Configuração gerenciada seletivamente |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` e `config.toml` | `model_provider`, `openai_base_url`, `model`, `review_model`, `model_reasoning_effort`, `disable_response_storage`, a tabela ativa selecionada em `model_providers` e `features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` e `settings.json` | Somente `env.ANTHROPIC_BASE_URL`, `env.ANTHROPIC_API_KEY`, `env.ANTHROPIC_AUTH_TOKEN`, `env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC` e o campo de compatibilidade `env.CLAUDE_CODE_ATTRIBUTION_HEADER` |
| OpenCode | `%USERPROFILE%\.local\share\opencode\auth.json` opcional; camadas globais `config.json`, `opencode.json`, `opencode.jsonc`; depois `OPENCODE_CONFIG` quando definido | Credenciais opacas de `/connect` e o perfil API efetivo de `provider`, `model`, `small_model` |

O aplicativo mescla esses campos sem substituir o arquivo inteiro. No Codex, `network_access`, `windows_wsl_setup_acknowledged`, `features.goals`, `cli_auth_credentials_store`, MCP, habilidades, sessões e todos os demais valores são preservados. No Claude Code, todas as outras entradas `env`, `.claude.json`, plugins, MCP, configurações de projeto e histórico são preservados. No OpenCode, valores semânticos não relacionados são preservados em cada arquivo global ou personalizado participante.

A raiz de configuração padrão do OpenCode é `%USERPROFILE%\.config`, e a raiz de dados padrão é `%USERPROFILE%\.local\share`. Um valor absoluto de `XDG_CONFIG_HOME` ou `XDG_DATA_HOME` substitui a raiz padrão correspondente; um valor vazio não a substitui. Todas as substituições de caminho não vazias do OpenCode (`XDG_CONFIG_HOME`, `XDG_DATA_HOME`, `OPENCODE_CONFIG` e `OPENCODE_CONFIG_DIR`) devem ser absolutas; um valor relativo bloqueia a captura e a alternância antes de qualquer gravação. A configuração global carrega, nessa ordem, `opencode\config.json`, `opencode.json` e `opencode.jsonc` sob a raiz de configuração; camadas posteriores prevalecem. `OPENCODE_CONFIG` é carregado por último. O aplicativo captura o resultado efetivo de `provider`, `model` e `small_model` como um perfil API. As credenciais de `/connect` são lidas de `opencode\auth.json` sob a raiz de dados.

O OpenCode aceita credenciais de `/connect` em `auth.json` e chaves de API embutidas no objeto `provider`; o instantâneo guarda o estilo presente, ou ambos.

Ao aplicar, `provider`, `model` e `small_model` são removidos das outras camadas globais para impedir que um endpoint antigo prevaleça. Com `OPENCODE_CONFIG`, o destino é gravado ali e as três camadas globais são limpas. Sem ele, valores não vazios são normalizados em `opencode.jsonc` global e removidos de `config.json` e `opencode.json`, mesmo se antes só existisse JSON ou o arquivo legado. Um instantâneo apenas de autenticação ou com configuração gerenciada vazia é válido: limpa valores existentes sem criar um `opencode.jsonc` vazio.

A mesclagem seletiva preserva valores semânticos não relacionados, mas não garante preservar byte a byte a formatação ou os comentários quando JSON ou TOML é serializado novamente.

Antes de capturar ou alternar o OpenCode, o aplicativo verifica, somente para leitura, as substituições conhecidas de ambiente com prioridade mais alta. Um valor não vazio de `OPENCODE_AUTH_CONTENT` bloqueia a operação. `OPENCODE_CONFIG_CONTENT` só é permitido quando contém JSON ou JSONC válido e nenhuma das chaves gerenciadas de nível superior `provider`, `model` ou `small_model`; conteúdo inválido ou qualquer uma dessas chaves bloqueia a operação. Se `OPENCODE_CONFIG_DIR` estiver definido, seus arquivos `opencode.json` e `opencode.jsonc` são verificados; um arquivo ilegível ou inválido, ou uma chave gerenciada em qualquer um deles, bloqueia a operação. Configuração inline ou de diretório contendo apenas chaves não relacionadas é permitida. O aplicativo não modifica nenhuma dessas fontes fornecidas pelo ambiente.

`CODEX_HOME` e `CLAUDE_CONFIG_DIR` alteram as respectivas raízes. Configuração OpenCode de projeto, fontes gerenciadas centralmente e variáveis de ambiente específicas do provedor continuam não gerenciadas e podem sobrepor o perfil global selecionado após uma alternância; o aplicativo não as procura nem modifica. A proteção de processos verifica `opencode` e `opencode-cli`. `disable_response_storage`, `features.responses_websockets_v2` e `CLAUDE_CODE_ATTRIBUTION_HEADER` permanecem campos de compatibilidade, sem afirmar que todas as versões atuais os documentam.

O Codex deve usar armazenamento de credenciais baseado em arquivo. Se sua instalação usa o armazenamento de credenciais do sistema operacional, adicione esta configuração ao `config.toml` na raiz ativa do Codex (`%USERPROFILE%\.codex` por padrão, ou `CODEX_HOME` quando definido):

```toml
cli_auth_credentials_store = "file"
```

Consulte a documentação oficial de [autenticação do Codex](https://developers.openai.com/codex/auth) e [autenticação do Claude Code](https://code.claude.com/docs/en/authentication) para os contratos atuais de armazenamento.

## Como funciona

1. Entre pelo fluxo oficial ou configure um site de API compatível nos arquivos normais do provedor.
2. Feche completamente Codex, Claude Code, OpenCode e qualquer cliente ou extensão relacionada.
3. Salve a conta e as configurações de API gerenciadas com um nome como `Personal`.
4. Entre em outra conta ou configure outro site de API e salve-o como `Work`.
5. Selecione um perfil. Se um processo relacionado estiver ativo, a troca é bloqueada e nenhum arquivo gerenciado é alterado.
6. Após uma confirmação vinculada ao conteúdo, o instantâneo gerenciado atual é salvo no último perfil criptografado para preservar tokens atualizados e mudanças intencionais de API. O instantâneo anterior exato também fica em um bloco de recuperação DPAPI exclusivo da transação.

O aplicativo marca esse perfil como **Último selecionado**, e não “atual verificado”. Se o arquivo ativo não corresponder mais ao instantâneo salvo, a alternância será pausada antes de qualquer gravação. Confirme apenas se a mudança for uma atualização da mesma conta. Se você entrou em outra conta fora do aplicativo, escolha primeiro **Salvar como novo** (ou substitua explicitamente o perfil existente com o nome correto).

O botão **Restaurar instantâneo** no cartão do último perfil selecionado verifica se o arquivo ativo ainda corresponde ao instantâneo salvo. Se for diferente, o aplicativo avisa que o login atual não salvo será substituído e vincula a aprovação àqueles bytes exatos. Outro login ativo não pode reutilizar uma confirmação anterior. Durante a restauração, uma credencial de recuperação exclusiva da transação e criptografada por DPAPI preserva os bytes anteriores até a operação ser confirmada ou revertida.

Toda transação composta ou de vários arquivos salva o instantâneo gerenciado atual exato no bloco de recuperação criptografado antes do diário, mesmo quando ele corresponde à origem salva. Isso permite reverter exatamente uma gravação parcial e atualizar com segurança um perfil antigo que continha apenas credenciais. Após uma interrupção, a recuperação prioriza esse instantâneo anterior e sincroniza o perfil de origem se o restaurar. Diários antigos sem bloco continuam recorrendo à origem salva. Se o estado ativo não corresponder à origem nem ao destino, o diário e o bloco permanecem para recuperação manual.

O commit de vários arquivos é fechado por segurança: remove primeiro a autenticação separada, mescla a configuração atomicamente e instala a autenticação de destino por último. Uma interrupção pode deixar a autenticação ausente, mas nunca combina credenciais com o endpoint do perfil oposto; a recuperação conclui ou reverte usando o instantâneo criptografado anterior.

Perfis antigos brutos do Codex e Claude Code são interpretados como credenciais mais uma configuração de API gerenciada vazia. Ativá-los limpa os campos gerenciados de rota API e modelo para não reutilizar o endpoint de terceiros do perfil anterior. Depois, configure o modelo/API desejado e capture o perfil novamente. Um perfil antigo que era a origem ativa é atualizado para o formato composto ao ser abandonado.

O aplicativo não promete login permanente. Revogação pelo provedor, política da organização, SSO, MFA ou expiração de token ainda podem exigir um login normal pelo cliente oficial.

Renomear ou excluir um perfil afeta somente o cofre local de snapshots criptografados. Renomear altera apenas o rótulo e os metadados salvos, sem alterar o conteúdo do snapshot; excluir remove apenas o snapshot local criptografado selecionado. Excluir o último perfil selecionado também limpa a associação ativa no aplicativo, mas não encerra a sessão nem altera arquivos ativos de autenticação ou configuração do provedor. Salve a conta atual antes de trocar novamente. As duas ações são recusadas enquanto uma transação de troca interrompida aguarda recuperação.

## Configurações

Abra **Configurações** na janela do aplicativo para escolher o idioma de exibição ou controlar se o aplicativo inicia com o Windows. O idioma escolhido é salvo localmente para o usuário atual do Windows e pode ser alterado a qualquer momento.

**Iniciar com o Windows** adiciona uma entrada deste aplicativo em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Ela se aplica apenas ao usuário atual do Windows e não exige privilégios de administrador. Desativar a opção remove somente a entrada de inicialização pertencente ao Coding Agent Account Switcher; os demais aplicativos de inicialização não são alterados.

**Verificar atualizações** é uma ação totalmente iniciada pelo usuário. Somente após o clique, o aplicativo envia uma única solicitação HTTPS `GET` anônima à API oficial do GitHub deste repositório. Não há verificações na inicialização, em segundo plano ou periódicas, e a solicitação não envia credenciais, configurações, nomes de perfis, identificadores do dispositivo nem telemetria. O aplicativo apenas compara metadados da release; ele nunca baixa nem executa automaticamente um instalador ou uma build portátil.

## Modelo de segurança

- Os dados criptografados de perfil são armazenados em `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
- Cada conjunto normalizado de arquivos gerenciados possui cofre, estado, diário e mutex próprios. Alterar `CODEX_HOME`, `CLAUDE_CONFIG_DIR` ou `OPENCODE_CONFIG` inicia outro conjunto de perfis.
- O DPAPI `CurrentUser` impede que outra conta do Windows descriptografe diretamente o perfil, mas não protege contra software malicioso já executado como o mesmo usuário do Windows.
- Além do arquivo normal de autenticação ativa do provedor, os bytes descriptografados do instantâneo existem apenas por pouco tempo na memória e na substituição atômica do mesmo diretório durante a captura ou alternância.
- Os arquivos temporários e de backup da substituição de autenticação usam o ID da transação de recuperação. A conclusão normal remove os dois arquivos exatos. Depois de uma interrupção, a recuperação restaura um arquivo ativo ausente a partir do instantâneo de origem criptografado ou da credencial de recuperação exclusiva da transação, remove todos os arquivos de preparação que pertencem exatamente à transação e então exclui o diário. O bloco de recuperação criptografado só é excluído depois do diário, em caráter de melhor esforço.
- O aplicativo não envia credenciais, que nunca devem ser incluídas em logs, issues, relatórios de falha, dados de teste ou commits do repositório.
- A detecção de processos é defensiva e de melhor esforço. Não inicie Codex, Claude Code ou OpenCode até a operação terminar.
- Se uma conta profissional for gerenciada por uma organização, obtenha aprovação antes de manter outro instantâneo local criptografado de autenticação.

Leia [SECURITY.md](SECURITY.md) e [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) antes de alterar o código que manipula credenciais.

## Compilar a partir do código-fonte

Requisitos:

- Windows 10 ou Windows 11
- .NET SDK 8.0.400 ou uma feature band mais recente do .NET 8

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

Criar uma build autossuficiente para Windows x64:

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## Release contínua latest

`.github/workflows/latest-release.yml` é executado a cada push para `main`:

1. Restaura e testa a solução.
2. Publica uma build portátil e autossuficiente para Windows x64.
3. Cria o instalador Windows x64 por usuário.
4. Prepara o executável portátil e as somas SHA-256 dos dois executáveis.
5. Exclui somente a release e a tag anteriores chamadas `latest`.
6. Publica uma nova release `latest` com o instalador, o executável portátil e as somas para o commit atual. Um único valor `APP_VERSION` do fluxo define a versão do executável e grava nas notas este marcador exato legível por máquina: `<!-- coding-agent-account-switcher-version: 0.1.N -->`.

Como a tag `latest` é contínua, o aplicativo lê esse marcador da resposta da API oficial do GitHub Releases somente quando o usuário verifica atualizações explicitamente. Não há verificação em segundo plano nem download ou execução automática de qualquer arquivo publicado.

Releases versionadas nunca são excluídas por esse fluxo. A opção **immutable releases** do GitHub deve permanecer desativada para a tag contínua `latest`, e as regras de branch ou tag devem permitir que o fluxo exclua `latest`. Repositórios que exigem releases imutáveis devem usar tags de build exclusivas.

O instalador e o executável portátil contínuos não são assinados no momento, portanto o Windows SmartScreen pode exibir um alerta de reputação. Revise o código-fonte e verifique a soma SHA-256 publicada correspondente antes de executar qualquer executável.

## Como contribuir

Contribuições são bem-vindas. Leia [CONTRIBUTING.md](CONTRIBUTING.md). Os testes devem usar credenciais e configurações sintéticas temporárias e nunca acessar arquivos reais do Codex, Claude Code ou OpenCode.

## Licença

[MIT](LICENSE)

# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Um alternador de contas não oficial, local e para Windows, compatível com Codex e Claude Code.

O Coding Agent Account Switcher armazena instantâneos nomeados e criptografados dos arquivos locais de autenticação usados pelo Codex e pelo Claude Code. Ele alterna somente o instantâneo de autenticação; suas configurações normais, configuração de MCP, habilidades, plugins e histórico de projetos permanecem nos locais originais.

> [!IMPORTANT]
> Este projeto não é afiliado, endossado nem patrocinado pela OpenAI ou pela Anthropic. Ele não transfere assinaturas, contorna requisitos de login, compartilha contas nem burla políticas de provedores ou organizações.

A descrição “inspirado no iOS 18” se refere apenas à direção visual geral. A Apple não é afiliada a este projeto, e nenhuma fonte, símbolo, arte ou marca da Apple é incluída.

## Download

Baixe a build atual na [release latest](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Instalador recomendado:** `coding-agent-account-switcher-setup-win-x64.exe` instala para o usuário atual do Windows sem privilégios de administrador e cria entradas no menu Iniciar e de desinstalação.
- **Pacote portátil:** `coding-agent-account-switcher-win-x64.zip` pode ser extraído e executado sem instalação.
- A release inclui um arquivo de soma SHA-256 correspondente para cada pacote.

O instalador não ativa automaticamente **Iniciar com o Windows** e não altera arquivos de autenticação ou configuração do Codex ou Claude Code. A desinstalação preserva os snapshots de contas criptografados e as configurações do aplicativo para que continuem disponíveis após uma reinstalação. O instalador e o executável portátil não são assinados no momento, portanto o Windows SmartScreen pode exibir um alerta de reputação.

## Recursos

- Interface WPF nativa do Windows com design de cartões de vidro inspirado no iOS 18.
- Idiomas integrados: inglês, chinês simplificado, chinês tradicional, espanhol, francês, alemão, japonês, coreano, português do Brasil, russo, árabe e hindi.
- Configurações no aplicativo para o idioma de exibição e a inicialização opcional com o Windows para o usuário atual.
- Perfis pessoais e profissionais nomeados para Codex e Claude Code.
- Proteção de processos que bloqueia a alternância até que os aplicativos relacionados sejam fechados.
- Credenciais tratadas como bytes opacos: sem análise de tokens, extração de e-mail ou registro de credenciais.
- Instantâneos de perfil criptografados com o Windows DPAPI para o usuário atual do Windows.
- Substituição atômica de credenciais no mesmo diretório, com suporte a reversão.
- Confirmação explícita antes que um login ativo alterado possa sobrescrever o instantâneo do último perfil selecionado.
- Ação segura **Restaurar instantâneo** para o último perfil selecionado, com confirmação vinculada aos bytes exatos da autenticação ativa que serão substituídos.
- Operação exclusivamente local, sem análise ou telemetria.
- Build contínua `latest` para Windows produzida automaticamente a partir de `main`.

## Arquivos de autenticação compatíveis

| Provedor | Arquivo de autenticação padrão alternado | Configuração que permanece intacta |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`, habilidades, MCP, sessões e outros dados |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`, `.claude.json`, plugins, MCP, configurações de projeto e histórico de sessões |

Se `CODEX_HOME` ou `CLAUDE_CONFIG_DIR` estiver definido, o aplicativo usa a raiz de autenticação específica do provedor. Mesmo assim, alterna apenas `auth.json` ou `.credentials.json`; os arquivos de configuração vizinhos permanecem intactos.

O Codex deve usar armazenamento de credenciais baseado em arquivo. Se sua instalação usa o armazenamento de credenciais do sistema operacional, adicione esta configuração ao `config.toml` na raiz ativa do Codex (`%USERPROFILE%\.codex` por padrão, ou `CODEX_HOME` quando definido):

```toml
cli_auth_credentials_store = "file"
```

Consulte a documentação oficial de [autenticação do Codex](https://developers.openai.com/codex/auth) e [autenticação do Claude Code](https://code.claude.com/docs/en/authentication) para os contratos atuais de armazenamento.

## Como funciona

1. Entre na primeira conta pelo fluxo de login oficial do provedor.
2. Feche completamente o Codex/Claude Code e qualquer cliente local ou extensão relacionada.
3. Salve o login atual com um nome escolhido por você, como `Personal`.
4. Entre na segunda conta e salve-a com outro nome, como `Work`.
5. Selecione um perfil salvo. O aplicativo verifica os processos relacionados antes de qualquer alteração. Se um estiver em execução, a alternância é bloqueada e nenhum arquivo de credenciais é modificado.
6. Ao mudar para outro perfil, o arquivo de autenticação atual é salvo no último perfil criptografado selecionado depois de uma confirmação vinculada aos bytes, preservando os tokens atualizados.

O aplicativo marca esse perfil como **Último selecionado**, e não “atual verificado”. Se o arquivo ativo não corresponder mais ao instantâneo salvo, a alternância será pausada antes de qualquer gravação. Confirme apenas se a mudança for uma atualização da mesma conta. Se você entrou em outra conta fora do aplicativo, escolha primeiro **Salvar como novo** (ou substitua explicitamente o perfil existente com o nome correto).

O botão **Restaurar instantâneo** no cartão do último perfil selecionado verifica se o arquivo ativo ainda corresponde ao instantâneo salvo. Se for diferente, o aplicativo avisa que o login atual não salvo será substituído e vincula a aprovação àqueles bytes exatos. Outro login ativo não pode reutilizar uma confirmação anterior. Durante a restauração, uma credencial de recuperação exclusiva da transação e criptografada por DPAPI preserva os bytes anteriores até a operação ser confirmada ou revertida.

O aplicativo não promete login permanente. Revogação pelo provedor, política da organização, SSO, MFA ou expiração de token ainda podem exigir um login normal pelo cliente oficial.

## Configurações

Abra **Configurações** na janela do aplicativo para escolher o idioma de exibição ou controlar se o aplicativo inicia com o Windows. O idioma escolhido é salvo localmente para o usuário atual do Windows e pode ser alterado a qualquer momento.

**Iniciar com o Windows** adiciona uma entrada deste aplicativo em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Ela se aplica apenas ao usuário atual do Windows e não exige privilégios de administrador. Desativar a opção remove somente a entrada de inicialização pertencente ao Coding Agent Account Switcher; os demais aplicativos de inicialização não são alterados.

## Modelo de segurança

- Os dados criptografados de perfil são armazenados em `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
- Cada local normalizado do arquivo de autenticação possui um cofre, estado ativo, diário de recuperação e mutex próprios, isolados por hash. Alterar `CODEX_HOME` ou `CLAUDE_CONFIG_DIR` inicia, portanto, um conjunto independente de perfis, em vez de reutilizar a conta ativa de outro local.
- O DPAPI `CurrentUser` impede que outra conta do Windows descriptografe diretamente o perfil, mas não protege contra software malicioso já executado como o mesmo usuário do Windows.
- Além do arquivo normal de autenticação ativa do provedor, os bytes descriptografados do instantâneo existem apenas por pouco tempo na memória e na substituição atômica do mesmo diretório durante a captura ou alternância.
- Os arquivos temporários e de backup da substituição de autenticação usam o ID da transação de recuperação. A conclusão normal remove os dois arquivos exatos. Depois de uma interrupção, a recuperação restaura um arquivo ativo ausente a partir do instantâneo de origem criptografado ou da credencial de recuperação exclusiva da transação, remove todos os arquivos de preparação que pertencem exatamente à transação e então exclui o diário.
- O aplicativo não envia credenciais, que nunca devem ser incluídas em logs, issues, relatórios de falha, dados de teste ou commits do repositório.
- A detecção de processos é defensiva e de melhor esforço. Um processo recém-iniciado pode competir com a alternância; não inicie o Codex ou o Claude Code até a operação terminar.
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
4. Cria o ZIP portátil e as somas SHA-256 dos dois pacotes.
5. Exclui somente a release e a tag anteriores chamadas `latest`.
6. Publica uma nova release `latest` com o instalador, o pacote portátil e as somas para o commit atual.

Releases versionadas nunca são excluídas por esse fluxo. A opção **immutable releases** do GitHub deve permanecer desativada para a tag contínua `latest`, e as regras de branch ou tag devem permitir que o fluxo exclua `latest`. Repositórios que exigem releases imutáveis devem usar tags de build exclusivas.

O instalador e o executável portátil contínuos não são assinados no momento, portanto o Windows SmartScreen pode exibir um alerta de reputação. Revise o código-fonte e verifique a soma SHA-256 publicada correspondente antes de executar qualquer pacote.

## Como contribuir

Contribuições são bem-vindas. Leia [CONTRIBUTING.md](CONTRIBUTING.md). Os testes devem usar arquivos de credenciais falsos e temporários e nunca devem acessar os arquivos reais de autenticação do Codex ou Claude Code de um desenvolvedor.

## Licença

[MIT](LICENSE)

# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Ícone do Coding Agent Account Switcher">
</p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Um aplicativo simples para Windows que alterna entre contas ou configurações de
sites de API salvas para Codex, Claude Code e OpenCode.

Ele altera somente as informações da conta e as configurações de API compatíveis
salvas no perfil. Servidores MCP, skills, plugins, projetos e histórico permanecem
onde estão.

> [!IMPORTANT]
> Este é um projeto comunitário não oficial. Não é afiliado à OpenAI, Anthropic,
> Apple nem ao projeto OpenCode. Ele não transfere assinaturas, não contorna o
> login e não substitui políticas da organização.

## Download

Obtenha a versão atual na
[release mais recente](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Instalador:** <code>CAAS-vX.Y.Z-Setup-x64.exe</code>
- **Aplicativo portátil:** <code>CAAS-vX.Y.Z-Portable-x64.exe</code>
- **Checksums:** cada executável possui um arquivo SHA-256 correspondente

A instalação é somente para o usuário atual, não exige privilégios de
administrador e não ativa **Iniciar com o Windows** sem sua escolha. Os builds
atuais não são assinados, portanto o Windows SmartScreen pode exibir um aviso.

## Principais recursos

- Salve perfis pessoais, profissionais e de sites de API com nomes claros.
- Troque contas do Codex, Claude Code e OpenCode com poucos cliques.
- Mantenha endereços, chaves e roteamento de provedor de API compatíveis no perfil correto.
- Renomeie um perfil com dois cliques ou exclua um snapshot local.
- Veja uma confirmação clara do perfil ativo após a troca.
- Bloqueie a troca enquanto aplicativos relacionados estiverem abertos.
- Use um dos 12 idiomas integrados.
- Mantenha o tema claro/escuro e o idioma entre execuções.
- Na mesma sessão do Windows, abrir o aplicativo novamente restaura e traz para
  frente a janela existente em vez de criar outra.
- Inicie opcionalmente com o Windows e verifique atualizações manualmente.

Tudo permanece no computador. O aplicativo não possui análise nem telemetria.

## Aplicativos compatíveis

| Aplicativo | O que é trocado | O que permanece igual |
| --- | --- | --- |
| Codex | Login e conexão do provedor de API selecionado | Modelos, opções de revisão/raciocínio, recursos, MCP, skills, sessões, histórico e outras configurações |
| Claude Code | Login e configurações compatíveis de endereço/chave de API | Plugins, MCP, projetos, histórico e outras configurações |
| OpenCode | Login salvo e configurações compatíveis de provedor/modelo | Configuração do projeto e outras configurações |

Somente os campos de conta compatíveis com o projeto são alterados. Consulte a
[arquitetura](docs/ARCHITECTURE.md) para ver a lista exata.

## Início rápido

1. Entre normalmente ou configure o site de API desejado.
2. Abra o aplicativo e escolha o provedor correspondente.
3. Selecione **Salvar conta atual** e use um nome como <code>Personal</code>.
4. Entre na segunda conta ou configure outro site de API.
5. Salve como <code>Work</code>, por exemplo.
6. Antes de trocar, feche completamente o aplicativo relacionado e escolha um perfil salvo.

Você pode salvar uma conta enquanto o aplicativo relacionado está aberto. Antes
de trocar, o aplicativo verifica processos em execução. Se algo estiver aberto,
ele pede para fechar e não faz nenhuma alteração.

Se a conta atual mudou desde que foi salva, o aplicativo pede confirmação. Se for
realmente outra conta, salve-a primeiro como um novo perfil.

## Perfis e configurações

- **Renomear:** clique duas vezes no nome do perfil.
- **Excluir:** use o botão da lixeira. Somente o snapshot local criptografado é
  removido; a conta não é excluída e a sessão não é encerrada.
- **Último selecionado:** indica o perfil ativado mais recentemente pelo aplicativo.
- **Snapshot danificado:** um item ausente ou ilegível é ocultado, enquanto os
  perfis válidos continuam disponíveis.
- **Tema e idioma:** são lembrados para o usuário atual do Windows.
- **Iniciar com o Windows:** opcional, somente para o usuário atual e sem privilégios de administrador.
- **Verificar atualizações:** executa apenas após o clique, sem download ou instalação automática.

## Privacidade e segurança

- Os perfis são criptografados com o Windows DPAPI para o usuário atual.
- Credenciais e chaves de API não são exibidas nem gravadas em logs.
- Os perfis ficam em <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code>.
- A troca usa substituição protegida e recuperação para reduzir alterações parciais.
- O aplicativo não envia credenciais, nomes de perfis, identificadores do dispositivo ou telemetria.
- Contas profissionais podem seguir políticas da organização; obtenha autorização
  antes de salvar outra cópia local do login.

Leia [SECURITY.md](SECURITY.md) antes de relatar um problema de segurança.

## Limitações

- Logout do provedor, expiração do token, SSO, MFA ou políticas da organização
  ainda podem exigir um login normal.
- Conversas existentes do Codex permanecem vinculadas ao provedor usado quando
  foram criadas. Após trocar de provedor, inicie uma nova conversa ou volte ao
  provedor original para continuar a conversa anterior.
- O aplicativo não transfere assinaturas nem garante uma sessão permanente.
- Atualmente, apenas Windows 10 e Windows 11 x64 são compatíveis.

## Compilar do código-fonte

Requer Windows e .NET SDK 8.0.400 ou uma feature band mais recente do .NET 8.

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

Cada push para <code>main</code> publica uma nova release versionada pelo GitHub
Actions. Somente a release mais recente mantém o instalador e o aplicativo portátil.

## Contribuição

Contribuições são bem-vindas. Leia [CONTRIBUTING.md](CONTRIBUTING.md).
Nunca inclua credenciais ou chaves de API reais em issues, logs, testes ou commits.

## Avisos de terceiros

Este aplicativo usa [Tomlyn](https://github.com/xoofx/Tomlyn), licenciado sob a
licença BSD de 2 cláusulas. O aviso completo de direitos autorais e licença está
no [README em inglês](README.md#third-party-notices).

## Licença

[MIT](LICENSE)

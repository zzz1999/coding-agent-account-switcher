# Coding Agent Account Switcher

<p align="center">
  <img src="src/CodingAgentAccountSwitcher.App/Assets/CodingAgentAccountSwitcher.png" width="144" alt="Icono de Coding Agent Account Switcher">
</p>

[English](README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Una aplicación sencilla para Windows que permite cambiar entre cuentas o
configuraciones de sitios API guardadas para Codex, Claude Code y OpenCode.

Solo cambia la información de la cuenta y los ajustes API compatibles guardados
en cada perfil. Los servidores MCP, habilidades, complementos, proyectos y el
historial permanecen en su lugar.

> [!IMPORTANT]
> Este es un proyecto comunitario no oficial. No está afiliado a OpenAI,
> Anthropic, Apple ni al proyecto OpenCode. No transfiere suscripciones, no evita
> requisitos de inicio de sesión ni sustituye las políticas de una organización.

## Descarga

Obtenga la versión actual desde la
[última versión](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Instalador:** <code>CAAS-vX.Y.Z-Setup-x64.exe</code>
- **Aplicación portátil:** <code>CAAS-vX.Y.Z-Portable-x64.exe</code>
- **Sumas de comprobación:** cada ejecutable incluye su archivo SHA-256

El instalador es para el usuario actual, no requiere privilegios de administrador
y no activa **Iniciar con Windows** sin su permiso. Las compilaciones actuales no
están firmadas, por lo que Windows SmartScreen puede mostrar una advertencia.

## Funciones principales

- Guarde perfiles personales, de trabajo y de sitios API con nombres claros.
- Cambie cuentas de Codex, Claude Code y OpenCode con unos pocos clics.
- Mantenga las direcciones, claves y rutas de proveedor API compatibles en el
  perfil correcto.
- Cambie el nombre de un perfil con doble clic o elimine una instantánea local.
- Vea una confirmación clara del perfil activo después del cambio.
- Impida el cambio mientras las aplicaciones relacionadas estén abiertas.
- Use cualquiera de los 10 idiomas integrados.
- Conserve el tema claro u oscuro y el idioma entre inicios.
- En la misma sesión de Windows, abrir la aplicación de nuevo restaura y trae al
  frente la ventana existente en lugar de crear otra.
- Inicie opcionalmente con Windows y compruebe actualizaciones de forma manual.

Todo permanece en el equipo. La aplicación no incluye análisis ni telemetría.

## Aplicaciones compatibles

| Aplicación | Qué cambia | Qué permanece igual |
| --- | --- | --- |
| Codex | Inicio de sesión y conexión del proveedor API seleccionado | Modelos, opciones de revisión/razonamiento, funciones, MCP, habilidades, sesiones, historial y otros ajustes |
| Claude Code | Inicio de sesión y ajustes compatibles de dirección/clave API | Complementos, MCP, proyectos, historial y otros ajustes |
| OpenCode | Inicio de sesión guardado y ajustes compatibles de proveedor/modelo | Configuración del proyecto y otros ajustes |

Solo se cambian los campos de cuenta compatibles con el proyecto. Consulte la
[arquitectura](docs/ARCHITECTURE.md) para conocer la lista exacta.

## Inicio rápido

1. Inicie sesión normalmente o configure el sitio API que quiera usar.
2. Abra la aplicación y elija el proveedor correspondiente.
3. Pulse **Guardar cuenta actual** y asígnele un nombre, por ejemplo <code>Personal</code>.
4. Inicie sesión con la segunda cuenta o configure otro sitio API.
5. Guárdela como <code>Work</code>, por ejemplo.
6. Antes de cambiar, cierre por completo la aplicación relacionada y seleccione un perfil guardado.

Puede guardar una cuenta mientras su aplicación está abierta. Antes de cambiar,
la aplicación busca procesos en ejecución. Si algo sigue abierto, pide que lo
cierre y no realiza ningún cambio.

Si la cuenta actual cambió desde la última vez que se guardó, la aplicación pide
confirmación. Si realmente es otra cuenta, guárdela primero como un perfil nuevo.

## Perfiles y ajustes

- **Cambiar nombre:** haga doble clic en el nombre del perfil.
- **Eliminar:** use el botón de la papelera. Solo elimina la instantánea local
  cifrada; no borra la cuenta ni cierra la sesión.
- **Último seleccionado:** indica el perfil activado más recientemente por la aplicación.
- **Instantánea dañada:** una instantánea ausente o ilegible se oculta, mientras
  los perfiles válidos siguen disponibles.
- **Tema e idioma:** se recuerdan para el usuario actual de Windows.
- **Iniciar con Windows:** opcional, solo para el usuario actual y sin permisos de administrador.
- **Buscar actualizaciones:** solo se ejecuta al pulsarlo; nunca descarga ni instala automáticamente.

## Privacidad y seguridad

- Los perfiles se cifran con Windows DPAPI para el usuario actual.
- Las credenciales y claves API no se muestran ni se escriben en los registros.
- Los perfiles se guardan en <code>%LOCALAPPDATA%\CodingAgentAccountSwitcher</code>.
- El cambio usa sustitución protegida y recuperación para reducir cambios parciales.
- La aplicación no envía credenciales, nombres de perfiles, identificadores del
  dispositivo ni telemetría.
- Las cuentas de trabajo pueden estar sujetas a políticas de la organización;
  obtenga autorización antes de guardar otra copia local del inicio de sesión.

Lea [SECURITY.md](SECURITY.md) antes de comunicar un problema de seguridad.

## Limitaciones

- El cierre de sesión del proveedor, la caducidad del token, SSO, MFA o las
  políticas de la organización pueden exigir un inicio de sesión normal.
- Las conversaciones existentes de Codex quedan vinculadas al proveedor con el
  que se crearon. Tras cambiar de proveedor, inicie una conversación nueva o
  vuelva al proveedor original para continuar la anterior.
- La aplicación no transfiere suscripciones ni garantiza una sesión permanente.
- Actualmente solo se admiten Windows 10 y Windows 11 x64.

## Compilar desde el código fuente

Requiere Windows y .NET SDK 8.0.400 o una versión posterior de .NET 8.

~~~powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
~~~

Cada envío a <code>main</code> publica una nueva versión numerada mediante GitHub
Actions. Solo la versión más reciente conserva el instalador y la aplicación portátil.

## Contribuir

Las contribuciones son bienvenidas. Lea [CONTRIBUTING.md](CONTRIBUTING.md).
Nunca incluya credenciales ni claves API reales en incidencias, registros,
pruebas o commits.

## Avisos de terceros

Esta aplicación usa [Tomlyn](https://github.com/xoofx/Tomlyn), distribuido bajo
la licencia BSD de 2 cláusulas. Consulta el aviso completo de copyright y
licencia en el [README en inglés](README.md#third-party-notices).

El instalador también incluye traducciones de [Inno Setup](https://jrsoftware.org/),
bajo la [licencia de Inno Setup](installer/Languages/LICENSE.txt).

## Licencia

[MIT](LICENSE)

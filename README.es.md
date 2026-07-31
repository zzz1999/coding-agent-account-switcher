# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Un selector de cuentas no oficial, local y para Windows, compatible con Codex y Claude Code.

Coding Agent Account Switcher guarda instantáneas cifradas y con nombre de los archivos de autenticación locales que usan Codex y Claude Code. Solo cambia la instantánea de autenticación; la configuración habitual, la configuración de MCP, las habilidades, los complementos y el historial de proyectos permanecen en sus ubicaciones originales.

> [!IMPORTANT]
> Este proyecto no está afiliado, respaldado ni patrocinado por OpenAI ni Anthropic. No transfiere suscripciones, no elude los requisitos de inicio de sesión, no comparte cuentas ni sortea las políticas del proveedor o de una organización.

La mención «inspirado en iOS 18» se refiere únicamente a la dirección visual general. Apple no está afiliada con este proyecto y no se incluyen tipografías, símbolos, ilustraciones ni marcas de Apple.

## Descarga

Descargue la compilación actual desde la [versión latest](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Instalador recomendado:** `coding-agent-account-switcher-setup-win-x64.exe` instala la aplicación para el usuario actual de Windows sin privilegios de administrador y crea accesos en el menú Inicio y para la desinstalación.
- **Paquete portátil:** `coding-agent-account-switcher-win-x64.zip` se puede extraer y ejecutar sin instalación.
- La versión incluye un archivo de suma SHA-256 correspondiente para cada paquete.

El instalador no activa automáticamente **Iniciar con Windows** ni modifica los archivos de autenticación o configuración de Codex o Claude Code. La desinstalación conserva las instantáneas de cuentas cifradas y los ajustes de la aplicación para que sigan disponibles tras reinstalarla. El instalador y el ejecutable portátil no están firmados actualmente, por lo que Windows SmartScreen puede mostrar una advertencia de reputación.

## Funciones

- Interfaz WPF nativa de Windows con un diseño de tarjetas de cristal inspirado en iOS 18.
- Idiomas integrados: inglés, chino simplificado, chino tradicional, español, francés, alemán, japonés, coreano, portugués de Brasil, ruso, árabe e hindi.
- Ajustes dentro de la aplicación para elegir el idioma y, opcionalmente, iniciar con Windows para el usuario actual.
- Perfiles personales y de trabajo con nombre para Codex y Claude Code.
- Protección por procesos que impide el cambio hasta cerrar las aplicaciones relacionadas.
- Las credenciales se tratan como bytes opacos: no se analizan tokens, no se extraen correos electrónicos y no se registran credenciales.
- Instantáneas de perfil cifradas con Windows DPAPI para el usuario actual de Windows.
- Sustitución atómica de credenciales en el mismo directorio, con recuperación ante errores.
- Confirmación explícita antes de que un inicio de sesión activo modificado sobrescriba la instantánea del último perfil seleccionado.
- Acción segura **Restaurar instantánea** para el último perfil seleccionado, con una confirmación vinculada a los bytes exactos de autenticación que serán reemplazados.
- Funcionamiento exclusivamente local, sin análisis ni telemetría.
- Compilación continua `latest` para Windows generada automáticamente desde `main`.

## Archivos de autenticación compatibles

| Proveedor | Archivo de autenticación predeterminado que se cambia | Configuración que no se modifica |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.codex\config.toml`, habilidades, MCP, sesiones y otros datos |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` | `settings.json`, `.claude.json`, complementos, MCP, configuración de proyectos e historial de sesiones |

Si se define `CODEX_HOME` o `CLAUDE_CONFIG_DIR`, la aplicación usa la raíz de autenticación específica del proveedor. Aun así, solo cambia `auth.json` o `.credentials.json`; los archivos de configuración vecinos no se alteran.

Codex debe usar almacenamiento de credenciales basado en archivos. Si su instalación utiliza el almacén de credenciales del sistema operativo, añada este ajuste a `config.toml` en la raíz activa de Codex (`%USERPROFILE%\.codex` de forma predeterminada, o `CODEX_HOME` cuando esté definido):

```toml
cli_auth_credentials_store = "file"
```

Consulte la documentación oficial de [autenticación de Codex](https://developers.openai.com/codex/auth) y [autenticación de Claude Code](https://code.claude.com/docs/en/authentication) para conocer los contratos de almacenamiento actuales.

## Cómo funciona

1. Inicie sesión en la primera cuenta mediante el flujo oficial del proveedor.
2. Cierre por completo Codex/Claude Code y cualquier cliente o extensión local relacionado.
3. Guarde el inicio de sesión actual con una etiqueta elegida por usted, como `Personal`.
4. Inicie sesión en la segunda cuenta y guárdela con otra etiqueta, como `Work`.
5. Seleccione un perfil guardado. La aplicación comprueba los procesos relacionados antes de realizar cambios. Si alguno está en ejecución, se bloquea el cambio y no se modifica ningún archivo de credenciales.
6. Al cambiar a otro perfil, el archivo de autenticación actual se guarda en el último perfil cifrado seleccionado después de una confirmación vinculada a sus bytes, para conservar los tokens renovados.

La aplicación etiqueta ese perfil como **Último seleccionado**, no como «actual verificado». Si el archivo activo ya no coincide con su instantánea, el cambio se detiene antes de escribir. Confirme solo si la modificación es una renovación de la misma cuenta. Si inició sesión en otra cuenta fuera de la aplicación, elija primero **Guardar como nuevo** (o reemplace explícitamente el perfil existente con el nombre correcto).

El botón **Restaurar instantánea** de la tarjeta del último perfil seleccionado comprueba si el archivo activo aún coincide con la instantánea. Si difiere, la aplicación advierte que sustituirá el inicio de sesión actual no guardado y vincula la aprobación a esos bytes exactos. Un inicio de sesión activo distinto no puede reutilizar una confirmación anterior. Durante la restauración, una credencial de recuperación exclusiva de la transacción y cifrada con DPAPI conserva los bytes anteriores hasta confirmar o revertir la operación.

La aplicación no garantiza que una sesión dure indefinidamente. Una revocación del proveedor, una política de la organización, SSO, MFA o el vencimiento del token aún pueden requerir un inicio de sesión normal en el cliente oficial.

## Ajustes

Abra **Ajustes** desde la ventana de la aplicación para elegir el idioma o controlar si la aplicación se inicia con Windows. El idioma se guarda localmente para el usuario actual de Windows y puede cambiarse en cualquier momento.

**Iniciar con Windows** añade una entrada para esta aplicación en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Solo afecta al usuario actual de Windows y no requiere privilegios de administrador. Al desactivarlo se elimina únicamente la entrada de inicio perteneciente a Coding Agent Account Switcher, sin alterar otras aplicaciones de inicio.

## Modelo de seguridad

- Los perfiles cifrados se almacenan bajo `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
- Cada ubicación normalizada del archivo de autenticación tiene un almacén, estado activo, diario de recuperación y mutex independientes, delimitados por un hash. Cambiar `CODEX_HOME` o `CLAUDE_CONFIG_DIR` inicia por tanto un conjunto de perfiles independiente, sin reutilizar la cuenta activa de otra ubicación.
- DPAPI `CurrentUser` impide que otra cuenta de Windows descifre directamente el perfil, pero no protege frente a software malicioso que ya se ejecute como el mismo usuario.
- Aparte del archivo activo normal del proveedor, los bytes descifrados solo existen brevemente en memoria y en la sustitución atómica del mismo directorio durante la captura o el cambio.
- Los archivos temporales y de copia de seguridad de la sustitución usan el ID de la transacción de recuperación. Al terminar normalmente se eliminan ambos archivos exactos. Tras una interrupción, la recuperación restaura un archivo activo ausente desde la instantánea de origen cifrada o la credencial de recuperación exclusiva de la transacción, elimina todos los archivos de preparación que pertenecen exactamente a esa transacción y luego borra el diario.
- La aplicación no carga credenciales, que nunca deben incluirse en registros, incidencias, informes de fallos, datos de prueba ni commits del repositorio.
- La detección de procesos es defensiva y de mejor esfuerzo. Un proceso recién iniciado puede competir con el cambio; no abra Codex ni Claude Code hasta que termine la operación.
- Si una organización administra su cuenta de trabajo, obtenga autorización antes de conservar otra instantánea local cifrada.

Lea [SECURITY.md](SECURITY.md) y [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) antes de modificar el código que gestiona credenciales.

## Compilar desde el código fuente

Requisitos:

- Windows 10 o Windows 11
- .NET SDK 8.0.400 o una feature band posterior de .NET 8

```powershell
dotnet restore CodingAgentAccountSwitcher.sln
dotnet test CodingAgentAccountSwitcher.sln --configuration Release
dotnet run --project .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj
```

Para crear una compilación autónoma de Windows x64:

```powershell
dotnet publish .\src\CodingAgentAccountSwitcher.App\CodingAgentAccountSwitcher.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\artifacts\publish
```

## Versión continua latest

`.github/workflows/latest-release.yml` se ejecuta con cada push a `main`:

1. Restaura y prueba la solución.
2. Publica una compilación portátil autónoma para Windows x64.
3. Crea el instalador para el usuario actual de Windows x64.
4. Crea el ZIP portátil y las sumas SHA-256 de ambos paquetes.
5. Elimina únicamente la versión y la etiqueta anteriores llamadas `latest`.
6. Publica una nueva versión `latest` con el instalador, el paquete portátil y las sumas para el commit actual.

El flujo nunca elimina versiones numeradas. La opción **immutable releases** de GitHub debe estar desactivada para la etiqueta continua `latest`, y las reglas de ramas o etiquetas deben permitir que el flujo elimine `latest`. Los repositorios que exijan versiones inmutables deben usar etiquetas de compilación únicas.

El instalador y el ejecutable portátil continuos no están firmados actualmente, por lo que Windows SmartScreen puede mostrar una advertencia de reputación. Revise el código fuente y verifique la suma SHA-256 publicada correspondiente antes de ejecutar cualquiera de los paquetes.

## Contribuir

Las contribuciones son bienvenidas. Lea [CONTRIBUTING.md](CONTRIBUTING.md). Las pruebas deben usar archivos de credenciales falsos y temporales; nunca deben acceder a los archivos reales de autenticación de Codex o Claude Code de un desarrollador.

## Licencia

[MIT](LICENSE)

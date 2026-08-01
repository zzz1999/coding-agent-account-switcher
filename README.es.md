# Coding Agent Account Switcher

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md)

Un selector local y no oficial de cuentas y sitios API para Codex, Claude Code y OpenCode en Windows.

Coding Agent Account Switcher guarda instantáneas cifradas de las credenciales locales junto con solo los campos de proveedor API necesarios. Los demás ajustes, MCP, habilidades, complementos e historial permanecen en sus ubicaciones originales.

> [!IMPORTANT]
> Este proyecto no está afiliado, respaldado ni patrocinado por OpenAI ni Anthropic. No transfiere suscripciones, no elude los requisitos de inicio de sesión, no comparte cuentas ni sortea las políticas del proveedor o de una organización.

La mención «inspirado en iOS 18» se refiere únicamente a la dirección visual general. Apple no está afiliada con este proyecto y no se incluyen tipografías, símbolos, ilustraciones ni marcas de Apple.

## Descarga

Descargue la compilación actual desde la [versión latest](https://github.com/zzz1999/coding-agent-account-switcher/releases/latest):

- **Instalador recomendado:** `coding-agent-account-switcher-setup-win-x64.exe` instala la aplicación para el usuario actual de Windows sin privilegios de administrador y crea accesos en el menú Inicio y para la desinstalación.
- **Ejecutable portátil:** descargue `coding-agent-account-switcher-portable-win-x64.exe` y ejecútelo directamente, sin necesidad de instalación.
- La versión incluye un archivo de suma SHA-256 correspondiente para cada ejecutable.

El instalador no activa automáticamente **Iniciar con Windows** ni modifica archivos de autenticación o configuración de Codex, Claude Code u OpenCode. La desinstalación conserva las instantáneas de cuentas cifradas y los ajustes de la aplicación para que sigan disponibles tras reinstalarla. El instalador y el ejecutable portátil no están firmados actualmente, por lo que Windows SmartScreen puede mostrar una advertencia de reputación.

## Funciones

- Interfaz WPF nativa de Windows con un diseño de tarjetas de cristal inspirado en iOS 18.
- Idiomas integrados: inglés, chino simplificado, chino tradicional, español, francés, alemán, japonés, coreano, portugués de Brasil, ruso, árabe e hindi.
- Ajustes dentro de la aplicación para elegir el idioma y, opcionalmente, iniciar con Windows para el usuario actual.
- Perfiles personales, de trabajo y de sitios API para Codex, Claude Code y OpenCode.
- Protección por procesos que impide el cambio hasta cerrar las aplicaciones relacionadas.
- Los archivos de autenticación siguen siendo bytes opacos. Solo se analizan y fusionan los campos gestionados indicados abajo; los secretos nunca se muestran ni registran.
- Instantáneas de perfil cifradas con Windows DPAPI para el usuario actual de Windows.
- Sustitución atómica de los archivos gestionados en el mismo directorio, con recuperación ante errores.
- Confirmación explícita antes de que un inicio de sesión activo modificado sobrescriba la instantánea del último perfil seleccionado.
- Acción segura **Restaurar instantánea** para el último perfil seleccionado, con una confirmación vinculada a los bytes exactos de autenticación que serán reemplazados.
- Funcionamiento exclusivamente local, sin análisis ni telemetría.
- Compilación continua `latest` para Windows generada automáticamente desde `main`.

## Configuración de cuenta y API compatible

| Proveedor | Archivos gestionados | Configuración gestionada selectivamente |
| --- | --- | --- |
| Codex | `%USERPROFILE%\.codex\auth.json` y `config.toml` | `model_provider`, `openai_base_url`, `model`, `review_model`, `model_reasoning_effort`, `disable_response_storage`, la tabla activa seleccionada de `model_providers` y `features.responses_websockets_v2` |
| Claude Code | `%USERPROFILE%\.claude\.credentials.json` y `settings.json` | Solo `env.ANTHROPIC_BASE_URL`, `env.ANTHROPIC_API_KEY`, `env.ANTHROPIC_AUTH_TOKEN`, `env.CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC` y el campo de compatibilidad `env.CLAUDE_CODE_ATTRIBUTION_HEADER` |
| OpenCode | `%USERPROFILE%\.local\share\opencode\auth.json` opcional; capas globales `config.json`, `opencode.json`, `opencode.jsonc`; después `OPENCODE_CONFIG` si está definido | Credenciales opacas de `/connect` y el perfil API efectivo de `provider`, `model` y `small_model` |

La aplicación fusiona esos campos sin reemplazar el archivo completo. En Codex conserva `network_access`, `windows_wsl_setup_acknowledged`, `features.goals`, `cli_auth_credentials_store`, MCP, habilidades, sesiones y cualquier otro valor. En Claude Code conserva las demás entradas `env`, `.claude.json`, complementos, MCP, ajustes de proyecto e historial. En OpenCode conserva los valores semánticos no relacionados de cada archivo global o personalizado participante.

La raíz de configuración predeterminada de OpenCode es `%USERPROFILE%\.config` y la raíz de datos predeterminada es `%USERPROFILE%\.local\share`. Un valor absoluto de `XDG_CONFIG_HOME` o `XDG_DATA_HOME` sustituye la raíz predeterminada correspondiente; un valor vacío no la sustituye. Todas las anulaciones de ruta no vacías de OpenCode (`XDG_CONFIG_HOME`, `XDG_DATA_HOME`, `OPENCODE_CONFIG` y `OPENCODE_CONFIG_DIR`) deben ser absolutas; un valor relativo bloquea la captura y el cambio antes de cualquier escritura. La configuración global se carga desde `opencode\config.json`, `opencode.json` y `opencode.jsonc` bajo la raíz de configuración, en ese orden; cada capa posterior prevalece. `OPENCODE_CONFIG` se carga al final. La aplicación captura el resultado efectivo de `provider`, `model` y `small_model` como un único perfil API. Las credenciales de `/connect` se leen de `opencode\auth.json` bajo la raíz de datos.

OpenCode admite tanto credenciales de `/connect` en su `auth.json` como claves API incluidas en el objeto `provider`; la instantánea guarda el estilo presente, o ambos.

Al aplicar, la aplicación elimina `provider`, `model` y `small_model` de las demás capas globales para impedir que un endpoint antiguo prevalezca. Con `OPENCODE_CONFIG`, escribe allí el destino y limpia las tres capas globales. Sin él, los valores no vacíos se normalizan en `opencode.jsonc` global y se eliminan de `config.json` y `opencode.json`, aunque antes solo existiera JSON o el archivo heredado. Una instantánea solo de autenticación o con configuración gestionada vacía es válida: limpia los valores existentes sin crear un `opencode.jsonc` vacío.

La fusión selectiva conserva los valores semánticos no relacionados, pero no garantiza conservar byte por byte el formato ni los comentarios si se vuelve a serializar JSON o TOML.

Antes de capturar o cambiar OpenCode, la aplicación comprueba en modo de solo lectura las variables de entorno conocidas de mayor prioridad. Un valor no vacío de `OPENCODE_AUTH_CONTENT` bloquea la operación. `OPENCODE_CONFIG_CONTENT` solo se permite si es JSON o JSONC válido y no contiene las claves superiores gestionadas `provider`, `model` o `small_model`; el contenido no válido o cualquiera de esas claves bloquea la operación. Si se define `OPENCODE_CONFIG_DIR`, se comprueban sus archivos `opencode.json` y `opencode.jsonc`; un archivo ilegible o no válido, o una clave gestionada en cualquiera de ellos, bloquea la operación. Se permite la configuración en línea o de directorio que solo contenga claves no relacionadas. La aplicación no modifica ninguna de estas fuentes proporcionadas por el entorno.

`CODEX_HOME` y `CLAUDE_CONFIG_DIR` cambian las raíces respectivas. La configuración OpenCode de proyecto, las fuentes administradas centralmente y las variables de entorno específicas del proveedor siguen sin estar gestionadas y pueden prevalecer sobre el perfil global seleccionado después de un cambio; la aplicación no las busca ni modifica. La protección de procesos comprueba `opencode` y `opencode-cli`. `disable_response_storage`, `features.responses_websockets_v2` y `CLAUDE_CODE_ATTRIBUTION_HEADER` son campos de compatibilidad; no se afirma que todas las versiones actuales los documenten.

Codex debe usar almacenamiento de credenciales basado en archivos. Si su instalación utiliza el almacén de credenciales del sistema operativo, añada este ajuste a `config.toml` en la raíz activa de Codex (`%USERPROFILE%\.codex` de forma predeterminada, o `CODEX_HOME` cuando esté definido):

```toml
cli_auth_credentials_store = "file"
```

Consulte la documentación oficial de [autenticación de Codex](https://developers.openai.com/codex/auth) y [autenticación de Claude Code](https://code.claude.com/docs/en/authentication) para conocer los contratos de almacenamiento actuales.

## Cómo funciona

1. Inicie sesión mediante el flujo oficial o configure un sitio API compatible en los archivos normales del proveedor.
2. Cierre por completo Codex, Claude Code, OpenCode y cualquier cliente o extensión relacionado.
3. Guarde la cuenta y los ajustes API gestionados con una etiqueta como `Personal`.
4. Inicie sesión en otra cuenta o configure otro sitio API y guárdelo como `Work`.
5. Seleccione un perfil guardado. Si un proceso relacionado está activo, se bloquea el cambio y no se modifica ningún archivo gestionado.
6. Tras una confirmación vinculada al contenido, la instantánea gestionada actual se guarda en el último perfil cifrado para conservar tokens renovados y cambios API intencionales. La instantánea exacta previa al cambio también se conserva en un bloque de recuperación DPAPI exclusivo de la transacción.

La aplicación etiqueta ese perfil como **Último seleccionado**, no como «actual verificado». Si el archivo activo ya no coincide con su instantánea, el cambio se detiene antes de escribir. Confirme solo si la modificación es una renovación de la misma cuenta. Si inició sesión en otra cuenta fuera de la aplicación, elija primero **Guardar como nuevo** (o reemplace explícitamente el perfil existente con el nombre correcto).

El botón **Restaurar instantánea** de la tarjeta del último perfil seleccionado comprueba si el archivo activo aún coincide con la instantánea. Si difiere, la aplicación advierte que sustituirá el inicio de sesión actual no guardado y vincula la aprobación a esos bytes exactos. Un inicio de sesión activo distinto no puede reutilizar una confirmación anterior. Durante la restauración, una credencial de recuperación exclusiva de la transacción y cifrada con DPAPI conserva los bytes anteriores hasta confirmar o revertir la operación.

Cada transacción compuesta o de varios archivos guarda la instantánea gestionada actual exacta en el bloque de recuperación cifrado antes del diario, incluso si coincide con el origen guardado. Así puede revertir una escritura parcial y actualizar con seguridad un perfil antiguo que solo contenía credenciales. Tras una interrupción, la recuperación prefiere esa instantánea previa al cambio y sincroniza el perfil de origen si la restaura. Los diarios antiguos sin bloque siguen recurriendo al origen guardado. Si el estado activo no coincide con el origen conservado ni con el destino, el diario y el bloque cifrado permanecen para la recuperación manual.

La confirmación de varios archivos es de cierre seguro: primero elimina la autenticación separada, después fusiona la configuración de forma atómica y, al final, instala la autenticación de destino. Una interrupción puede dejar la autenticación ausente, pero nunca combina credenciales con el endpoint del perfil opuesto; la recuperación termina o revierte desde la instantánea cifrada previa.

Los perfiles antiguos sin formato compuesto de Codex y Claude Code se interpretan como credenciales más una configuración API gestionada vacía. Al activarlos se borran los campos gestionados de ruta API y modelo para no reutilizar un endpoint externo del perfil anterior. Después, configure el modelo/API deseado y vuelva a capturar el perfil. Un perfil antiguo que era el origen activo se actualiza al formato compuesto al abandonarlo.

La aplicación no garantiza que una sesión dure indefinidamente. Una revocación del proveedor, una política de la organización, SSO, MFA o el vencimiento del token aún pueden requerir un inicio de sesión normal en el cliente oficial.

Cambiar el nombre o eliminar un perfil solo afecta al almacén local de instantáneas cifradas. El cambio de nombre modifica únicamente la etiqueta y los metadatos guardados, no el contenido de la instantánea; la eliminación solo quita la instantánea local cifrada seleccionada. Eliminar el perfil seleccionado por última vez también borra su asociación activa en la aplicación, pero no cierra la sesión ni modifica los archivos activos de autenticación o configuración del proveedor. Guarde la cuenta actual antes de volver a cambiar. Ambas acciones se rechazan mientras una transacción de cambio interrumpida esté pendiente de recuperación.

## Ajustes

Abra **Ajustes** desde la ventana de la aplicación para elegir el idioma o controlar si la aplicación se inicia con Windows. El idioma se guarda localmente para el usuario actual de Windows y puede cambiarse en cualquier momento.

**Iniciar con Windows** añade una entrada para esta aplicación en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Solo afecta al usuario actual de Windows y no requiere privilegios de administrador. Al desactivarlo se elimina únicamente la entrada de inicio perteneciente a Coding Agent Account Switcher, sin alterar otras aplicaciones de inicio.

**Buscar actualizaciones** es una acción totalmente iniciada por el usuario. Solo después de pulsarla, la aplicación envía una única solicitud HTTPS `GET` anónima a la API oficial de GitHub de este repositorio. No hay comprobaciones al iniciar, en segundo plano ni periódicas, y la solicitud no carga credenciales, ajustes, nombres de perfiles, identificadores del equipo ni telemetría. La aplicación solo compara metadatos de la versión; nunca descarga ni ejecuta automáticamente un instalador o una compilación portátil.

## Modelo de seguridad

- Los perfiles cifrados se almacenan bajo `%LOCALAPPDATA%\CodingAgentAccountSwitcher`.
- Cada conjunto normalizado de archivos gestionados tiene un almacén, estado, diario y mutex independientes. Cambiar `CODEX_HOME`, `CLAUDE_CONFIG_DIR` u `OPENCODE_CONFIG` inicia otro conjunto de perfiles.
- DPAPI `CurrentUser` impide que otra cuenta de Windows descifre directamente el perfil, pero no protege frente a software malicioso que ya se ejecute como el mismo usuario.
- Aparte del archivo activo normal del proveedor, los bytes descifrados solo existen brevemente en memoria y en la sustitución atómica del mismo directorio durante la captura o el cambio.
- Los archivos temporales y de copia de seguridad de la sustitución usan el ID de la transacción de recuperación. Al terminar normalmente se eliminan ambos archivos exactos. Tras una interrupción, la recuperación restaura un archivo activo ausente desde la instantánea de origen cifrada o la credencial de recuperación exclusiva de la transacción, elimina todos los archivos de preparación que pertenecen exactamente a esa transacción y luego borra el diario. El bloque de recuperación cifrado se elimina únicamente después del diario y como una operación de mejor esfuerzo.
- La aplicación no carga credenciales, que nunca deben incluirse en registros, incidencias, informes de fallos, datos de prueba ni commits del repositorio.
- La detección de procesos es defensiva y de mejor esfuerzo. No abra Codex, Claude Code ni OpenCode hasta que termine la operación.
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
4. Prepara el ejecutable portátil y las sumas SHA-256 de ambos ejecutables.
5. Elimina únicamente la versión y la etiqueta anteriores llamadas `latest`.
6. Publica una nueva versión `latest` con el instalador, el ejecutable portátil y las sumas para el commit actual. Un único valor `APP_VERSION` del flujo asigna la versión del ejecutable y escribe este marcador exacto legible por máquina en las notas: `<!-- coding-agent-account-switcher-version: 0.1.N -->`.

Como la etiqueta `latest` es continua, la aplicación lee ese marcador de la respuesta de la API oficial de GitHub Releases únicamente cuando el usuario busca actualizaciones. No realiza comprobaciones en segundo plano ni descarga o ejecuta automáticamente ningún archivo publicado.

El flujo nunca elimina versiones numeradas. La opción **immutable releases** de GitHub debe estar desactivada para la etiqueta continua `latest`, y las reglas de ramas o etiquetas deben permitir que el flujo elimine `latest`. Los repositorios que exijan versiones inmutables deben usar etiquetas de compilación únicas.

El instalador y el ejecutable portátil continuos no están firmados actualmente, por lo que Windows SmartScreen puede mostrar una advertencia de reputación. Revise el código fuente y verifique la suma SHA-256 publicada correspondiente antes de ejecutar cualquiera de los ejecutables.

## Contribuir

Las contribuciones son bienvenidas. Lea [CONTRIBUTING.md](CONTRIBUTING.md). Las pruebas deben usar credenciales y configuraciones sintéticas temporales; nunca deben acceder a archivos reales de Codex, Claude Code u OpenCode.

## Licencia

[MIT](LICENSE)

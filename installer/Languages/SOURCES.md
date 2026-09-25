# Installer language resources

These UTF-8 files are vendored from the official [Inno Setup source repository](https://github.com/jrsoftware/issrc). Building the installer never downloads translations and does not depend on the runner's installed language collection.

## Fixed upstream revisions

- Inno Setup 6.7.1 (`is-6_7_1`): commit `cfdf48923178df4b4f040e038b423aa555a61ffc`. Supplies English, Spanish, French, German, Japanese, Korean, Brazilian Portuguese, Russian, Arabic, Hindi, and the license.

The original upstream byte hashes below allow a future maintainer to reproduce the imports. They are not hashes of the locally modified files.

| Local file | Fixed upstream source | Original SHA-256 |
| --- | --- | --- |
| English.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Default.isl) | `42a5f6f7dbbddf26cc278f67db5d894235ce1d126a6856702e31bd02023a1316` |
| Spanish.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/Spanish.isl) | `c61ee6287d6a10db186cce43b556422e0a8bc631784132468a9cebca4f2df0d5` |
| French.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/French.isl) | `145bf4ea34eaa79c52e4fcb77ce3eee9720b0d424c69458cb99592bc52121fba` |
| German.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/German.isl) | `f016f4a957de8c59e8db2da788357b4c923cecadca6f0c6eab404fb28015cac9` |
| Japanese.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/Japanese.isl) | `d5450537bb128112347bf86a4bdc3a4be0605414df0bc4fc90f55aaef1ba369b` |
| Korean.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/Korean.isl) | `cb56d6ea5c082bcf6b4acc1ae4c303ba91f662b1d0bfe05e4d3902c38c41e02a` |
| BrazilianPortuguese.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/BrazilianPortuguese.isl) | `e7e5e3dbd0ddee4c5a1f502f3035e431a21f87e68cef9a0295680b63ed61200b` |
| Russian.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/Russian.isl) | `81981fe6cfef7f3b2f744beac1494f4d2b29cea69c96a08d1c0a6b79dc0ed8f2` |
| Arabic.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/Arabic.isl) | `d621121eba68640cc9cb3e1e63593d9b5905c15ab6d1dcfa168df603f52e0cad` |
| LICENSE.txt | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/license.txt) | `2e5346868c2a18434489824e11d65c3031620f792fefc415d05f19cd441abf5c` |
| Hindi.isl | [source](https://raw.githubusercontent.com/jrsoftware/issrc/cfdf48923178df4b4f040e038b423aa555a61ffc/Files/Languages/Unofficial/Hindi.islu) | `fbb1045f3b25842bb926bdd5400d07875f4c8572b04ffab14bb7add9882cc19b` |

## Local changes

All files are stored as UTF-8 without a BOM. Original translator attribution is retained. Apart from text-format normalization, these deliberate changes are marked in the affected files:

- English: display name changed to `English (United States)` to match the application.
- Japanese: display name written as UTF-8 `日本語`; the optional empty `HelpTextNote` is explicit.
- Brazilian Portuguese: display name changed to `Português (Brasil)`; repaired the upstream `% 1` typo in `OnlyOnTheseArchitectures` to `%1`.
- Hindi: retained the official repository's older contributed translation and its translator attribution; translated 67 missing 6.7.1 entries, removed seven obsolete message keys and obsolete font settings, and repaired three existing placeholder errors. The display name is `हिन्दी`, the Windows language ID is `$0439`, and Nirmala UI is requested for Devanagari text. These additions are project-maintained translations, not an unchanged upstream release.

All **10 supported languages** explicitly define all **281 standard messages and 12 custom messages** from Inno Setup 6.7.1. The packaging tests compare every key and every numbered/application placeholder with the English catalog, validate strict UTF-8, and check the names, language IDs and text direction against the application's `SupportedLanguages`. Paragraph breaks and accelerator letters can differ between languages.

## Selection behavior

The installer uses `ShowLanguageDialog=yes` and `UsePreviousLanguage=no`, so a normal interactive launch (including a reinstall) asks the user to confirm a language before entering the wizard. Windows' UI language supplies only the initial selection. This choice controls installer/uninstaller messages; it does not overwrite the application's saved language setting.

Inno Setup language identifiers permit underscores but not hyphens, so application codes such as `hi-IN` map to internal installer names such as `hi_IN`. Unattended callers may still use `/LANG=hi_IN`, `/SILENT`, or `/VERYSILENT`; the native dialog is suppressed for these documented modes, preserving the CI smoke test.

References: [ShowLanguageDialog](https://jrsoftware.org/ishelp/topic_setup_showlanguagedialog.htm), [UsePreviousLanguage](https://jrsoftware.org/ishelp/topic_setup_usepreviouslanguage.htm), [command-line parameters](https://jrsoftware.org/ishelp/topic_setupcmdline.htm), and [6.7.1 silent-dialog implementation](https://github.com/jrsoftware/issrc/blob/cfdf48923178df4b4f040e038b423aa555a61ffc/Projects/Src/Setup.MainFunc.pas#L3603-L3619).

## License and maintenance

The imported files are covered by the [Inno Setup License](LICENSE.txt). Keep that full license and each original translator's notices with redistributions of these sources. Modified translations must remain identified as modified. The distributed installer includes the project `THIRD-PARTY-NOTICES.md`, which reproduces the license.

When changing the supported language list or upgrading the compiler, update these pinned files deliberately, inspect upstream changes and permissions, preserve placeholder semantics, run `ReleasePackagingTests`, and compile the installer with all languages enabled. Do not replace these sources with build-time downloads from a moving branch.

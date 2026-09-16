# IdleAutoGame

Applicazione desktop Linux per l'automazione di giochi mobile di tipo idle tramite dispositivi Android (ADB) e intelligenza artificiale (LLM locale o remoto).

## Funzionalità Principali

- **Supporto Multi-Dispositivo**: Rilevamento automatico e controllo di dispositivi Android via ADB (USB e Wireless).
- **LLM Ibrido (Locale e Remoto)**:
  - **Motore Locale Nativo**: Esecuzione in-process ad alte prestazioni tramite `llama.cpp` e `LLamaSharp` (v0.27.0).
  - **Gestione Modelli Integrata**: Download chunked con resume, verifica di integrità SHA-256 e raccomandazione basata su RAM hardware (3 tier: 8GB, 16GB, 32GB+).
  - **Provider Remoto / Server**: Supporto per endpoint compatibili OpenAI e `llama-server`.
- **Policy di Sicurezza Vincolanti**: Protezione integrata contro l'uso di valute premium e microtransazioni (`Deny-by-Default`).
- **Activity Guard**: Monitoraggio del foreground Android in tempo reale con difesa contro race condition.
- **Interfaccia Grafica Linux**: Desktop GUI nativa sviluppata con Avalonia UI 11.3 e .NET 10.

## Struttura della Documentazione

La documentazione di progetto è la *source of truth* delle specifiche funzionali, architetturali e tecniche.

* `docs/PROJECT-CONTEXT.md`: Contesto generale, vision, stack tecnologico e onboarding.
* `docs/product/`: Specifiche di prodotto (requisiti, user flows, schermate, impostazioni, error states).
* `docs/architecture/`: Specifiche architetturali, state machine, prompt hierarchy, tracciabilità e roadmap.
* `docs/adr/`: Architecture Decision Records (`ADR-001` - `ADR-009`).
  - `ADR-009`: Motore LLM Locale con llama.cpp e LLamaSharp.
* `docs/llm/`: Guide dedicate al sottosistema LLM:
  - `docs/llm/local-llm-guide.md`: Architettura, ciclo di vita e runtime settings di LLamaSharp.
  - `docs/llm/model-management-guide.md`: Catalogo modelli GGUF, tier RAM e download manager.
* `docs/changelog.md`: Storico completo delle versioni e delle modifiche.

# Build e sviluppo

Il progetto include una suite completa di script cross-platform per build, esecuzione, testing, diagnostica e pubblicazione, sia per **Linux** (Bash `.sh`) che per **Windows** (Windows CMD `.cmd` nativo - senza dipendenze da PowerShell).

## Prerequisiti

### Linux
- **OS Supportati**: Linux x64 (Fedora/Nobara, Ubuntu/Debian, Arch Linux o qualsiasi distribuzione moderna con `glibc` $\ge 2.34$).
- **.NET SDK**: .NET 10.0 SDK (`10.0.100` o superiore, vedi `global.json`).
- **Architettura**: `x86_64` (`linux-x64`).
- **Dipendenze Native / Sistema**:
  - `android-tools` / `adb` per il collegamento ai dispositivi Android.
  - Librerie X11/Wayland e font di sistema (es. Fontconfig, FreeType).
  - Shell standard Bash (`/usr/bin/env bash`).

### Windows
- **OS Supportati**: Windows 10 (1809+) o Windows 11 a 64 bit (`win-x64`).
- **.NET SDK**: .NET 10.0 SDK (`10.0.100` o superiore).
- **Architettura**: `x64` (`win-x64`).
- **Ambiente Shell**: Prompt dei comandi standard (`cmd.exe`). Non è richiesto né utilizzato PowerShell.
- **Dipendenze Esterne**: `adb.exe` (Android Platform Tools) aggiunto al `%PATH%` per il rilevamento dei dispositivi.

---

## Comandi di Sviluppo e Automazione

Tutti gli script determinano dinamicamente la root del repository e possono essere eseguiti sia dalla root sia dalla cartella `scripts/`.

### Tabella di Corrispondenza

| Operazione | Linux (Bash) | Windows (CMD) | Descrizione |
|---|---|---|---|
| **Verifica Ambiente** | `./scripts/doctor.sh` | `scripts\doctor.cmd` | Controlla OS, .NET SDK 10, progetti, NuGet e permessi. |
| **Clean Build** | `./scripts/build.sh` | `scripts\build.cmd` | Pulisce `bin/obj`, esegue `dotnet restore` e compila in `Release`. |
| **Build + Run** | `./scripts/run.sh` | `scripts\run.cmd` | Compila ed esegue l'interfaccia grafica Avalonia. |
| **Test Suite** | `./scripts/test.sh` | `scripts\test.cmd` | Esegue la suite completa di unit test (145 test). |
| **Publish** | `./scripts/publish.sh` | `scripts\publish.cmd` | Pubblica l'eseguibile in `artifacts/publish/<RID>/`. |
| **Run Published** | `./scripts/run-published.sh` | `scripts\run-published.cmd` | Avvia il binario pubblicato precedentemente. |
| **Clean** | `./scripts/clean.sh` | `scripts\clean.cmd` | Rimuove tutti i file temporanei `bin`, `obj`, `artifacts` e `dist`. |

---

### Esempi di Utilizzo Linux (Bash)

```bash
# Diagnostica preliminare dell'ambiente
./scripts/doctor.sh

# Pulizia completa dell'albero di build
./scripts/clean.sh

# Compilazione pulita in configurazione Release
./scripts/build.sh

# Esecuzione della suite di test unitari
./scripts/test.sh

# Compilazione ed esecuzione dell'applicazione
./scripts/run.sh

# Pubblicazione in formato Framework-Dependent (predefinito) per linux-x64
./scripts/publish.sh

# Pubblicazione opzionale Self-Contained
./scripts/publish.sh -r linux-x64 --self-contained true

# Esecuzione del binario standalone pubblicato
./scripts/run-published.sh
```

---

### Esempi di Utilizzo Windows CMD (Senza PowerShell)

Da un prompt dei comandi standard (`cmd.exe`):

```cmd
:: Diagnostica preliminare dell'ambiente
scripts\doctor.cmd

:: Pulizia completa dell'albero di build
scripts\clean.cmd

:: Compilazione pulita in configurazione Release
scripts\build.cmd

:: Esecuzione della suite di test unitari
scripts\test.cmd

:: Compilazione ed esecuzione dell'applicazione
scripts\run.cmd

:: Pubblicazione in formato Framework-Dependent (predefinito) per win-x64
scripts\publish.cmd

:: Pubblicazione opzionale Self-Contained
scripts\publish.cmd -r win-x64 --self-contained true

:: Esecuzione del binario pubblicato
scripts\run-published.cmd
```

---

## Guida al Troubleshooting

### 1. .NET SDK mancante o non trovato in PATH
- **Sintomo**: `[ERROR] .NET CLI (dotnet) was not found in PATH.` o `BUILD FAILED: Required .NET SDK not found.`
- **Causa**: L'SDK .NET non è installato o la directory dell'eseguibile `dotnet` non è presente nella variabile d'ambiente `PATH`.
- **Risoluzione**:
  - **Linux**: Installare .NET 10 tramite il gestore pacchetti (es. `sudo dnf install dotnet-sdk-10.0` o `sudo apt-get install dotnet-sdk-10.0`) oppure scaricarlo da https://dotnet.microsoft.com/download/dotnet/10.0. Verificare che `/usr/bin/dotnet` o `~/.dotnet` sia presente in `PATH`.
  - **Windows**: Installare l'installer .NET 10 SDK da https://dotnet.microsoft.com/download/dotnet/10.0 e riavviare la finestra CMD per ricaricare la variabile `%PATH%`.

### 2. Versione .NET SDK errata (< 10.0)
- **Sintomo**: `[ERROR] Required .NET SDK (v10.x) was not found.`
- **Causa**: È presente una versione precedente di .NET (es. .NET 8 o .NET 9), ma il progetto richiede .NET 10 come specificato in `global.json` e nei `.csproj`.
- **Risoluzione**: Eseguire `dotnet --list-sdks` per verificare le versioni installate. Installare .NET 10 SDK per affiancarlo alle versioni esistenti.

### 3. Errore di NuGet Restore / Connessione di Rete
- **Sintomo**: `BUILD FAILED: NuGet Restore Error` o `Unable to load the service index for source https://api.nuget.org/v3/index.json`.
- **Causa**: Assenza di connessione internet, proxy aziendale non configurato o feed nuget.org temporaneamente irraggiungibile.
- **Risoluzione**:
  - Verificare la connettività con `curl -I https://api.nuget.org/v3/index.json` o `dotnet nuget list source`.
  - Se si utilizza un proxy, configurare le variabili d'ambiente standard `HTTP_PROXY` e `HTTPS_PROXY`.

### 4. Permessi Negati su Linux (`Permission denied`)
- **Sintomo**: `bash: ./scripts/build.sh: Permission denied`
- **Causa**: I permessi di esecuzione POSIX non sono impostati sui file `.sh`.
- **Risoluzione**: Assegnare i permessi di esecuzione tramite `chmod +x scripts/*.sh`.

### 5. Binario Pubblicato non Trovato
- **Sintomo**: `[ERROR] Published application not found. Run ./scripts/publish.sh first.`
- **Causa**: È stato invocato `run-published.sh` / `run-published.cmd` prima che la pubblicazione fosse completata con successo o per un RID differente.
- **Risoluzione**: Eseguire prima `./scripts/publish.sh` (Linux) o `scripts\publish.cmd` (Windows) e assicurarsi che termini con `[SUCCESS]`.

### 6. Modalità Framework-Dependent vs Self-Contained
- **Predefinito**: La configurazione predefinita è **Framework-Dependent** (`--self-contained false`), che genera pacchetti leggeri riutilizzando il runtime .NET 10 installato sull'host.
- **Distribuzione Standalone**: Per distribuire l'applicazione su macchine dove .NET 10 non è installato, aggiungere il flag `--self-contained true` allo script di publish (`./scripts/publish.sh --self-contained true` o `scripts\publish.cmd --self-contained true`).

### 7. Architettura non Supportata
- **Sintomo**: Errori di compilazione con `LLamaSharp.Backend.Cpu` o mancate librerie `libllama.so` / `llama.dll`.
- **Causa**: I binari nativi inclusi sono compilati per architetture `x86_64` (`linux-x64` e `win-x64`).
- **Risoluzione**: Assicurarsi di operare su CPU a 64 bit x86_64 con supporto AVX/AVX2. Su architetture ARM64 (es. Raspberry Pi o Apple Silicon), compilare i backend nativi `llama.cpp` localmente.

## Regole di Manutenzione della Documentazione

1. La documentazione deve rimanere coerente con l'evoluzione del progetto.
2. Le modifiche ai requisiti devono aggiornare i Requirement ID e il `docs/changelog.md`.
3. I documenti obsoleti devono essere esplicitamente marcati (es. `status: deprecated`).
4. I nuovi agenti/sviluppatori devono poter comprendere il progetto leggendo questa documentazione senza bisogno dello storico della chat.


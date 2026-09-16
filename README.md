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
| **Test Suite** | `./scripts/test.sh` | `scripts\test.cmd` | Esegue la suite completa di test (174 test). |
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

## Guida Rapida Utente (Getting Started)

Questa guida illustra il percorso completo per un nuovo utente: dal primo avvio alla configurazione e all'esecuzione autonoma del gameplay.

### 1. Primo Avvio e Preflight
1. Avviare l'applicazione tramite `./scripts/run.sh` (oppure `./scripts/run-published.sh` se si utilizza il pacchetto pubblicato).
2. All'apertura viene mostrata la schermata di **Splash & Preflight**: l'applicazione rileva automaticamente CPU, RAM di sistema e VRAM GPU disponibile.
3. Premere **"Continue to Dashboard"** (o navigare tramite la barra superiore).

### 2. Configurazione LLM (Locale o Remoto)
Accedere alla scheda **AI Models**:
- **Motore Locale (GGUF / llama.cpp in-process)**:
  - L'applicazione raccomanda automaticamente il modello più adatto al quantitativo di RAM rilevato (Tier: 8GB, 16GB, 32GB+).
  - Se il modello non è presente, premere **"Download"**: il download manager gestisce resume, chunked transfer e verifica crittografica dell'hash SHA-256.
  - Premere **"Use Model"** per attivarlo.
- **Provider Remoto (Server HTTP / OpenAI compatible)**:
  - Disattivare il toggle "Usa Motore Locale".
  - Inserire l'URL dell'endpoint (es. `http://localhost:8080` per `llama-server` o `https://api.openai.com/v1`) e l'eventuale API Key.
  - Selezionare il modello desiderato e salvare.

### 3. Connessione Dispositivo Android (ADB)
Accedere alla scheda **Devices**:
- **Connessione USB**:
  1. Abilitare le *Opzioni Sviluppatore* e il *Debug USB* sullo smartphone Android.
  2. Collegare il dispositivo via cavo USB.
  3. Premere **"Refresh Devices"** e confermare il prompt di autorizzazione RSA sullo schermo del dispositivo se richiesto.
  4. Selezionare il dispositivo e premere **"Verify Device"** per confermare la corretta acquisizione dei frame dello schermo.
- **Connessione Wireless**:
  1. Connettere il dispositivo alla stessa rete Wi-Fi del computer.
  2. Inserire indirizzo IP e porta (es. `192.168.1.145:5555`) e codice di pairing se necessario.
  3. Premere **"Connect Wireless"**.

### 4. Selezione Gioco e Policy di Sicurezza
Accedere alla scheda **Games**:
- Selezionare il profilo di gioco (es. **Tap Titans 2**).
- Verificare i vincoli di sicurezza: per impostazione predefinita (`Deny-by-Default`), l'uso di valute premium e gli acquisti in-app con denaro reale sono disabilitati.

### 5. Controllo del Gameplay Autonomo
Accedere alla scheda **Dashboard**:
- Premere **"Start"**: l'agent avvia il ciclo autonomo di osservazione (cattura frame ADB $\rightarrow$ inferenza LLM con output strutturato $\rightarrow$ validazione policy $\rightarrow$ esecuzione tocco/swipe).
- **Controlli Runtime**:
  - **Pause**: Sospende temporaneamente il ciclo di esecuzione senza resettare i contatori di sessione.
  - **Resume**: Riprende l'esecuzione del gameplay dal punto in cui era stato sospeso.
  - **STOP (Priorità Assoluta)**: Arresta immediatamente qualsiasi operazione entro 1 secondo.
  - **Override Istruzioni**: È possibile inviare comandi testuali aggiuntivi all'agente in tempo reale.

### 6. Interpretazione degli Stati e Activity Guard
- **Idle**: Applicazione in attesa, nessun ciclo attivo.
- **Executing**: Ciclo di automazione in esecuzione normale.
- **Paused**: Automazione sospesa dall'utente o da una policy.
- **ActivityLost**: L'Activity Guard ha rilevato che il gioco non è più in primo piano (es. apertura accidentale del Google Play Store). L'automazione viene immediatamente interrotta a protezione dell'utente. Riportando il gioco in primo piano e premendo *Resume*, l'agente riprende regolarmente.
- **Stopped**: Automazione arrestata.

### 7. Impostazioni Globali e Persistenza
Nella scheda **Settings** è possibile personalizzare l'intervallo tra screenshot, i timeout ADB, le policy di log e l'aspetto dell'interfaccia. Tutte le modifiche vengono salvate in `settings.json` e ripristinate automaticamente al riavvio dell'applicazione.

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


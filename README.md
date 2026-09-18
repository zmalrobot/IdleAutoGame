# IdleAutoGame

> [!WARNING]
> ### Progetto Sperimentale Sviluppato Tramite LLM / Experimental LLM-Generated Software
> 
> Questo repository e il codice sorgente dell'applicazione sono stati generati e sviluppati prevalentemente tramite modelli linguistici di grandi dimensioni (**LLM**) all'interno di un'attività di ricerca e sperimentazione mirata a valutare le capacità dell'IA nello sviluppo e nella manutenzione di software desktop complesso.
> 
> - **Natura Sperimentale**: Il progetto è un prototipo di ricerca; sebbene coperto da oltre 300 test automatizzati e verifiche visive, potrebbe contenere bug, difetti architetturali, anomalie di runtime o comportamenti imprevisti.
> - **Non per Uso in Produzione**: Il software non è inteso né certificato per ambienti di produzione o mission-critical.
> - **Uso a Proprio Rischio**: Chiunque decida di clonare, compilare o eseguire questo software, o di collegarlo a dispositivi Android fisici, lo fa a proprio ed esclusivo rischio.

---

Applicazione desktop cross-platform (Linux e Windows) per l'automazione intelligente di giochi mobile idle tramite dispositivi Android (ADB) e modelli multimodali di intelligenza artificiale (LLM locali GGUF o endpoint remoti compatibili).

---

## Indice

- [Caratteristiche Principali](#caratteristiche-principali)
- [Architettura del Progetto](#architettura-del-progetto)
- [Releases e Distribuzione](#releases-e-distribuzione)
- [Sviluppo e Prerequisiti](#sviluppo-e-prerequisiti)
- [Comandi di Automazione e Script](#comandi-di-automazione-e-script)
- [Guida Rapida (Getting Started)](#guida-rapida-getting-started)
- [Sicurezza e Policy di Gioco](#sicurezza-e-policy-di-gioco)
- [Risoluzione Problemi (Troubleshooting)](#risoluzione-problemi-troubleshooting)
- [Licenza e Disclaimer](#licenza-e-disclaimer)

---

## Caratteristiche Principali

- **Supporto Dispositivi Android via ADB**: Rilevamento automatico e controllo completo di telefoni ed emulatori sia via cavo USB che wireless (Wi-Fi ADB), con sincronizzazione binaria affidabile per la cattura dello schermo e iniezione gesti (tap, double-tap, swipe, long-press, keyevents).
- **LLM Ibrido (Visione Multimodale)**:
  - **Motore Locale In-Process**: Inferenza nativa ad alte prestazioni tramite `LLamaSharp` (v0.27.0) e `llama.cpp` con runtime fallback compatibile x86-64-v2.
  - **Model Manager & Catalogo GGUF**: Catalogo integrato con verifica SHA-256 crittografica, download chunked resumabile e raccomandazione automatica in base alla RAM (Tier 8GB, 16GB, 32GB+). I modelli vengono salvati localmente sul filesystem e **non sono inclusi nella repository Git**.
  - **Provider Remoti**: Connessione ad endpoint HTTP compatibili OpenAI e `llama-server`.
- **Raw LLM Streaming in Tempo Reale**: Visualizzazione live dei token generati in streaming (token-per-token per il provider locale, SSE per provider remoti) con telemetria del throughput (tokens/sec) e latenza.
- **Execution Lock Globale dell'Applicazione**: Macchina a stati autoritativa a livello di Application Layer (`IExecutionStateGuard`). Quando l'automazione è in esecuzione (`Running` o `Paused`), la Dashboard resta pienamente utilizzabile mentre la navigazione e la modifica di parametri critici (dispositivo, modello, policy, gioco) vengono categoricamente bloccate (`Pause ≠ Unlock`).
- **Ultimo Screenshot in Tempo Reale (Zero Cronologia)**: Pannello diagnostico dedicato con l'ultimo frame live acquisito dal dispositivo, zoom 1:1, salvataggio su richiesta utente e gestione rigorosa della memoria (smaltimento tempestivo delle bitmap unmanaged Skia via `Dispose()`).
- **Policy di Sicurezza & IAP Protection**: Meccanismo `Deny-by-Default` che impedisce categoricamente il tocco di aree shop, acquisti in-app e consumo di valute premium.
- **Activity Guard**: Monitoraggio del foreground Android in tempo reale; se l'utente o un evento apre un'app diversa dal gioco, l'agente sospende istantaneamente il ciclo mettendosi in stato `ActivityLost`.
- **Interfaccia Grafica Desktop**: GUI fluida e responsiva sviluppata con Avalonia UI 11.2 e tema Fluent, verificata a varie risoluzioni (900×600, 1100×700, 1400×900).

---

## Architettura del Progetto

Il progetto adotta i principi della **Clean Architecture**, suddiviso in 7 progetti modulari:

```text
IdleAutoGame/
├── src/
│   ├── IdleAutoGame.Core/               # Entità, modelli di dominio, interfacce, enums
│   ├── IdleAutoGame.Application/        # Engine di automazione, guardie di stato, validatori, servizi
│   ├── IdleAutoGame.Infrastructure.Adb/ # Controller ADB (AdvancedSharpAdbClient, sync service)
│   ├── IdleAutoGame.Infrastructure.Llm/ # Provider LLM (LLamaSharp, SSE client, model manager)
│   ├── IdleAutoGame.Infrastructure.Persistence/ # Repository SQLite/JSON per sessioni e impostazioni
│   ├── IdleAutoGame.Games.TapTitans2/   # Definizione specifica del gioco Tap Titans 2
│   └── IdleAutoGame.Presentation/       # Avalonia MVVM Desktop UI, Views e ViewModels
├── tests/
│   └── IdleAutoGame.Tests.Unit/         # Suite di test unitari (316 test, 100% pass)
├── scripts/                             # Script cross-platform di automazione (.sh e .cmd)
└── .github/workflows/                   # GitHub Actions (release.yml manuale)
```

---

## Releases e Distribuzione

Le release di IdleAutoGame vengono compilate e pubblicate tramite **GitHub Actions** con dispatch manuale (`workflow_dispatch`).

### Piattaforme Supportate

| Piattaforma | Runtime Identifier (RID) | Pacchetto Generato | Note |
| :--- | :--- | :--- | :--- |
| **Linux x64** | `linux-x64` | `IdleAutoGame-<version>-linux-x64.tar.gz` | Testato su distribuzioni moderne con glibc $\ge 2.34$. Include runtime nativo fallback llama.cpp. |
| **Windows x64** | `win-x64` | `IdleAutoGame-<version>-win-x64.zip` | Testato su Windows 10/11 a 64 bit. |
| **Linux ARM64** | `linux-arm64` | `IdleAutoGame-<version>-linux-arm64.tar.gz` | Cross-compilato con .NET 10 SDK. |

### Come Installare ed Eseguire un Pacchetto di Release

1. Scaricare l'archivio corrispondente al proprio sistema operativo dalla sezione **Releases** del repository GitHub.
2. Estrarre il pacchetto:
   - **Linux**: `tar -xzf IdleAutoGame-1.0.0-linux-x64.tar.gz`
   - **Windows**: Estrarre il file `.zip` in una cartella a scelta.
3. **Prerequisiti di Esecuzione**:
   - **.NET 10 Runtime**: Installare il runtime .NET 10 se si usa la versione predefinita (Framework-Dependent).
   - **ADB (Android Debug Bridge)**: Verificare che il comando `adb` (Linux) o `adb.exe` (Windows) sia installato e accessibile nel `PATH`.
4. Avviare l'eseguibile:
   - **Linux**: `./IdleAutoGame.Presentation` (o `./scripts/run-published.sh`)
   - **Windows**: `IdleAutoGame.Presentation.exe` (o `scripts\run-published.cmd`)

---

## Sviluppo e Prerequisiti

### Requisiti Comuni
- **.NET SDK**: versione 10.0 (`10.0.100` o successiva, come definito in `global.json`).
- **ADB**: Android Platform Tools per la comunicazione con dispositivi o emulatori Android.

### Linux
- Distribuzione a 64 bit (x86_64).
- Librerie di sistema standard per font e grafica (X11 / Wayland, FreeType, Fontconfig).
- Shell Bash (`/usr/bin/env bash`).

### Windows
- Windows 10 (1809+) o Windows 11 x64.
- Prompt dei comandi standard (`cmd.exe`). **Non è richiesto né utilizzato PowerShell per gli script utente del progetto.**

---

## Comandi di Automazione e Script

Il repository fornisce una suite completa di script nativi sia per Linux (`.sh`) che per Windows CMD (`.cmd`). Tutti gli script individuano automaticamente la radice del repository e possono essere invocati da qualsiasi directory.

| Obiettivo | Linux (Bash) | Windows (CMD) | Descrizione |
| :--- | :--- | :--- | :--- |
| **Diagnostica** | `./scripts/doctor.sh` | `scripts\doctor.cmd` | Controlla .NET 10 SDK, permessi disco, progetti e dipendenze. |
| **Clean Build** | `./scripts/build.sh` | `scripts\build.cmd` | Pulisce, esegue restore e compila la soluzione in configurazione Release. |
| **Build & Run** | `./scripts/run.sh` | `scripts\run.cmd` | Compila ed esegue la GUI Desktop Avalonia. |
| **Test Suite** | `./scripts/test.sh` | `scripts\test.cmd` | Esegue la suite completa dei 316 test unitari. |
| **Pubblicazione** | `./scripts/publish.sh` | `scripts\publish.cmd` | Pubblica l'applicazione in `artifacts/publish/<RID>/`. |
| **Esecuzione Pubblicata** | `./scripts/run-published.sh` | `scripts\run-published.cmd` | Avvia il binario pubblicato standalone. |
| **Pulizia** | `./scripts/clean.sh` | `scripts\clean.cmd` | Rimuove `bin/`, `obj/`, `artifacts/` e `dist/`. |

---

## Guida Rapida (Getting Started)

1. **Avvio e Preflight**:
   All'apertura dell'applicazione (`./scripts/run.sh`), la schermata di preflight verifica l'hardware host (CPU, RAM totale e VRAM stimata).
2. **Configurazione Modello AI**:
   - *Locale*: Nella scheda **AI Models**, consultare i modelli GGUF raccomandati per la propria dotazione di RAM. Premere **Download** per avviare lo scaricamento con verifica di integrità SHA-256.
   - *Remoto*: Disattivare il toggle "Usa Motore Locale", inserire l'endpoint HTTP OpenAI-compatible e attivare il modello prescelto.
3. **Collegamento Dispositivo Android**:
   - Abilitare il *Debug USB* nelle Opzioni Sviluppatore del telefono.
   - Nella scheda **Devices**, premere **Refresh Devices** e confermare l'autorizzazione sul telefono.
   - Premere **Verify Device** per controllare la risoluzione e la cattura screenshot.
4. **Selezione Gioco**:
   - Nella scheda **Games**, selezionare il profilo di gioco supportato (es. *Tap Titans 2*). Le policy di sicurezza inibiscono di default acquisti e valute premium.
5. **Dashboard e Avvio Automazione**:
   - Nella scheda **Dashboard**, premere **Start Automation**: l'Execution Lock viene acquisito e le schede di configurazione vengono protette.
   - La finestra di **Dettaglio Esecuzione in Tempo Reale** consente di visualizzare lo streaming dei token LLM e l'ultimo screenshot acquisito dal dispositivo con zero accumulo di cronologia.

---

## Sicurezza e Policy di Gioco

- **Nessuna Credenziale Salvata nel Repository**: Tutte le API key o configurazioni utente rimangono esclusivamente nei file locali non tracciati da Git (`.env`, `settings.json`).
- **Isolamento Output LLM**: Il testo generato dal modello in streaming viene considerato non fidato e non produce azioni fisiche finché non viene validato sintatticamente (`LlmResponseParser`) e semanticamente (`ActionValidator`, `GamePolicyService`).
- **Activity Guard e Arresto di Emergenza**: Qualsiasi transizione fuori dall'app di gioco arresta o sospende il ciclo in meno di 1 secondo, prevenendo azioni indesiderate sul sistema operativo del dispositivo.

---

## Risoluzione Problemi (Troubleshooting)

1. **.NET SDK 10 Non Trovato**:
   Verificare l'installazione di .NET 10 con `dotnet --list-sdks`. Assicurarsi che la versione `10.0.x` sia presente e che il comando `dotnet` sia incluso nel proprio `PATH`.
2. **Dispositivo Non Rilevato da ADB**:
   Eseguire `adb devices` nel terminale e verificare che il dispositivo compaia nello stato `device` (e non `unauthorized` o `offline`). Accettare la richiesta di debug RSA sullo schermo dello smartphone.
3. **Cattura Schermo Fallita su Android Moderno**:
   Su alcune versioni di Android con SELinux restrittivo su `/dev/graphics/fb0`, l'applicazione effettua automaticamente il fallback sulla cattura ad alta velocità con salvataggio temporaneo in `/data/local/tmp/` e prelievo via socket `SyncService`.

---

## Licenza e Disclaimer

Questo software è distribuito a solo scopo educativo, di studio e di ricerca sulle capacità dei modelli linguistici nello sviluppo software. 

L'autore e i contributori non si assumono alcuna responsabilità per eventuali danni a dispositivi, account di gioco, ban o perdite di dati derivanti dall'utilizzo diretto o indiretto di questa applicazione.

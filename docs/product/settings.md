---
title: Settings Configuration
status: approved
version: 1.0
date: 2026-09-16
---

# Settings / Configuration

## Principio di Configurabilità
Tutte le opzioni che modificano il comportamento dell'applicazione e che hanno senso per l'utente devono essere gestite centralmente in una pagina "Impostazioni".
**Regola Fondamentale**: Nessun parametro critico (timeout, intervalli, device default) deve essere hardcoded nel codice o nascosto in file che l'utente deve editare a mano, salvo documentate eccezioni avanzate.

## Tracciabilità Impostazioni (Settings Registry)

Di seguito l'elenco delle configurazioni che devono essere esposte all'utente, con i rispettivi identificatori per la fase di sviluppo tecnico.

### Categoria: Generali
* `SETTING-GEN-001` - **Lingua Interfaccia**: (Default: System Locale).
* `SETTING-GEN-002` - **Tema UI**: Preferenza visiva dell'app desktop.

### Categoria: AI / LLM
* `SETTING-LLM-001` - **Modello Selezionato**: Il modello corrente (es. Gemini Pro, Llama 3).
* `SETTING-LLM-002` - **Provider LLM**: Se usare LLM locali o API cloud.
* `SETTING-LLM-003` - **Timeout Risposta**: Tempo massimo di attesa prima che l'app consideri fallita la richiesta al modello (Default: 30s).
* `SETTING-LLM-004` - **Max Tentativi (Retries)**: Numero di tentativi automatici in caso di JSON malformato o errore di rete (Default: 3).

### Categoria: Automazione
* `SETTING-AUT-001` - **Intervallo di Osservazione**: Quanti secondi attendere tra la fine di un'azione e l'acquisizione del nuovo screenshot (Default: 2.0s, previene l'invio comandi durante animazioni).
* `SETTING-AUT-002` - **Comportamento post-errore**: Cosa fare se si superano i tentativi massimi (Opzioni: Stop, Metti in Pausa, Ignora e prosegui). (Default: Metti in Pausa).
* `SETTING-AUT-003` - **Auto-reconnect ADB**: Tentare automaticamente il ripristino se il device si disconnette.

### Categoria: Dispositivi
* `SETTING-DEV-001` - **Dispositivo Predefinito**: ID del device che viene automaticamente selezionato all'avvio (se connesso).
* `SETTING-DEV-002` - **Preferenza ADB (USB vs Wireless)**: Se un device è raggiungibile su entrambi, quale preferire (Default: USB per latenza minore).

### Categoria: Giochi
* `SETTING-GAM-001` - **Gioco Predefinito**: Gioco selezionato all'avvio.
* `SETTING-GAM-002` - **Configurazioni Specifiche (Per-Gioco)**: Spazio per opzioni definite dal plugin del singolo gioco (es. "Usa Macro X", Prompt persistenti).

### Categoria: Logging e Diagnostica
* `SETTING-LOG-001` - **Livello di Log**: Quanto dettaglio mostrare/salvare (Info, Warning, Error, Debug).
* `SETTING-LOG-002` - **Salva Screenshot AI**: Opzione per salvare su disco un buffer degli ultimi N screenshot utilizzati dall'AI per review/debug (Default: False).
* `SETTING-LOG-003` - **Dimensione Cronologia UI**: Numero di azioni passate da mostrare nella schermata principale (Default: 50).

## Funzioni della Pagina Impostazioni
1. **Default**: Ogni impostazione ha un default ragionevole che garantisce il funzionamento base.
2. **Validazione**: Valori impossibili (es. intervallo = -5s) devono essere scartati con avviso visivo.
3. **Reset**: Possibilità di ripristinare ai default una singola impostazione, un'intera categoria o l'app intera.


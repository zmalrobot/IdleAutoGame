---
title: Product Requirements
status: approved
version: 1.0
date: 2026-09-16
---

# Product Requirements

## Product Goals
* Fornire un'interfaccia unificata per gestire l'automazione di giochi mobile.
* Astrarre la complessità tecnica di ADB e dei modelli LLM per l'utente finale.
* Creare un sistema estensibile che permetta di aggiungere con facilità nuovi giochi e provider LLM in futuro.

## User Goals
* Automatizzare il farming, grinding o la progressione in giochi idle.
* Mantenere il controllo di alto livello fornendo istruzioni testuali al sistema.
* Monitorare chiaramente lo stato dell'automazione, comprendendo perché l'AI compie determinate azioni.

## MVP (Minimum Viable Product)
* App desktop per Linux con interfaccia grafica.
* Discovery e connessione di dispositivi Android (USB e Wireless) pre-autorizzati.
* Selezione tra modelli LLM preconfigurati.
* Supporto per 1 gioco di base (es. *Tap Titans 2*).
* Ciclo di automazione: Screenshot -> Analisi LLM -> Azione (Tap/Swipe).
* Pagina "Impostazioni" centralizzata.
* Supporto per inserimento override testuale da parte dell'utente.
* Visualizzazione dello stato, screenshot corrente e logica decisionale.

## Future Scope (Post-MVP)
* Gestione automatizzata del pairing per ADB Wireless.
* Download, aggiornamento e gestione dei modelli LLM locali direttamente dall'interfaccia dell'app.
* Supporto a giochi non-idle più complessi (timing-sensitive).
* Dashboard avanzata per statistiche, esportazione dati di farming.
* Creazione di macro pre-registrate ibridate con l'AI.

## Out of Scope
* Supporto a piattaforme target diverse da Android (no iOS).
* Build ufficiali dell'app per Windows e macOS (target primario Linux).
* Training di modelli LLM custom.
* Alterazione del client di gioco, memory injection o bypass anti-cheat (il sistema opera strettamente come una "black-box" esterna).

## Requirement ID Registry

### Prodotti e Generali
* `PROD-001` (Esplicito): L'applicazione deve essere eseguibile su desktop Linux.
* `PROD-002` (Esplicito): Il sistema si basa sul ciclo concettuale: Observe-Analyze-Decide-Act.
* `PROD-003` (Esplicito): L'architettura deve supportare l'aggiunta di molteplici giochi.
* `PROD-004` (Esplicito): La qualità del codice (testabilità, coesione, modularità) è un requisito formale per consentire estensibilità.

### Selezione LLM
* `LLM-001` (Esplicito): L'utente deve poter selezionare il modello LLM.
* `LLM-002` (Esplicito): L'app deve suggerire modelli basandosi sulle risorse hardware e disabilitare quelli non supportati.
* `LLM-003` (Proposta): La schermata deve mostrare informazioni su consumo, velocità e qualità attesa per ogni modello.

### Dispositivi (Device Discovery)
* `DEVICE-USB-001` (Esplicito): Supporto connessione via ADB USB.
* `DEVICE-WIRELESS-001` (Esplicito): Supporto connessione via ADB Wireless.
* `DEVICE-DISCOVERY-001` (Esplicito): Elenco unificato dei dispositivi (USB e Wireless visualizzati assieme).
* `DEVICE-002` (Esplicito): Mostrare informazioni del dispositivo (Modello, Android version, risoluzione, stato).
* `DEVICE-003` (Esplicito): Il resto del workflow (post-selezione) deve funzionare allo stesso modo a prescindere dal tipo di connessione usata.

### Interfaccia e Automazione
* `UI-001` (Esplicito): Comandi chiari per Start, Pause, Resume, Stop. Lo stop deve interrompere immediatamente il ciclo.
* `UI-002` (Esplicito): La UI deve mostrare screenshot, azione corrente, risultato, ed una "spiegazione sintetica della decisione".
* `USER-001` (Esplicito): L'utente può inserire "User Overrides" testuali a runtime per alterare il comportamento dell'automazione.

### Configurazione
* `CONF-001` (Esplicito): Configurazione centralizzata gestibile tramite una singola pagina "Impostazioni".
* `CONF-002` (Esplicito): Nessun valore comportamentale critico deve essere hardcoded.


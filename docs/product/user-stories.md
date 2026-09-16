---
title: User Stories
status: approved
version: 1.0
date: 2026-09-16
---

# User Stories and Acceptance Criteria

## Epic 1: Avvio e Configurazione

**US01: Inizializzazione Trasparente**
*Come utente, voglio che l'app verifichi i requisiti tecnici allo splash screen, in modo da non avere sorprese durante l'uso.*
* **AC1**: Lo splash screen mostra una dicitura di caricamento per ogni fase (es. ADB, LLM).
* **AC2**: Se manca una dipendenza (es. ADB non trovato), il caricamento si ferma, mostrando chiaramente l'errore e come risolverlo.

**US02: Selezione Modello Bilanciata**
*Come utente, voglio poter scegliere il modello AI ma vedere subito quali modelli il mio PC può far girare, per evitare blocchi del sistema.*
* **AC1**: La lista propone 3 modelli per fasce di RAM.
* **AC2**: I modelli che richiedono più risorse del PC appaiono in grigio (disabilitati) con una nota del motivo (es. "RAM insufficiente").

**US03: Device Discovery Unificato**
*Come utente, voglio vedere il mio telefono connesso in USB e il tablet connesso via Wi-Fi nella stessa lista per poter scegliere.*
* **AC1**: L'interfaccia mostra contemporaneamente dispositivi USB e Wireless.
* **AC2**: Viene esplicitamente mostrata un'etichetta "USB" o "Wireless (IP)".
* **AC3**: Il workflow successivo funziona nello stesso modo a prescindere dal device scelto.

**US04: Autorizzazione Dispositivo**
*Come utente, se collego un nuovo telefono, voglio che l'app mi avvisi se devo confermare il collegamento sullo schermo del device.*
* **AC1**: Se ADB riporta lo stato `unauthorized`, l'app lo mostra chiaramente.
* **AC2**: Una nota avvisa l'utente di guardare lo schermo del telefono.

## Epic 2: Esecuzione e Automazione

**US05: Selezione e Cambio Gioco**
*Come utente, voglio selezionare il gioco da una lista, e se cambio gioco voglio che l'automazione riparta da zero.*
* **AC1**: La schermata di selezione elenca i giochi supportati.
* **AC2**: Cambiando gioco, la sessione di automazione viene resettata al suo stato iniziale.

**US06: Osservabilità delle Decisioni**
*Come utente, voglio guardare la dashboard e capire immediatamente cosa sta facendo il bot e perché.*
* **AC1**: La dashboard mostra lo screenshot più recente.
* **AC2**: Viene renderizzata a schermo una frase sintetica (es. "Upgrade effettuato per incrementare danno").
* **AC3**: Non viene mostrato codice grezzo, JSON, o il chain-of-thought interno del LLM come output primario.

**US07: User Override a Runtime**
*Come utente, voglio dire al bot "Non usare le gemme" senza dover fermare tutto e cercare nelle impostazioni.*
* **AC1**: Esiste un campo di testo visibile durante l'automazione.
* **AC2**: L'invio dell'input modifica il comportamento dal ciclo successivo.
* **AC3**: L'UI mostra chiaramente l'override attivo e permette di cancellarlo.
* **AC4**: Se la regola dell'utente entra in conflitto con una regola hardcoded del gioco o del sistema, vince il sistema salvaguardando il gioco, e l'UI avvisa l'utente ("Impossibile applicare: tasto non presente").

**US08: Gestione Errori e Stop**
*Come utente, se l'LLM va in timeout o il cavo USB si stacca, voglio che il bot si fermi in sicurezza avvisandomi.*
* **AC1**: Disconnessione device: lo stato passa a "Errore Device" e l'automazione si ferma (Pause).
* **AC2**: Timeout LLM: l'app esegue i tentativi (retry) configurati e poi va in "Errore Modello".
* **AC3**: Il tasto Stop riporta sempre e immediatamente l'app a uno stato inerte e di attesa.


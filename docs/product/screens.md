---
title: Screens and UI States
status: approved
version: 1.0
date: 2026-09-16
---

# Screens and UI States

## 0. Splash / Initialization
Scopo: Nascondere il caricamento tecnico (verifica ADB, dipendenze, rilevamento hardware).
* **Initial/Loading State**: Logo dell'app, progress bar o testo che descrive chiaramente cosa si sta inizializzando in linguaggio user-friendly.
* **Error State**: In caso di dipendenza mancante (es. ADB non installato), il progresso si ferma mostrando l'errore in modo chiaro e comprensibile, indicando in quale fase si è verificato.
* **Success State**: L'app transiziona automaticamente alla schermata successiva logica (Selezione Modello o Dashboard se tutto è già configurato).

## 1. Selezione del Modello LLM
Scopo: Permettere la scelta dell'AI che guiderà l'automazione.
* **Initial State**: Elenco dei modelli (3 suggerimenti per ogni fascia di RAM). Vengono mostrati: nome, requisiti, consumo previsto, qualità, velocità.
* **Disabled State**: I modelli che superano le risorse del computer vengono mostrati come disabilitati, con un messaggio chiaro del motivo (es. "Richiede 16GB RAM, rilevati 8GB").
* **Transizione**: L'utente clicca su un modello abilitato e conferma.

## 2. Selezione e Verifica del Dispositivo
Scopo: Rilevamento unificato e scelta del target Android (USB/Wireless). `[Ref: DEVICE-DISCOVERY-001]`
* **Initial State**: Tabella/Lista unificata dei device.
    * Colonne: Nome Device, Connessione (USB/Wireless), Stato. Per il Wireless, mostra l'indirizzo IP.
* **Empty State**: Nessun dispositivo trovato. Un pulsante chiaro suggerisce "Aggiorna" o "Configura Wireless".
* **States per Dispositivo**:
    * *Connected*: Pronto per essere selezionato.
    * *Unauthorized*: Collegato ma ADB necessita del prompt sullo schermo del telefono.
    * *Offline*: Precedentemente configurato ma irraggiungibile.
* **Wireless Setup**: Un pulsante/modalità per inserire un IP manuale e tentare l'accoppiamento wireless.
* **Requisito di Prodotto**: Una volta selezionato un dispositivo valido, la connessione (USB o Wireless) diventa trasparente per il resto dell'applicazione.

## 3. Selezione del Gioco
Scopo: Inserire l'automazione nel contesto corretto.
* **Initial State**: Griglia visiva con icone e nomi dei giochi supportati, accompagnati da una breve descrizione e gli obiettivi predefiniti.

## 4. Game Automation (Dashboard)
Scopo: Schermata operativa principale e monitoraggio del ciclo.
* **Elementi Visualizzati**:
    * Screenshot corrente analizzato dall'IA.
    * Stato generale (In esecuzione, In pausa, Fermo).
    * Azione corrente e Ultima azione eseguita.
    * **Spiegazione sintetica della decisione**: (es. "Uso l'abilità per sconfiggere il boss", NON il raw JSON o i log incomprensibili).
    * Cronologia delle azioni/statistiche base.
    * Indicatori del dispositivo, modello e gioco attivi.
* **Controlli (Initial/Running State)**: Start, Pause, Resume, Stop. 
    * "Stop" è distruttivo e ripristina la sessione, "Pause" ferma solo l'invio comandi temporaneamente.
* **User Override**: Un campo di input testuale ben visibile. 
    * Inserendo un override, l'UI mostra lo stato "Istruzione Utente Attiva: [Testo]".
    * Presente un bottone visibile per revocare l'override.

## 5. Impostazioni (Settings)
Scopo: Centralizzare tutte le configurazioni utente.
* Vedi dettagli in `docs/product/settings.md`. 
* Organizzata in categorie logiche (Generali, AI/LLM, Automazione, Dispositivi, Giochi, Logging, Interfaccia).


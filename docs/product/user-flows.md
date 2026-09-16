---
title: User Flows
status: approved
version: 1.0
date: 2026-09-16
---

# User Flows

## Main User Journey

1. **Avvio e Inizializzazione**: L'utente lancia l'app. Uno splash screen gestisce asincronamente la verifica delle dipendenze (ADB, LLM) mascherando la complessità tecnica.
2. **Selezione Modello (Se non preconfigurato)**: L'utente visualizza una lista di modelli LLM filtrati e suggeriti in base alla RAM del suo computer, ne comprende i compromessi e ne seleziona uno.
3. **Selezione Dispositivo**: L'utente accede alla lista unificata dei dispositivi Android rilevati (via cavo USB o tramite Wireless). Seleziona il device target (es. "Pixel 8 - USB").
4. **Selezione Gioco**: L'utente seleziona il gioco che desidera automatizzare da una griglia di giochi supportati (es. "Tap Titans 2").
5. **Dashboard di Automazione**: L'utente si trova nella schermata principale. Preme "Start".
6. **Esecuzione e Osservabilità**: Il sistema inizia a ciclare. L'utente vede aggiornarsi lo screenshot del dispositivo e legge spiegazioni sintetiche come: *"Trovato un boss, utilizzo l'abilità offensiva principale"*.
7. **Intervento Utente (Override)**: L'utente nota che l'AI sta spendendo monete inutilmente. Inserisce nell'apposito campo: "Non spendere più monete fino a nuovo ordine".
8. **Feedback all'Override**: Al ciclo successivo, l'utente legge: *"Nessuna azione intrapresa: bloccata la spesa di monete come richiesto dall'utente"*.
9. **Pausa/Stop**: L'utente preme "Pause" per rispondere a un messaggio sul telefono, e poi riprende con "Resume". Infine preme "Stop" per terminare la sessione.

## Modello Concettuale dell'Automazione (Agente AI)

L'automazione non è basata su script lineari, ma su un loop asincrono infinito controllato dallo stato (Running/Paused/Stopped).

1. **Osservazione**: Il sistema richiede al dispositivo Android (via ADB) uno screenshot aggiornato e legge gli eventuali stati interni (risoluzione, orientamento).
2. **Comprensione (Analyze)**: L'immagine, unita alle regole specifiche del gioco e agli override attivi dell'utente, viene inviata al modello LLM.
3. **Decisione**: Il LLM elabora i dati e restituisce una struttura contenente:
    * L'azione da eseguire (es. `TAP`, `SWIPE`, `WAIT`).
    * I parametri dell'azione (es. coordinate X, Y).
    * Una sintesi stringata in linguaggio naturale della decisione (es. *"Potenzio l'eroe per massimizzare il DPS"*).
4. **Validazione**: Il core dell'applicazione verifica che l'azione decisa dal LLM sia tecnicamente possibile e sicura (es. coordinate all'interno dello schermo).
5. **Esecuzione (Act)**: I comandi vengono inviati al dispositivo (es. `adb shell input tap X Y`).
6. **Feedback**: Il sistema valuta l'esito dell'azione (se misurabile).
7. **Attesa (Delay)**: L'app attende per un intervallo di tempo configurato (es. 2 secondi) per permettere alle animazioni del gioco di completarsi.
8. **Ripetizione**: Il ciclo riparte dal punto 1.


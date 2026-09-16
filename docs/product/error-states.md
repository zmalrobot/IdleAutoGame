---
title: Error States and Edge Cases
status: approved
version: 1.0
date: 2026-09-16
---

# Error States and Edge Cases

Definizione del comportamento atteso dell'applicazione di fronte a situazioni impreviste. L'obiettivo primario è salvaguardare il sistema e fermarsi in sicurezza (Fail-Safe), mantenendo informato l'utente.

## 1. Disconnessione Dispositivo (ADB Drop)
* **Scenario**: L'utente scollega il cavo USB durante un'azione, oppure cade il segnale Wi-Fi (ADB Wireless).
* **Comportamento Sistema**: Il processo di acquisizione screenshot o esecuzione tap lancia un'eccezione / timeout.
* **Stato UI**: L'automazione passa istantaneamente da "Running" a "Errore Dispositivo (In Pausa)".
* **Recovery**: Il ciclo si sospende. Viene abilitato un bottone per tentare la riconnessione. Quando il device torna online, l'utente preme "Resume". (Opzionalmente governato da `SETTING-AUT-003` auto-reconnect).

## 2. Errore Modello LLM / Timeout / Rate Limit
* **Scenario**: Il modello AI (locale o remoto) non risponde o risponde con errore HTTP.
* **Comportamento Sistema**: L'analisi fallisce per quel ciclo.
* **Stato UI**: Notifica soft ("Errore Modello. Tentativo X di Y").
* **Recovery**: Il sistema esegue i tentativi definiti in `SETTING-LLM-004`. Se si esauriscono, l'app applica il `SETTING-AUT-002` (solitamente Mette in Pausa) avvisando l'utente.

## 3. Decisioni LLM Impossibili o Malformate
* **Scenario**: Il LLM restituisce testo non JSON, coordinate invalide (es. X=9000), o un'azione che non esiste nel profilo del gioco (es. `ACTION_FLY`).
* **Comportamento Sistema**: La validazione pre-esecuzione blocca l'azione per sicurezza (il comando ADB NON parte).
* **Stato UI**: Loggato come "Azione non valida generata dall'AI".
* **Recovery**: Si salta l'esecuzione, si attende il delay, si fa un nuovo screenshot per far riprovare l'AI da zero con contesto aggiornato.

## 4. Nuovi Input Utente durante l'Analisi
* **Scenario**: L'utente invia un nuovo "User Override" mentre l'LLM sta processando lo screenshot precedente.
* **Comportamento Sistema**: La decisione del LLM in corso potrebbe non rispettare il nuovo override.
* **Recovery**: La decisione in corso viene scartata (oppure eseguita se ininfluente). Il nuovo override viene applicato strettamente al ciclo successivo.

## 5. Stato del Gioco non Previsto / App Chiusa
* **Scenario**: Il gioco Android va in crash chiudendosi, oppure appare una notifica di sistema a tutto schermo.
* **Comportamento Sistema**: Il LLM analizza l'immagine ma non trova gli elementi del gioco.
* **Stato UI**: Il LLM viene istruito nel prompt a restituire `UNKNOWN_STATE` o `WAIT`. L'UI mostra: "Stato gioco non riconosciuto (possibile crash/pubblicità)".
* **Recovery**: Se il sistema riceve `UNKNOWN_STATE` per troppi cicli consecutivi (es. 5), va in "Pausa" automaticamente.

## 6. Conflitto Logico (Utente vs Gioco)
* **Scenario**: L'utente scrive nell'override "Sblocca l'abilità speciale", ma non ci sono monete per farlo.
* **Comportamento Sistema**: Il LLM proverà a rispettare l'utente, ma deve fallire "gentilmente" spiegandolo.
* **Stato UI**: Spiegazione Sintetica: "L'utente richiede sblocco, ma mancano monete. Nessuna azione intrapresa".

## 7. Cambio Risoluzione / Orientamento
* **Scenario**: Il dispositivo Android viene ruotato o la risoluzione cambia.
* **Comportamento Sistema**: Il core deve rilevare le nuove metriche ADB (`wm size`).
* **Recovery**: I calcoli per le coordinate (relative -> assolute) e lo scaling per il LLM vengono rigenerati automaticamente senza bloccare l'app.


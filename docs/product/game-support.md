---
title: Game Support Concept
status: approved
version: 1.0
date: 2026-09-16
---

# Game Support Concept

## Visione Multi-Gioco
L'applicazione è progettata fin dalle basi per non essere legata a un singolo titolo. Funziona come un "motore" di automazione AI-driven, a cui è possibile agganciare **Profili di Gioco**.

## Il Modello Concettuale del Gioco

Ogni profilo di gioco, concettualmente, fornisce all'applicazione:

### 1. Elementi Specifici del Gioco
* **Identificatore e Metadati**: Nome, ID interno (es. `tap-titans-2`), Descrizione.
* **Prompt Base**: Le regole fondamentali del gioco, gli obiettivi (es. "Il tuo scopo è massimizzare lo stage. Se puoi fare un prestige, fallo.").
* **Azioni Conosciute**: Il dizionario formale delle mosse consentite (es. `TAP`, `SWIPE_UP`, `UPGRADE_HERO_1`).
* **Vincoli Strict**: Regole immutabili per prevenire disastri (es. "Vietato interagire con l'area (0-10%, 0-10%) dove si trova il tasto delle microtransazioni").

### 2. Elementi Comuni Gestiti dal Core
Questi non vanno reimplementati per ogni gioco, ma sono garantiti dall'app:
* Acquisizione Screenshot.
* Loop temporale (Delay, Retry, Pause, Stop).
* Traduzione delle azioni logiche (es. `TAP`) in comandi ADB fisici per Android.
* Gestione degli Overrides Testuali dell'utente.
* Interfacciamento con il provider LLM.

## Gestione di Versioni e Aggiornamenti
Un enorme vantaggio dell'utilizzo dell'IA basata sulla visione (LLM) rispetto ai bot tradizionali (pixel-matching/OCR fisso) è la **resilienza ai cambiamenti di UI**.
* Se il gioco si aggiorna e sposta un pulsante di pochi pixel, o ne cambia il colore, il LLM continuerà a capirne la semantica senza necessità di aggiornare l'applicazione.
* Nuove versioni dell'applicazione estenderanno semplicemente il prompt di base per coprire nuove meccaniche introdotte dal gioco.

## Configurazioni e Prompt
* **Globali**: Impostazioni come il provider LLM, il delay tra screenshot, il device usato.
* **Per-Gioco**: Impostazioni specifiche, ad esempio i prompt salvati dall'utente per uno specifico gioco ("In Tap Titans 2, usa solo l'abilità di fuoco"). Queste configurazioni vanno salvate e contestualizzate unicamente a quel gioco.


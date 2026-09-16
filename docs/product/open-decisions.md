---
title: Open Product Decisions
status: approved
version: 1.0
date: 2026-09-16
---

# Open Product Decisions

Queste decisioni di prodotto e architetturali rimangono aperte in attesa della Fase 2 (Progettazione Tecnica) e successive. Sono documentate qui per non bloccare la fase di specificazione funzionale.

## 1. Gestione Input Dispositivo
* **Dilemma**: L'uso standard di `adb shell input tap x y` soffre di elevata latenza overhead per ogni comando. Per i giochi idle lenti va bene, ma per futuri giochi veloci è problematico.
* **Decisione Aperta**: Nell'MVP usare ADB nativo per semplicità, ma l'architettura tecnica dovrà astrarre il `DeviceController` per supportare in futuro l'invio via socket `scrcpy` o un demone nativo pre-iniettato su Android.

## 2. Structured Output del LLM
* **Dilemma**: Dobbiamo fare parsing di testo libero che l'LLM deve formattare come JSON a mano, o affidarci alle funzionalità native di Tool Calling/Structured Output (JSON Schema) presenti nei modelli recenti?
* **Decisione Aperta**: Deleghiamo alla Fase 2 l'adozione del Tool Calling, considerando le SDK disponibili in C# (o il linguaggio scelto).

## 3. Packaging ed Esecuzione Locale dei Modelli
* **Dilemma**: Come forniremo all'utente finale (su Linux) il sistema di esecuzione dei modelli LLM locali (es. Llama.cpp, Ollama)?
* **Decisione Aperta**: Fuori scope per l'MVP, l'app probabilmente richiederà un backend tipo Ollama già attivo localmente o una chiave API esterna. Il packaging bundle (es. AppImage con runtime incluso) è posticipato.

## 4. Meccanica "Recording e Replay"
* **Dilemma**: C'è valore nel permettere all'utente di "registrare" macro di tap per ripeterle ad alta velocità, bypassando l'AI?
* **Decisione Aperta**: Sì, per coprire scenari "dumb" ad alta velocità (es. fare 500 tap in 10 secondi per sconfiggere un boss in Tap Titans). Verrà valutato post-MVP in base ai colli di bottiglia osservati.


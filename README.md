# IdleAutoGame

Applicazione desktop Linux per l'automazione di giochi mobile di tipo idle tramite dispositivi Android (ADB) e intelligenza artificiale (LLM).

## Struttura della Documentazione

La documentazione di prodotto (Fase 1) è la *source of truth* delle specifiche funzionali e di prodotto.

* `docs/PROJECT-CONTEXT.md`: Contesto generale, vision e onboarding.
* `docs/product/product-requirements.md`: Obiettivi, requisiti (con ID stabili), MVP e scope.
* `docs/product/user-flows.md`: Flussi utente principali e modello concettuale dell'automazione.
* `docs/product/screens.md`: Specifiche delle schermate e stati dell'interfaccia.
* `docs/product/user-stories.md`: User stories e acceptance criteria.
* `docs/product/game-support.md`: Modello concettuale per il supporto multi-gioco.
* `docs/product/settings.md`: Definizione centralizzata delle impostazioni utente.
* `docs/product/error-states.md`: Gestione degli errori e casi limite.
* `docs/product/open-decisions.md`: Decisioni di prodotto ancora aperte.
* `docs/changelog.md`: Storico delle modifiche alle specifiche.

## Regole di Manutenzione della Documentazione

1. La documentazione deve rimanere coerente con l'evoluzione del progetto.
2. Le modifiche ai requisiti devono aggiornare i Requirement ID e il `docs/changelog.md`.
3. I documenti obsoleti devono essere esplicitamente marcati (es. `status: deprecated`).
4. I nuovi agenti/sviluppatori devono poter comprendere il progetto leggendo questa documentazione senza bisogno dello storico della chat.


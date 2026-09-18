using System;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Thread-safe authoritative implementation of <see cref="IExecutionStateGuard"/>.
/// Enforces strict application-layer access boundaries during gameplay sessions.
/// </summary>
public sealed class ExecutionStateGuard : IExecutionStateGuard
{
    private readonly object _syncLock = new();

    private ApplicationRuntimeState _currentState = ApplicationRuntimeState.Idle;
    private GameplaySessionSnapshot? _activeSessionSnapshot;
    private string? _lastStateReason;

    /// <inheritdoc />
    public ApplicationRuntimeState CurrentState
    {
        get
        {
            lock (_syncLock)
            {
                return _currentState;
            }
        }
        private set
        {
            ApplicationRuntimeState oldState;
            string? reason;
            bool isLocked;

            lock (_syncLock)
            {
                if (_currentState == value) return;
                oldState = _currentState;
                _currentState = value;
                reason = _lastStateReason;
                isLocked = IsLockedInternal(_currentState);
            }

            StateChanged?.Invoke(this, new ApplicationRuntimeStateChangedEventArgs(oldState, value, reason, isLocked));
        }
    }

    /// <inheritdoc />
    public bool IsExecutionLocked
    {
        get
        {
            lock (_syncLock)
            {
                return IsLockedInternal(_currentState);
            }
        }
    }

    /// <inheritdoc />
    public GameplaySessionSnapshot? ActiveSessionSnapshot
    {
        get
        {
            lock (_syncLock)
            {
                return _activeSessionSnapshot;
            }
        }
        private set
        {
            lock (_syncLock)
            {
                _activeSessionSnapshot = value;
            }
        }
    }

    /// <inheritdoc />
    public string? LockSummary
    {
        get
        {
            lock (_syncLock)
            {
                if (!IsLockedInternal(_currentState) || _activeSessionSnapshot == null)
                {
                    return null;
                }

                var dev = !string.IsNullOrWhiteSpace(_activeSessionSnapshot.DeviceDisplayName)
                    ? _activeSessionSnapshot.DeviceDisplayName
                    : _activeSessionSnapshot.DeviceSerial;
                var mod = _activeSessionSnapshot.ModelId;
                var game = _activeSessionSnapshot.GameName;

                return $"🔒 Esecuzione attiva | Device: {dev} | Model: {mod} | Game: {game}";
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<ApplicationRuntimeStateChangedEventArgs>? StateChanged;

    private static bool IsLockedInternal(ApplicationRuntimeState state) =>
        state is ApplicationRuntimeState.Starting
              or ApplicationRuntimeState.Running
              or ApplicationRuntimeState.Pausing
              or ApplicationRuntimeState.Paused
              or ApplicationRuntimeState.Stopping
              or ApplicationRuntimeState.EmergencyStopping
              or ApplicationRuntimeState.Error;

    /// <inheritdoc />
    public ExecutionOperationResult CanChangeDevice(string serial)
    {
        lock (_syncLock)
        {
            if (IsLockedInternal(_currentState))
            {
                return ExecutionOperationResult.Rejected(
                    ExecutionLockReason.DeviceInUse,
                    _currentState,
                    "Impossibile cambiare dispositivo o modificare la connessione ADB durante l'esecuzione attiva. Arresta prima l'agente.");
            }

            return ExecutionOperationResult.Allowed(_currentState);
        }
    }

    /// <inheritdoc />
    public ExecutionOperationResult CanChangeModel(string modelId)
    {
        lock (_syncLock)
        {
            if (IsLockedInternal(_currentState))
            {
                return ExecutionOperationResult.Rejected(
                    ExecutionLockReason.ModelInUse,
                    _currentState,
                    "Impossibile cambiare modello o provider LLM durante l'esecuzione attiva. Arresta prima l'agente.");
            }

            return ExecutionOperationResult.Allowed(_currentState);
        }
    }

    /// <inheritdoc />
    public ExecutionOperationResult CanUnloadModel(string modelId)
    {
        lock (_syncLock)
        {
            if (IsLockedInternal(_currentState))
            {
                return ExecutionOperationResult.Rejected(
                    ExecutionLockReason.ModelInUse,
                    _currentState,
                    "Impossibile scaricare dalla memoria il modello LLM mentre la sessione di automazione è attiva o in pausa. Arresta prima l'agente.");
            }

            return ExecutionOperationResult.Allowed(_currentState);
        }
    }

    /// <inheritdoc />
    public ExecutionOperationResult CanChangeGame(string gameId)
    {
        lock (_syncLock)
        {
            if (IsLockedInternal(_currentState))
            {
                return ExecutionOperationResult.Rejected(
                    ExecutionLockReason.GameInUse,
                    _currentState,
                    "Impossibile cambiare gioco o modificare il target package durante l'esecuzione attiva. Arresta prima l'agente.");
            }

            return ExecutionOperationResult.Allowed(_currentState);
        }
    }

    /// <inheritdoc />
    public ExecutionOperationResult CanUpdateSetting(string category, string propertyName)
    {
        lock (_syncLock)
        {
            if (!IsLockedInternal(_currentState))
            {
                return ExecutionOperationResult.Allowed(_currentState);
            }

            // Classification of RuntimeMutable vs RuntimeLocked
            bool isMutable = category.Equals("Ui", StringComparison.OrdinalIgnoreCase)
                          || (category.Equals("General", StringComparison.OrdinalIgnoreCase) &&
                              (propertyName.Equals("Theme", StringComparison.OrdinalIgnoreCase) ||
                               propertyName.Equals("Locale", StringComparison.OrdinalIgnoreCase)))
                          || (category.Equals("Logging", StringComparison.OrdinalIgnoreCase) &&
                              (propertyName.Equals("SaveRawLlmOutput", StringComparison.OrdinalIgnoreCase) ||
                               propertyName.Equals("LogLevel", StringComparison.OrdinalIgnoreCase)));

            if (isMutable)
            {
                return ExecutionOperationResult.Allowed(_currentState);
            }

            return ExecutionOperationResult.Rejected(
                ExecutionLockReason.SettingLocked,
                _currentState,
                $"La modifica all'impostazione '{category}.{propertyName}' è bloccata durante l'esecuzione attiva. Arresta l'agente per applicarla.");
        }
    }

    /// <inheritdoc />
    public ExecutionOperationResult CanNavigateTo(string targetView)
    {
        // Dashboard is ALWAYS allowed for telemetry, observation, and emergency controls
        if (string.Equals(targetView, "Dashboard", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionOperationResult.Allowed(CurrentState);
        }

        lock (_syncLock)
        {
            if (IsLockedInternal(_currentState))
            {
                return ExecutionOperationResult.Rejected(
                    ExecutionLockReason.NavigationBlocked,
                    _currentState,
                    "L'applicazione è in esecuzione. Le impostazioni e la selezione di dispositivo/modello sono temporaneamente bloccate. Arresta l'agente per modificarle.");
            }

            return ExecutionOperationResult.Allowed(_currentState);
        }
    }

    /// <inheritdoc />
    public Task<ExecutionOperationResult> AcquireLockAsync(GameplaySessionSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_syncLock)
        {
            if (IsLockedInternal(_currentState))
            {
                return Task.FromResult(ExecutionOperationResult.Rejected(
                    ExecutionLockReason.ExecutionActive,
                    _currentState,
                    $"Impossibile avviare una nuova sessione: l'applicazione è già in stato '{_currentState}'."));
            }

            _activeSessionSnapshot = snapshot;
            _lastStateReason = null;
            CurrentState = ApplicationRuntimeState.Starting;

            return Task.FromResult(ExecutionOperationResult.Allowed(ApplicationRuntimeState.Starting));
        }
    }

    /// <inheritdoc />
    public void TransitionState(ApplicationRuntimeState newState, string? reason = null)
    {
        lock (_syncLock)
        {
            _lastStateReason = reason;
            CurrentState = newState;
        }
    }

    /// <inheritdoc />
    public Task ReleaseLockAsync(CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            _activeSessionSnapshot = null;
            _lastStateReason = null;
            CurrentState = ApplicationRuntimeState.Stopped;
        }

        return Task.CompletedTask;
    }
}


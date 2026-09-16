namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Lifecycle and installation status of a local LLM model.
/// </summary>
public enum ModelStatus
{
    /// <summary>
    /// Model metadata is present in catalog but files are not downloaded.
    /// </summary>
    NotInstalled,

    /// <summary>
    /// Model files are currently downloading from remote repository.
    /// </summary>
    Downloading,

    /// <summary>
    /// Model payload is undergoing integrity and checksum (SHA-256) validation.
    /// </summary>
    Verifying,

    /// <summary>
    /// Model is being registered or installed in local storage.
    /// </summary>
    Installing,

    /// <summary>
    /// Model is downloaded, verified, and ready to be loaded for inference.
    /// </summary>
    Ready,

    /// <summary>
    /// Model weights and context are actively being allocated into memory / GPU.
    /// </summary>
    Loading,

    /// <summary>
    /// Model is loaded into memory and ready for inference.
    /// </summary>
    Loaded,

    /// <summary>
    /// Model is currently executing an inference cycle.
    /// </summary>
    InUse,

    /// <summary>
    /// Model is currently releasing native memory and context.
    /// </summary>
    Unloading,

    /// <summary>
    /// Model files are in process of being safely deleted from disk.
    /// </summary>
    Deleting,

    /// <summary>
    /// Model encountered an error during download, verification, loading, or execution.
    /// </summary>
    Error
}


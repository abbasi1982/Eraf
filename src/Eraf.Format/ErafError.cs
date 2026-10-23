namespace Eraf.Format;

/// <summary>
/// Validation failures, named exactly as in ERAF_FORMAT_SPEC.md §9. Do not rename: other
/// implementations and the fixture sidecars use these strings.
/// </summary>
public enum ErafError
{
    TooShort,
    BadMagic,
    UnsupportedVersion,
    BadHeaderLength,
    HeaderChecksumMismatch,
    UnknownFlags,
    ReservedNonZero,
    UnsupportedMediaType,
    UnsupportedCodec,
    EmptyPayload,
    LimitExceeded,
    LengthMismatch,
    InvalidPayloadSignature,
    HashMismatch,
}

/// <summary>Thrown by the throwing read/write entry points; carries the spec error code.</summary>
public sealed class ErafFormatException : Exception
{
    public ErafFormatException(ErafError error)
        : base($"ERAF format error: {error}") => Error = error;

    public ErafError Error { get; }
}

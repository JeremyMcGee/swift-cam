namespace SwiftCam;

/// <summary>
/// Response DTO returned by the /api/audio-status endpoint.
/// </summary>
public record AudioStatusResponse(
    string State,
    string Reason,
    string? CurrentWindowStart,
    string? CurrentWindowEnd,
    string? NextWindowStart);

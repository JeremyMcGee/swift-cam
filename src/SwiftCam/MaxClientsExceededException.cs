namespace SwiftCam;

/// <summary>
/// Thrown when a new client attempts to subscribe but the maximum number
/// of concurrent subscribers has been reached.
/// The HTTP layer should map this to a 503 Service Unavailable response.
/// </summary>
public class MaxClientsExceededException : Exception
{
    public MaxClientsExceededException()
        : base("Maximum number of concurrent clients exceeded.")
    {
    }

    public MaxClientsExceededException(string message)
        : base(message)
    {
    }

    public MaxClientsExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

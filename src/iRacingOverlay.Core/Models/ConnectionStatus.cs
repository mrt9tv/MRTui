namespace iRacingOverlay.Core.Models;

/// <summary>
/// Represents the connection status to iRacing
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Not connected to iRacing
    /// </summary>
    Disconnected,

    /// <summary>
    /// Currently attempting to connect
    /// </summary>
    Connecting,

    /// <summary>
    /// Successfully connected and receiving data
    /// </summary>
    Connected,

    /// <summary>
    /// Connection lost, attempting to reconnect
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Connection failed with error
    /// </summary>
    Error
}

/// <summary>
/// Event args for connection status changes
/// </summary>
public class ConnectionStatusEventArgs : EventArgs
{
    public ConnectionStatus Status { get; }
    public string? Message { get; }
    public Exception? Error { get; }

    public ConnectionStatusEventArgs(ConnectionStatus status, string? message = null, Exception? error = null)
    {
        Status = status;
        Message = message;
        Error = error;
    }
}

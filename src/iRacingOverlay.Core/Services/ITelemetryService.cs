using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Interface for telemetry service that connects to iRacing and provides telemetry data
/// </summary>
public interface ITelemetryService : IDisposable
{
    /// <summary>
    /// Gets the current connection status
    /// </summary>
    ConnectionStatus Status { get; }

    /// <summary>
    /// Gets whether the service is currently connected to iRacing
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the current telemetry update rate in Hz (updates per second)
    /// </summary>
    double UpdateRate { get; }

    /// <summary>
    /// Event fired when telemetry data is updated
    /// </summary>
    event EventHandler<TelemetryData>? TelemetryUpdated;

    /// <summary>
    /// Event fired when connection status changes
    /// </summary>
    event EventHandler<ConnectionStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Connects to iRacing and starts monitoring telemetry
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from iRacing and stops monitoring
    /// </summary>
    Task DisconnectAsync();
}

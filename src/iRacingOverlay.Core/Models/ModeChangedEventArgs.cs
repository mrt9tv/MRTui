namespace iRacingOverlay.Core.Models;

/// <summary>
/// Event args for mode change notifications
/// </summary>
public class ModeChangedEventArgs : EventArgs
{
    /// <summary>
    /// The previous operational mode
    /// </summary>
    public OperationalMode PreviousMode { get; set; }
    
    /// <summary>
    /// The new operational mode
    /// </summary>
    public OperationalMode NewMode { get; set; }
    
    /// <summary>
    /// Timestamp when the mode change occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public ModeChangedEventArgs(OperationalMode previousMode, OperationalMode newMode)
    {
        PreviousMode = previousMode;
        NewMode = newMode;
    }
}

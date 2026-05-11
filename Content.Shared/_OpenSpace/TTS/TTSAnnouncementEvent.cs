using Robust.Shared.Player;

namespace Content.Shared._OpenSpace.TTS;

/// <summary>
///     Raised on the server when a station or global announcement is dispatched,
///     so the TTS system can generate and broadcast announcement audio.
/// </summary>
public sealed class TTSAnnouncementEvent : EntityEventArgs
{
    public readonly string Message;
    public readonly Filter Recipients;

    public TTSAnnouncementEvent(string message, Filter recipients)
    {
        Message = message;
        Recipients = recipients;
    }
}

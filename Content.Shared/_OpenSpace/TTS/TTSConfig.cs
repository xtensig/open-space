using Content.Shared.Humanoid;

namespace Content.Shared._OpenSpace.TTS;

public sealed class TTSConfig
{
    public const string DefaultVoice = "gman";
    public static readonly Dictionary<Sex, string> DefaultSexVoice = new()
    {
        {Sex.Male, "Eugene"},
        {Sex.Female, "Kseniya"},
        {Sex.Unsexed, "Xenia"}
    };
    public const int VoiceRange = 10;
    public const int WhisperClearRange = 2;
    public const int WhisperMuffledRange = 5;
}

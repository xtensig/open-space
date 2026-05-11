using Content.Shared.Chat;
using Content.Shared.CCVar;
using Content.Shared._OpenSpace.TTS;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using System.IO;

namespace Content.Client._OpenSpace.TTS;

internal record struct TTSQueueElem(AudioStream Audio, bool IsWhisper, bool IsRadio, NetEntity Source);

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IAudioManager _audioLoader = default!;

    private ISawmill _sawmill = default!;
    private bool _enabled = true;

    /// <summary>
    /// Reducing the volume of the TTS when whispering. Will be converted to logarithm.
    /// </summary>
    private const float WhisperFade = 4f;

    /// <summary>
    /// The volume at which the TTS sound will not be heard.
    /// </summary>
    private const float MinimalVolume = -10f;

    /// <summary>
    /// Maximum queued tts talks per entity.
    /// </summary>
    private const int MaxQueuedSounds = 20;

    private float _volume = 0.0f;
    private float _radioVolume = 0.0f;
    internal List<NetEntity> _toDelete = new();

    // Author -> Queue of sounds from different sources
    private Dictionary<NetEntity, Queue<TTSQueueElem>> _queue = new();
    // Author -> currently playing sound
    private Dictionary<NetEntity, AudioComponent?> _playing = new();

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        _cfg.OnValueChanged(CCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(CCVars.TTSRadioVolume, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(CCVars.TTSClientEnabled, OnTtsClientOptionChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCVars.TTSVolume, OnTtsVolumeChanged);
        _cfg.UnsubValueChanged(CCVars.TTSRadioVolume, OnTtsRadioVolumeChanged);
        _cfg.UnsubValueChanged(CCVars.TTSClientEnabled, OnTtsClientOptionChanged);
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!_enabled) return;
        _toDelete.Clear();
        foreach (var (uid, comp) in _playing)
        {
            if (comp != null && !comp.Deleted)
            {
                if (!comp.Playing)
                    _toDelete.Add(uid);
            }
            else
            {
                _toDelete.Add(uid);
            }
        }
        foreach (var uid in _toDelete)
        {
            _playing.Remove(uid);
        }

        _toDelete.Clear();
        foreach (var (author, queue) in _queue)
        {
            if (queue.Count <= 0)
            {
                _toDelete.Add(author);
                continue;
            }
            if (_playing.ContainsKey(author)) continue;
            if (!queue.TryDequeue(out var elem)) continue;
            if (!TryGetEntity(elem.Source, out var localSource))
                continue;
            _playing[author] = PlayTTSFromUid(localSource, elem.Audio, elem.IsWhisper, elem.IsRadio);
        }
        foreach (var author in _toDelete)
        {
            _queue.Remove(author);
        }
    }

    public AudioComponent? PlayTTSFromUid(EntityUid? uid, AudioStream audioStream, bool isWhisper, bool isRadio = false)
    {
        var audioParams = AudioParams.Default
            .WithVolume(isRadio ? AdjustRadioVolume() : AdjustVolume(isWhisper))
            .WithMaxDistance(AdjustDistance(isWhisper));
        (EntityUid Entity, AudioComponent Component)? stream;

        _sawmill.Verbose($"Playing TTS audio {audioStream.Length} bytes from {uid} entity");

        if (uid is not null)
            stream = _audio.PlayEntity(audioStream, uid.Value, null, audioParams);
        else
            stream = _audio.PlayGlobal(audioStream, null, audioParams);

        return stream?.Component;
    }

    public void RequestPreviewTTS(string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnTtsClientOptionChanged(bool option)
    {
        _enabled = option;
        RaiseNetworkEvent(new ClientOptionTTSEvent(option));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnTtsRadioVolumeChanged(float volume)
    {
        _radioVolume = volume;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        if (!_enabled) return;
        var source = ev.SourceUid ?? NetEntity.Invalid;
        var author = ev.Author ?? source;
        if (!_queue.ContainsKey(author))
            _queue[author] = new();

        if (_queue[author].Count >= MaxQueuedSounds)
            return;

        var audioStream = _audioLoader.LoadAudioOggVorbis(new MemoryStream(ev.Data));

        if (!author.Valid)
        {
            PlayTTSFromUid(null, audioStream, ev.IsWhisper, ev.IsRadio);
        }
        else
        {
            _queue[author].Enqueue(new TTSQueueElem
            {
                Audio = audioStream,
                IsWhisper = ev.IsWhisper,
                IsRadio = ev.IsRadio,
                Source = source,
            });
        }
    }

    private float AdjustVolume(bool isWhisper)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volume * 3.0f);

        if (isWhisper)
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);

        return volume;
    }

    private float AdjustRadioVolume()
    {
        return MinimalVolume + SharedAudioSystem.GainToVolume(_radioVolume * 3.0f);
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? TTSConfig.WhisperMuffledRange : TTSConfig.VoiceRange;
    }
}

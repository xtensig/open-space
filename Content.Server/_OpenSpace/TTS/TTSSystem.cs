using Content.Server.Communications;
using Content.Server.Players.RateLimiting;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Players.RateLimiting;
using Content.Shared.CCVar;
using Content.Shared._OpenSpace.TTS;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Threading.Tasks;

namespace Content.Server._OpenSpace.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;

    private List<ICommonSession> _ignoredRecipients = new();
    private EntityUid? _pendingAnnouncementActor; // OpenSpace

    private readonly List<string> _sampleText =
        new()
        {
            "Съешь же ещё этих мягких французских булок, да выпей чаю.",
            "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
            "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
            "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!!",
            "Учёные, тут странная аномалия в баре! Она уже съела мима!",
            "Я надеюсь что инженеры внимательно следят за сингулярностью...",
            "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
            "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
            "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
            "Вам нужно согласие и печать квартирмейстера, если вы хотите сделать заказ на партию дробовиков.",
            "Возле эвакуационного шаттла разгерметизация! Инженеры, нам срочно нужна ваша помощь!",
            "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах!"
        };

    private const int MaxMessageChars = 100 * 2;
    private bool _isEnabled = false;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<ActorComponent, TTSRadioPlayEvent>(OnTTSRadioPlayEvent);
        SubscribeLocalEvent<TTSAnnouncementEvent>(OnAnnouncement);
        SubscribeLocalEvent<CommunicationConsoleAnnouncementEvent>(OnConsoleAnnouncement); // OpenSpace

        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);
        SubscribeNetworkEvent<ClientOptionTTSEvent>(OnClientOptionTTS);

        RegisterRateLimits();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private async void OnClientOptionTTS(ClientOptionTTSEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Enabled)
            _ignoredRecipients.Remove(args.SenderSession);
        else
            _ignoredRecipients.Add(args.SenderSession);
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        if (HandleRateLimit(args.SenderSession) != RateLimitStatus.Allowed)
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData, null), Filter.SinglePlayer(args.SenderSession));
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        if (args.ObfuscatedMessage != null)
        {
            HandleWhisper(uid, args.Message, args.ObfuscatedMessage, protoVoice.Speaker);
            return;
        }

        HandleSay(uid, args.Message, protoVoice.Speaker);
    }

    // OpenSpace edit start
    private void OnConsoleAnnouncement(ref CommunicationConsoleAnnouncementEvent ev)
    {
        _pendingAnnouncementActor = ev.Sender;
    }
    // OpenSpace edit end

    private async void OnAnnouncement(TTSAnnouncementEvent ev)
    {
        if (!_isEnabled) return;

        // OpenSpace edit start
        string speaker;
        var actor = _pendingAnnouncementActor;
        _pendingAnnouncementActor = null;

        if (actor != null
            && TryComp<TTSComponent>(actor, out var ttsComp)
            && ttsComp.VoicePrototypeId != null
            && _prototypeManager.TryIndex<TTSVoicePrototype>(ttsComp.VoicePrototypeId, out var actorVoice))
        {
            speaker = actorVoice.Speaker;
        }
        else
        {
            var voiceId = _cfg.GetCVar(CCVars.TTSAnnounceVoiceId);
            if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var defaultVoice))
                return;
            speaker = defaultVoice.Speaker;
        }
        // OpenSpace edit end

        var soundData = await GenerateTTS(ev.Message, speaker);
        if (soundData is null) return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData, null, isRadio: true), ev.Recipients.RemovePlayers(_ignoredRecipients));
    }

    private async void OnTTSRadioPlayEvent(EntityUid uid, ActorComponent comp, TTSRadioPlayEvent args)
    {
        var soundData = await GenerateTTS(args.Message, args.Voice, "radio");
        if (soundData is null) return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData, args.Source, false, args.Author, isRadio: true), uid);
    }

    private async void HandleSay(EntityUid uid, string message, string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null) return;
        RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid)), Filter.Pvs(uid).RemovePlayers(_ignoredRecipients));
    }

    private async void HandleWhisper(EntityUid uid, string message, string obfMessage, string speaker)
    {
        var fullSoundData = await GenerateTTS(message, speaker);
        if (fullSoundData is null) return;

        var obfSoundData = await GenerateTTS(obfMessage, speaker);
        if (obfSoundData is null) return;

        var fullTtsEvent = new PlayTTSEvent(fullSoundData, GetNetEntity(uid), true);
        var obfTtsEvent = new PlayTTSEvent(obfSoundData, GetNetEntity(uid), true);

        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourcePos = _xforms.GetWorldPosition(xformQuery.GetComponent(uid), xformQuery);
        var receptions = Filter.Pvs(uid).Recipients;
        foreach (var session in receptions)
        {
            if (!session.AttachedEntity.HasValue) continue;

            if (_ignoredRecipients.Contains(session)) continue;

            var xform = xformQuery.GetComponent(session.AttachedEntity.Value);
            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();
            if (distance > SharedChatSystem.VoiceRange * SharedChatSystem.VoiceRange)
                continue;

            RaiseNetworkEvent(distance > SharedChatSystem.WhisperClearRange ? obfTtsEvent : fullTtsEvent, session);
        }
    }

    // ReSharper disable once InconsistentNaming
    private async Task<byte[]?> GenerateTTS(string text, string speaker, string? effect = null)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "") return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        return await _ttsManager.ConvertTextToSpeech(speaker, textSanitized, effect);
    }
}

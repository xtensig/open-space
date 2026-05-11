using Content.Server._OpenSpace.TTS;
using Content.Shared.VoiceMask;
using Content.Shared._OpenSpace.TTS;
using Content.Shared.Inventory;

namespace Content.Server.VoiceMask;

public partial class VoiceMaskSystem
{
    private void InitializeTTS()
    {
        SubscribeLocalEvent<VoiceMaskComponent, InventoryRelayedEvent<TransformSpeakerVoiceEvent>>(OnSpeakerVoiceTransform);
        SubscribeLocalEvent<VoiceMaskComponent, VoiceMaskChangeVoiceMessage>(OnChangeVoice);
    }

    private string? GetEffectiveVoiceId(Entity<VoiceMaskComponent> entity)
    {
        if (entity.Comp.VoiceId != null)
            return entity.Comp.VoiceId;

        if (_container.TryGetContainingContainer(entity.Owner, out var container) &&
            TryComp<TTSComponent>(container.Owner, out var tts))
            return tts.VoicePrototypeId;

        return null;
    }

    private void OnSpeakerVoiceTransform(Entity<VoiceMaskComponent> ent, ref InventoryRelayedEvent<TransformSpeakerVoiceEvent> args)
    {
        if (ent.Comp.VoiceId != null)
            args.Args.VoiceId = ent.Comp.VoiceId;
    }

    private void OnChangeVoice(Entity<VoiceMaskComponent> entity, ref VoiceMaskChangeVoiceMessage msg)
    {
        if (msg.Voice is { } id && !_proto.HasIndex<TTSVoicePrototype>(id))
            return;

        entity.Comp.VoiceId = msg.Voice;

        _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-success"), entity, msg.Actor);

        UpdateUI(entity);
    }
}

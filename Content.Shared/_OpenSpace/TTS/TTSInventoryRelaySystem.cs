using Content.Shared.Inventory;

namespace Content.Shared._OpenSpace.TTS;

/// <summary>
///     Relays <see cref="TransformSpeakerVoiceEvent"/> to inventory items so VoiceMask
///     in the mask slot can override the TTS voice.
/// </summary>
public sealed class TTSInventoryRelaySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InventoryComponent, TransformSpeakerVoiceEvent>(OnTransformSpeakerVoice);
    }

    private void OnTransformSpeakerVoice(EntityUid uid, InventoryComponent component, TransformSpeakerVoiceEvent args)
    {
        _inventory.RelayEvent((uid, component), args);
    }
}

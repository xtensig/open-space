using Content.Shared._OpenSpace.TypeAuth;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;

namespace Content.Client._OpenSpace.TypeAuth;

public sealed class TypeAuthUIController : UIController
{
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IUriOpener _uriOpener = default!;

    private TypeAuthPopup? _popup;

    public override void Initialize()
    {
        base.Initialize();

        _netManager.RegisterNetMessage<TypeAuthShowMessage>(OnShowMessage);
        _netManager.RegisterNetMessage<TypeAuthCheckMessage>();
        _netManager.RegisterNetMessage<TypeAuthResultMessage>(OnResultMessage);
    }

    private void OnShowMessage(TypeAuthShowMessage message)
    {
        if (_popup != null)
            return;

        _clyde.RequestWindowAttention();

        _popup = new TypeAuthPopup(_uriOpener);
        _popup.SetLink(message.AuthLink);
        _popup.OnCheckPressed += OnCheckPressed;

        UIManager.WindowRoot.AddChild(_popup);
        LayoutContainer.SetAnchorPreset(_popup, LayoutContainer.LayoutPreset.Wide);
    }

    private void OnCheckPressed()
    {
        _popup?.SetChecking(true);
        _netManager.ClientSendMessage(new TypeAuthCheckMessage());
    }

    private void OnResultMessage(TypeAuthResultMessage message)
    {
        if (_popup == null)
            return;

        if (message.Success)
        {
            _popup.Orphan();
            _popup = null;
            return;
        }

        _popup.SetChecking(false);
        _popup.SetError(message.ErrorMessage);
    }
}

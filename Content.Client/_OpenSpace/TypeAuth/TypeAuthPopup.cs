using Content.Client.Parallax;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._OpenSpace.TypeAuth;

public sealed class TypeAuthPopup : Control
{
    public event Action? OnCheckPressed;

    private readonly Button _openLinkButton;
    private readonly Button _checkButton;
    private readonly Label _checkingLabel;
    private readonly Label _errorLabel;
    private string _link = "";

    public TypeAuthPopup(IUriOpener uriOpener)
    {
        MouseFilter = MouseFilterMode.Stop;

        AddChild(new ParallaxControl { SpeedX = 20 });

        _openLinkButton = new Button
        {
            Text = Loc.GetString("typeauth-window-open-link-button"),
            HorizontalAlignment = HAlignment.Center,
        };
        _openLinkButton.OnPressed += _ =>
        {
            if (!string.IsNullOrEmpty(_link))
                uriOpener.OpenUri(_link);
        };

        _checkButton = new Button
        {
            Text = Loc.GetString("typeauth-window-check-button"),
            HorizontalAlignment = HAlignment.Center,
        };
        _checkButton.OnPressed += _ => OnCheckPressed?.Invoke();

        _checkingLabel = new Label
        {
            Text = Loc.GetString("typeauth-window-checking"),
            HorizontalAlignment = HAlignment.Center,
            Visible = false,
        };

        _errorLabel = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            Align = Label.AlignMode.Center,
            Visible = false,
            ModulateSelfOverride = Color.Red,
        };

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 12,
            Margin = new Thickness(16),
            Children =
            {
                new Label
                {
                    Text = Loc.GetString("typeauth-window-title"),
                    HorizontalAlignment = HAlignment.Center,
                    StyleClasses = { "LabelHeading" },
                },
                new Label
                {
                    Text = Loc.GetString("typeauth-window-description"),
                    HorizontalAlignment = HAlignment.Center,
                    Align = Label.AlignMode.Center,
                },
                _openLinkButton,
                _checkButton,
                _checkingLabel,
                _errorLabel,
            },
        };

        var panel = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            StyleClasses = { "windowPanel" },
        };
        panel.AddChild(content);

        AddChild(panel);
    }

    public void SetLink(string link) => _link = link;

    public void SetChecking(bool checking)
    {
        _checkButton.Disabled = checking;
        _openLinkButton.Disabled = checking;
        _checkingLabel.Visible = checking;
        if (checking)
            _errorLabel.Visible = false;
    }

    public void SetError(string? error)
    {
        _errorLabel.Text = error ?? "";
        _errorLabel.Visible = error != null;
    }
}

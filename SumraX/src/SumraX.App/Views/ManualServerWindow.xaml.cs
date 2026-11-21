using System.Windows;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;

namespace SumraX.App.Views;

public partial class ManualServerWindow : Window
{
    private readonly ILocalizationService _loc;

    public ServerProfile? Result { get; private set; }

    public ManualServerWindow(ILocalizationService loc)
    {
        _loc = loc;
        InitializeComponent();
        ApplyLocalization();
        _loc.LanguageChanged += (_, _) => ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        Title = _loc.Get("ManualServerTitle");
        TitleLabel.Text = _loc.Get("ManualServerEntry");
        NameLabel.Text = _loc.Get("Name");
        ProtocolLabel.Text = _loc.Get("Protocol");
        AddressLabel.Text = _loc.Get("Address");
        PortLabel.Text = _loc.Get("Port");
        UserIdLabel.Text = _loc.Get("UserId");
        PasswordLabel.Text = _loc.Get("PasswordField");
        EncryptionLabel.Text = _loc.Get("Encryption");
        NetworkLabel.Text = _loc.Get("Network");
        SecurityLabel.Text = _loc.Get("TlsField");
        SniLabel.Text = _loc.Get("Sni");
        HostLabel.Text = _loc.Get("Host");
        PathLabel.Text = _loc.Get("Path");
        CancelButton.Content = _loc.Get("Cancel");
        SaveButton.Content = _loc.Get("Save");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AddressBox.Text))
        {
            MessageBox.Show(_loc.Get("AddressRequired"), _loc.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535)
        {
            MessageBox.Show(_loc.Get("InvalidPort"), _loc.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var protocol = ProtocolBox.SelectedIndex switch
        {
            1 => VpnProtocol.VLESS,
            2 => VpnProtocol.Trojan,
            3 => VpnProtocol.Shadowsocks,
            _ => VpnProtocol.VMess
        };

        Result = ManualServerFactory.Create(
            NameBox.Text,
            protocol,
            AddressBox.Text,
            port,
            UserIdBox.Text,
            PasswordBox.Text,
            EncryptionBox.Text,
            NetworkBox.Text,
            SecurityBox.Text,
            SniBox.Text,
            HostBox.Text,
            PathBox.Text);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

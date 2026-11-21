using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SumraX.App.Views.Controls;

public partial class CoderCreditBadge : UserControl
{
    private Storyboard? _reveal;
    private Storyboard? _hide;

    public CoderCreditBadge()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _reveal = (Storyboard)FindResource("RevealStoryboard");
            _hide = (Storyboard)FindResource("HideStoryboard");
        };
    }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => _reveal?.Begin(this, true);

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => _hide?.Begin(this, true);
}

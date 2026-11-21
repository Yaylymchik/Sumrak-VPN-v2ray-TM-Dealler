using CommunityToolkit.Mvvm.ComponentModel;
using SumraX.Core.Interfaces;

namespace SumraX.App.ViewModels;

public abstract class LocalizableViewModelBase : ObservableObject
{
    protected readonly ILocalizationService Loc;

    protected LocalizableViewModelBase(ILocalizationService loc)
    {
        Loc = loc;
        loc.LanguageChanged += (_, _) => OnLanguageChanged();
    }

    /// <summary>Call at end of derived constructor after all fields are assigned.</summary>
    protected void CompleteInitialization() => OnLanguageChanged();

    protected string L(string key) => Loc.Get(key);

    protected string F(string key, params object[] args) => Loc.Format(key, args);

    protected virtual void OnLanguageChanged() { }
}

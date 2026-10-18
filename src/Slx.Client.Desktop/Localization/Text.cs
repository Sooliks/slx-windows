using System.Globalization;
using System.ComponentModel;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;
namespace Slx.Client.Desktop.Localization;
public sealed class Text : INotifyPropertyChanged
{
    public static Text Catalog { get; } = new();
    private readonly ResourceManager resources = new("Slx.Client.Desktop.Localization.Strings", typeof(Text).Assembly);
    private CultureInfo culture = CultureInfo.GetCultureInfo("ru");
    public string this[string key] => resources.GetString(key, culture) ?? key;
    public string Language => culture.TwoLetterISOLanguageName;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void SetLanguage(string language)
    {
        culture = CultureInfo.GetCultureInfo(language == "en" ? "en" : "ru");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        PropertyChanged?.Invoke(this, new("Item[]"));
    }
}
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Text.Catalog, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}

using System.Collections;
using System.Globalization;
using System.Resources;
using Slx.Client.Desktop.Localization;
using Xunit;
namespace Slx.Client.UiTests;
internal static class LocalizationChecks
{
    // Every RU/EN catalog keeps the same keys, so no surface falls back to a raw key in one language.
    public static void Run()
    {
        foreach (var catalog in new[] { "Strings", "LibraryStrings", "UpdatesStrings", "RoutingStrings" })
        {
            var resources = new ResourceManager($"Slx.Client.Desktop.Localization.{catalog}", typeof(Text).Assembly);
            Assert.Equal(Keys(resources, CultureInfo.InvariantCulture), Keys(resources, CultureInfo.GetCultureInfo("ru")));
        }
        foreach (var language in new[] { "ru", "en" })
        {
            Text.Catalog.SetLanguage(language);
            foreach (var key in new[] { "RouteChoiceHint", "RemoveBlocked", "TrayStatus" }) Assert.NotEqual(key, Text.Catalog[key]);
            Assert.Contains("{0}", Text.Catalog["TrayStatus"]);
        }
        Text.Catalog.SetLanguage("ru");
    }
    private static string[] Keys(ResourceManager resources, CultureInfo culture) =>
        (resources.GetResourceSet(culture, true, false) ?? throw new InvalidOperationException(culture.Name))
        .Cast<DictionaryEntry>().Select(entry => (string)entry.Key).Order(StringComparer.Ordinal).ToArray();
}

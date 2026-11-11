using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Slx.Client.Core;
using Slx.Client.Desktop.Localization;
using Slx.Client.Desktop.Presentation;
using Slx.Client.Windows;
namespace Slx.Client.Desktop.ViewModels;
/// <summary>
/// First-run setup: locally found Discord, Telegram and browsers are offered as one-step additions, and a copied
/// SLX link is offered in the import dialog. Presentation only: a suggestion never enables an application,
/// imports a link or sends a service command until the user presses the corresponding control.
/// </summary>
public sealed partial class MainViewModel
{
    private const int StarterLimit = 4, DirectNameLimit = 2;
    private string quickStartDirect = "", clipboardLink = "";
    private bool quickStartSession, quickStartScanRequested;
    private RelayCommand? pasteLink, openDevices;
    internal Func<string?>? ClipboardTextOverride { get; set; }
    public ObservableCollection<LibraryEntry> StarterApplications { get; } = [];
    public bool HasStarterApplications => StarterApplications.Count > 0;
    /// <summary>Visible while nothing is chosen, then for the rest of that first session while suggestions remain.</summary>
    public bool ShowQuickStart => HasSubscription && IsSelectedMode && (!HasApplications || quickStartSession && HasStarterApplications);
    public string QuickStartBody => Text.Catalog[HasStarterApplications ? "QuickStartBody" : Discovering ? "QuickStartSearching" : "QuickStartEmpty"];
    public string QuickStartDirect => quickStartDirect;
    public string ImportFootnoteText => Text.Catalog[importLink.Length > 0 && importLink == clipboardLink ? "ImportClipboardHint" : "ImportFootnote"];
    public RelayCommand PasteLinkCommand => pasteLink ??= new(() => { if (!IsBusy && !OfferClipboardLink()) Notice("ClipboardNoLink"); });
    public RelayCommand OpenDevicesCommand => openDevices ??= new(() => OpenLink("https://slxvpn.com/account/devices#add-device"));
    private void RefreshQuickStart(IReadOnlyList<LibraryEntry> entries)
    {
        if (HasSubscription && IsSelectedMode && !HasApplications) quickStartSession = true;
        var order = new LibraryApplicationOrder(CatalogEntries);
        var installed = entries.Where(entry => entry.Rule is not null && !entry.IsAdded)
            .Select(entry => (Entry: entry, Paths: entry.Paths ?? [entry.Rule!])).ToArray();
        var starters = installed.Select(item => (item.Entry, Rank: item.Paths.Min(order.StarterRank)))
            .Where(item => item.Rank is not null).OrderBy(item => item.Rank).ThenBy(item => item.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Entry).Take(StarterLimit).ToArray();
        if (!StarterApplications.SequenceEqual(starters))
        {
            StarterApplications.Clear();
            foreach (var entry in starters) StarterApplications.Add(entry);
        }
        // Unselected programs use the direct outbound in this mode; naming found games restates that rule only.
        var games = installed.Where(item => item.Paths.Any(order.IsGame)).Select(item => item.Entry.Name)
            .Distinct(StringComparer.CurrentCultureIgnoreCase).Take(DirectNameLimit).ToArray();
        quickStartDirect = games.Length == 0 ? Text.Catalog["QuickStartDirectRest"]
            : string.Format(Text.Catalog.Culture, Text.Catalog["QuickStartDirectGames"], string.Join(", ", games));
        NotifyQuickStart();
    }
    private void NotifyQuickStart()
    {
        foreach (var name in new[] { nameof(ShowQuickStart), nameof(HasStarterApplications), nameof(QuickStartBody), nameof(QuickStartDirect) })
            OnPropertyChanged(name);
    }
    /// <summary>One bounded local scan so the first screen can name installed programs; stored test models never scan.</summary>
    internal void EnsureQuickStartScan()
    {
        if (!persistPreferences || quickStartScanRequested || disposed || !ShowQuickStart) return;
        quickStartScanRequested = true;
        _ = RefreshLibraryIfNeededAsync();
    }
    /// <summary>Reads the clipboard only after an explicit import action and keeps nothing but a valid SLX link.</summary>
    private bool OfferClipboardLink()
    {
        string? text;
        try { text = ClipboardTextOverride is not null ? ClipboardTextOverride() : persistPreferences && Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        catch (Exception error) when (error is ExternalException or System.Threading.ThreadStateException) { text = null; }
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 2048) return false;
        try { _ = SubscriptionClient.ValidateUrl(text); }
        catch (ClientFault) { return false; }
        clipboardLink = text; ImportLink = text; ClearMessage();
        OnPropertyChanged(nameof(ImportFootnoteText));
        return true;
    }
}

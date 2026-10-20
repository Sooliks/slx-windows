using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using Slx.Client.Core;

namespace Slx.Client.Windows;

public enum DiscoverySource { Running, Installed, Shortcuts, Steam, Groups, Catalog }
public sealed record DiscoveryBatch(DiscoverySource Source, IReadOnlyList<AppRule> Apps, bool Success);
internal sealed record DiscoverySourceScan(DiscoverySource Source, Func<CancellationToken, IReadOnlyList<AppRule>> Find);

/// <summary>Independent local sources; completion and cancellation wait for every started native scan.</summary>
public static class DiscoveryScanner
{
    private const int Concurrency = 3;
    public static Task RunAsync(IReadOnlyList<CatalogApplication> catalog, Action<DiscoveryBatch> publish,
        CancellationToken cancellationToken) => RunAsync(new DiscoverySourceScan[] {
            new(DiscoverySource.Running, AppDiscovery.RunningApplications),
            new(DiscoverySource.Installed, AppDiscovery.InstalledApplications),
            new(DiscoverySource.Shortcuts, ShortcutApplications.Find),
            new(DiscoverySource.Steam, SteamApplications.Find),
            new(DiscoverySource.Groups, ApplicationGroups.Discover),
            new(DiscoverySource.Catalog, token => CatalogApplications.Find(catalog, token))
        }, publish, cancellationToken);

    internal static async Task RunAsync(IReadOnlyList<DiscoverySourceScan> sources, Action<DiscoveryBatch> publish,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(publish);
        cancellationToken.ThrowIfCancellationRequested();
        var publication = new object();
        int next = -1;
        var workers = Enumerable.Range(0, Math.Min(Concurrency, sources.Count))
            .Select(_ => Task.Run(Scan, CancellationToken.None)).ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        void Scan()
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int index = Interlocked.Increment(ref next);
                if (index >= sources.Count) return;
                var source = sources[index];
                DiscoveryBatch batch;
                try
                {
                    var normalized = new Dictionary<string, AppRule>(StringComparer.OrdinalIgnoreCase);
                    foreach (var app in source.Find(cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var rule = ApplicationGroups.Normalize(app, cancellationToken);
                        normalized.TryAdd(rule.ExecutablePath, rule);
                    }
                    batch = new(source.Source, normalized.Values.OrderBy(x => x.Name,
                        StringComparer.CurrentCultureIgnoreCase).Take(300).ToArray(), true);
                }
                catch (Exception error) when (ExpectedSourceFailure(error))
                {
                    batch = new(source.Source, [], false);
                }
                lock (publication)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    publish(batch);
                }
            }
        }
    }

    private static bool ExpectedSourceFailure(Exception error) => error is ClientFault or Win32Exception
        or IOException or UnauthorizedAccessException or SecurityException or COMException;
}

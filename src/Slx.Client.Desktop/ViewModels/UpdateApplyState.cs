namespace Slx.Client.Desktop.ViewModels;

public enum UpdateApplyResult { Restarting, Cancelled, Failed, RebootRequired }

/// <summary>Bounded phase names; never raw worker messages, percentages or installation success.</summary>
public sealed record UpdateApplyProgress(string Phase);

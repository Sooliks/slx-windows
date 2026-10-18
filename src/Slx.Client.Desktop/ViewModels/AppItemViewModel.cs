using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Slx.Client.Core;
using Slx.Client.Desktop.Presentation;
namespace Slx.Client.Desktop.ViewModels;
public sealed class AppItemViewModel : ObservableObject
{
    private bool enabled;
    public AppItemViewModel(AppRule rule)
    {
        Id = rule.Id; Name = rule.Name; ExecutablePath = rule.ExecutablePath; enabled = rule.Enabled;
        Icon = LocalIcon.Load(rule.ExecutablePath);
    }
    public Guid Id { get; }
    public string Name { get; }
    public string ExecutablePath { get; }
    public ImageSource? Icon { get; }
    public bool Enabled { get => enabled; set { if (SetProperty(ref enabled, value)) RuleChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? RuleChanged;
    public AppRule ToRule() => new(Id, Name, ExecutablePath, Enabled);
}

using Slx.Client.Core;
using System.Windows.Media;
namespace Slx.Client.Desktop.Presentation;
public sealed record LibraryEntry(string Name,AppRule? Rule,CatalogApplication? Catalog)
{
    public string Detail=>Rule?.ExecutablePath??string.Join(", ",Catalog?.Executables??[]);
    public ImageSource Icon=>LocalIcon.Load(Rule?.ExecutablePath??"");
    public string Action=>Localization.Text.Catalog[Rule is null?"FindExecutable":"Add"];
}

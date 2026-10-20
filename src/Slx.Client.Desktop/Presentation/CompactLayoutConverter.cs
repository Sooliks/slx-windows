using System.Globalization;
using System.Windows.Data;
namespace Slx.Client.Desktop.Presentation;
public sealed class CompactLayoutConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is double width && width < 1080;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

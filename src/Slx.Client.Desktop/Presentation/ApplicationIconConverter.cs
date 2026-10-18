using System.Globalization;
using System.Windows.Data;
namespace Slx.Client.Desktop.Presentation;
public sealed class ApplicationIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => LocalIcon.Load(value as string ?? "");
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

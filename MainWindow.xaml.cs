using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TighteningController;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        Closed += (s, e) => vm.Cleanup();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)) : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ResultToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            if (s == "OK") return new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
            if (s == "NOK") return new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class AlarmActiveConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            if (targetType == typeof(Brush))
                return isActive ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) : new SolidColorBrush(Color.FromRgb(0xBD, 0xC3, 0xC7));
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

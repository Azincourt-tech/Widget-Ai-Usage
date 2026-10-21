using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AiUsageWidget.Converters;

/// <summary>
/// Converte uma cor "#RRGGBB" vinda do Core (que não conhece Avalonia) em pincel.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && Color.TryParse(hex, out var color) ? new SolidColorBrush(color) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

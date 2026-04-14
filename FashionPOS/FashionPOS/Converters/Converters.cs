using FashionPOS.Converters;

namespace FashionPOS.Converters
{
    public static class StaticConverters
    {
        public static readonly TypeMatchConverter TypeMatchConverter = new TypeMatchConverter();
        public static readonly BoolToVisibilityConverter BoolToVisibilityConverter = new BoolToVisibilityConverter();
        public static readonly InverseBoolToVisibilityConverter InverseBoolToVisibilityConverter = new InverseBoolToVisibilityConverter();
        public static readonly InverseBoolConverter InverseBoolConverter = new InverseBoolConverter();
        public static readonly StringToVisibilityConverter StringToVisibilityConverter = new StringToVisibilityConverter();
        public static readonly StockToColorConverter StockToColorConverter = new StockToColorConverter();
        public static readonly CurrencyConverter CurrencyConverter = new CurrencyConverter();
    }
}

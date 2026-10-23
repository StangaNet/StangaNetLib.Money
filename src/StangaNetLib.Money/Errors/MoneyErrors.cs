using StangaNetLib.Core.Common;

namespace StangaNetLib.Money.Errors;

internal static class MoneyErrors
{
    internal static Error Overflow()
        => Error.Validation("Amount", "Monetary arithmetic overflowed the supported minor-unit range.");

    internal static Error CurrencyMismatch(string left, string right)
        => Error.Validation("Currency", $"Cannot operate across currencies '{left}' and '{right}'.");

    internal static Error InvalidCurrency()
        => Error.Validation("Currency", "Currency code must be a non-empty ISO 4217 alphabetic code.");

    internal static Error InvalidDecimalScale()
        => Error.Validation("Amount", "Decimal amount must have at most 2 fractional digits.");

    internal static Error InvalidParse(string input)
        => Error.Validation("Amount", $"Cannot parse '{input}' as a monetary amount.");

    internal static Error InvalidParts()
        => Error.Validation("Parts", "Allocate parts must be a positive integer.");

    internal static Error InvalidRatios()
        => Error.Validation("Ratios", "Allocate ratios must be non-empty with a positive sum.");
}

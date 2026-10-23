using System.Globalization;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.Core.ValueObjects;
using StangaNetLib.Money.Errors;

namespace StangaNetLib.Money.ValueObjects;

/// <summary>
/// Monetary amount stored as integer minor units (cents) plus an ISO 4217 currency code.
/// Primary currency is <c>EUR</c>. Cross-currency arithmetic is rejected.
/// </summary>
public sealed class Money : ValueObject, IComparable<Money>
{
    /// <summary>Default currency code for factories that omit currency.</summary>
    public const string DefaultCurrency = "EUR";

    /// <summary>Creates a money value from already-validated components.</summary>
    private Money(long minorUnits, string currencyCode)
    {
        MinorUnits = minorUnits;
        CurrencyCode = currencyCode;
    }

    /// <summary>Amount in minor currency units (e.g. cents for EUR).</summary>
    public long MinorUnits { get; }

    /// <summary>ISO 4217 alphabetic currency code (upper-case).</summary>
    public string CurrencyCode { get; }

    /// <summary>Creates money from minor units.</summary>
    /// <param name="minorUnits">Amount in cents (or equivalent minor units).</param>
    /// <param name="currency">ISO 4217 code; defaults to EUR.</param>
    public static Result<Money> FromMinorUnits(long minorUnits, string currency = DefaultCurrency)
    {
        var currencyResult = NormalizeCurrency(currency);
        if (currencyResult.IsFailure)
            return Result<Money>.Failure(currencyResult.Error);

        return Result<Money>.Success(new Money(minorUnits, currencyResult.Value!));
    }

    /// <summary>
    /// Creates money from a human decimal amount (e.g. <c>12.34</c> → 1234 cents).
    /// At most two fractional digits are allowed.
    /// </summary>
    public static Result<Money> FromDecimal(decimal amount, string currency = DefaultCurrency)
    {
        var currencyResult = NormalizeCurrency(currency);
        if (currencyResult.IsFailure)
            return Result<Money>.Failure(currencyResult.Error);

        if (!HasAtMostTwoFractionalDigits(amount))
            return Result<Money>.Failure(MoneyErrors.InvalidDecimalScale());

        try
        {
            var minor = checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
            return Result<Money>.Success(new Money(minor, currencyResult.Value!));
        }
        catch (OverflowException)
        {
            return Result<Money>.Failure(MoneyErrors.Overflow());
        }
    }

    /// <summary>
    /// Parses a human amount string. Accepts both <c>12,34</c> (IT) and <c>12.34</c> forms.
    /// </summary>
    /// <param name="input">Amount text.</param>
    /// <param name="currency">Currency when not inferred; defaults to EUR.</param>
    public static Result<Money> Parse(string input, string? currency = null)
    {
        Guard.Against.NullOrWhiteSpace(input, nameof(input));

        var code = currency ?? DefaultCurrency;
        var currencyResult = NormalizeCurrency(code);
        if (currencyResult.IsFailure)
            return Result<Money>.Failure(currencyResult.Error);

        var trimmed = input.Trim();
        if (!TryParseAmount(trimmed, out var amount))
            return Result<Money>.Failure(MoneyErrors.InvalidParse(input));

        return FromDecimal(amount, currencyResult.Value!);
    }

    /// <summary>Returns the minor-unit integer amount.</summary>
    public long ToMinorUnits() => MinorUnits;

    /// <summary>Adds another amount of the same currency.</summary>
    public Result<Money> Add(Money other)
    {
        Guard.Against.Null(other, nameof(other));
        if (!SameCurrency(other))
            return Result<Money>.Failure(MoneyErrors.CurrencyMismatch(CurrencyCode, other.CurrencyCode));

        try
        {
            return Result<Money>.Success(new Money(checked(MinorUnits + other.MinorUnits), CurrencyCode));
        }
        catch (OverflowException)
        {
            return Result<Money>.Failure(MoneyErrors.Overflow());
        }
    }

    /// <summary>Subtracts another amount of the same currency.</summary>
    public Result<Money> Subtract(Money other)
    {
        Guard.Against.Null(other, nameof(other));
        if (!SameCurrency(other))
            return Result<Money>.Failure(MoneyErrors.CurrencyMismatch(CurrencyCode, other.CurrencyCode));

        try
        {
            return Result<Money>.Success(new Money(checked(MinorUnits - other.MinorUnits), CurrencyCode));
        }
        catch (OverflowException)
        {
            return Result<Money>.Failure(MoneyErrors.Overflow());
        }
    }

    /// <summary>
    /// Multiplies by a decimal factor and rounds to the nearest minor unit
    /// using <see cref="MidpointRounding.AwayFromZero"/>.
    /// </summary>
    public Result<Money> Multiply(decimal factor)
    {
        try
        {
            var product = (decimal)MinorUnits * factor;
            var rounded = decimal.Round(product, 0, MidpointRounding.AwayFromZero);
            var minor = checked((long)rounded);
            return Result<Money>.Success(new Money(minor, CurrencyCode));
        }
        catch (OverflowException)
        {
            return Result<Money>.Failure(MoneyErrors.Overflow());
        }
    }

    /// <summary>
    /// Splits this amount into <paramref name="parts"/> equal shares.
    /// Remainder cents are assigned to the first shares so no cent is lost.
    /// </summary>
    public Result<IReadOnlyList<Money>> Allocate(int parts)
    {
        if (parts <= 0)
            return Result<IReadOnlyList<Money>>.Failure(MoneyErrors.InvalidParts());

        var ratios = Enumerable.Repeat(1, parts).ToArray();
        return Allocate(ratios);
    }

    /// <summary>
    /// Splits this amount by positive integer ratios. Remainder cents go to the earliest shares.
    /// </summary>
    public Result<IReadOnlyList<Money>> Allocate(IReadOnlyList<int> ratios)
    {
        Guard.Against.Null(ratios, nameof(ratios));
        if (ratios.Count == 0 || ratios.Any(r => r < 0) || ratios.Sum() <= 0)
            return Result<IReadOnlyList<Money>>.Failure(MoneyErrors.InvalidRatios());

        var total = ratios.Sum();
        var results = new Money[ratios.Count];
        long allocated = 0;

        for (var i = 0; i < ratios.Count; i++)
        {
            var share = MinorUnits * ratios[i] / total;
            results[i] = new Money(share, CurrencyCode);
            allocated += share;
        }

        var remainder = MinorUnits - allocated;
        for (var i = 0; remainder != 0 && i < results.Length; i++)
        {
            if (ratios[i] == 0)
                continue;

            var step = remainder > 0 ? 1L : -1L;
            results[i] = new Money(results[i].MinorUnits + step, CurrencyCode);
            remainder -= step;
        }

        return Result<IReadOnlyList<Money>>.Success(results);
    }

    /// <summary>Compares two amounts of the same currency by minor units.</summary>
    public int CompareTo(Money? other)
    {
        if (other is null)
            return 1;
        if (!SameCurrency(other))
            throw new InvalidOperationException(
                $"Cannot compare currencies '{CurrencyCode}' and '{other.CurrencyCode}'.");

        return MinorUnits.CompareTo(other.MinorUnits);
    }

    /// <summary>Formats for display; defaults to Italian culture with currency symbol.</summary>
    public string Format(CultureInfo? culture = null)
    {
        culture ??= CultureInfo.GetCultureInfo("it-IT");
        var amount = MinorUnits / 100m;
        return amount.ToString("C", culture);
    }

    /// <inheritdoc />
    public override string ToString() => Format();

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinorUnits;
        yield return CurrencyCode;
    }

    /// <summary>Less-than comparison (same currency).</summary>
    public static bool operator <(Money left, Money right) => Compare(left, right) < 0;

    /// <summary>Greater-than comparison (same currency).</summary>
    public static bool operator >(Money left, Money right) => Compare(left, right) > 0;

    /// <summary>Less-than-or-equal comparison (same currency).</summary>
    public static bool operator <=(Money left, Money right) => Compare(left, right) <= 0;

    /// <summary>Greater-than-or-equal comparison (same currency).</summary>
    public static bool operator >=(Money left, Money right) => Compare(left, right) >= 0;

    private static int Compare(Money left, Money right)
    {
        Guard.Against.Null(left, nameof(left));
        Guard.Against.Null(right, nameof(right));
        return left.CompareTo(right);
    }

    private bool SameCurrency(Money other) =>
        string.Equals(CurrencyCode, other.CurrencyCode, StringComparison.Ordinal);

    private static Result<string> NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return Result<string>.Failure(MoneyErrors.InvalidCurrency());

        var code = currency.Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetter))
            return Result<string>.Failure(MoneyErrors.InvalidCurrency());

        return Result<string>.Success(code);
    }

    private static bool HasAtMostTwoFractionalDigits(decimal amount)
    {
        var scaled = amount * 100m;
        return scaled == decimal.Truncate(scaled);
    }

    private static bool TryParseAmount(string input, out decimal amount)
    {
        amount = 0m;

        // Prefer invariant when a single '.' looks like a decimal separator.
        if (input.Contains('.') && !input.Contains(','))
        {
            return decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        }

        // Italian / European: comma as decimal separator (optional thousand dots).
        if (input.Contains(','))
        {
            var normalized = input.Replace(".", string.Empty).Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        }

        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
               || decimal.TryParse(input, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out amount);
    }
}

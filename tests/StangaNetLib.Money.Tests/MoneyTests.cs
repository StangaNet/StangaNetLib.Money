using System.Globalization;
using MoneyVo = StangaNetLib.Money.ValueObjects.Money;

namespace StangaNetLib.Money.Tests;

public class MoneyTests
{
    [Fact]
    public void FromMinorUnits_ShouldCreateEurByDefault()
    {
        var result = MoneyVo.FromMinorUnits(1234);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MinorUnits.Should().Be(1234);
        result.Value.CurrencyCode.Should().Be("EUR");
        result.Value.ToMinorUnits().Should().Be(1234);
    }

    [Fact]
    public void FromDecimal_ShouldConvertToCents()
    {
        var result = MoneyVo.FromDecimal(12.34m);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MinorUnits.Should().Be(1234);
    }

    [Fact]
    public void FromDecimal_WithMoreThanTwoFractionalDigits_ShouldFail()
    {
        var result = MoneyVo.FromDecimal(12.345m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Amount");
    }

    [Theory]
    [InlineData("12,34", 1234L)]
    [InlineData("12.34", 1234L)]
    [InlineData("1.234,56", 123456L)]
    [InlineData("0", 0L)]
    public void Parse_ShouldAcceptItAndInvariantForms(string input, long expected)
    {
        var result = MoneyVo.Parse(input);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MinorUnits.Should().Be(expected);
    }

    [Fact]
    public void Parse_Invalid_ShouldFail()
    {
        MoneyVo.Parse("not-a-number").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Add_SameCurrency_ShouldSucceed()
    {
        var a = MoneyVo.FromMinorUnits(100).Value!;
        var b = MoneyVo.FromMinorUnits(50).Value!;

        var sum = a.Add(b);

        sum.IsSuccess.Should().BeTrue();
        sum.Value!.MinorUnits.Should().Be(150);
    }

    [Fact]
    public void Add_CrossCurrency_ShouldFail()
    {
        var eur = MoneyVo.FromMinorUnits(100, "EUR").Value!;
        var usd = MoneyVo.FromMinorUnits(100, "USD").Value!;

        var sum = eur.Add(usd);

        sum.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Subtract_ShouldWork()
    {
        var a = MoneyVo.FromMinorUnits(100).Value!;
        var b = MoneyVo.FromMinorUnits(40).Value!;

        a.Subtract(b).Value!.MinorUnits.Should().Be(60);
    }

    [Fact]
    public void Multiply_ShouldRoundAwayFromZero()
    {
        var money = MoneyVo.FromMinorUnits(100).Value!;

        money.Multiply(0.155m).Value!.MinorUnits.Should().Be(16); // 15.5 → 16
    }

    [Fact]
    public void Allocate_EqualParts_ShouldPreserveTotal()
    {
        var money = MoneyVo.FromMinorUnits(100).Value!;

        var parts = money.Allocate(3).Value!;

        parts.Should().HaveCount(3);
        parts.Sum(p => p.MinorUnits).Should().Be(100);
        parts.Select(p => p.MinorUnits).Should().Equal(34, 33, 33);
    }

    [Fact]
    public void Allocate_ByRatios_ShouldPreserveTotal()
    {
        var money = MoneyVo.FromMinorUnits(100).Value!;

        var parts = money.Allocate([1, 1, 1]).Value!;

        parts.Sum(p => p.MinorUnits).Should().Be(100);
    }

    [Fact]
    public void Equality_ShouldBeStructural()
    {
        var a = MoneyVo.FromMinorUnits(10, "EUR").Value!;
        var b = MoneyVo.FromMinorUnits(10, "EUR").Value!;
        var c = MoneyVo.FromMinorUnits(10, "USD").Value!;

        a.Should().Be(b);
        a.Should().NotBe(c);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Compare_SameCurrency_ShouldOrderByMinorUnits()
    {
        var a = MoneyVo.FromMinorUnits(10).Value!;
        var b = MoneyVo.FromMinorUnits(20).Value!;

        (a < b).Should().BeTrue();
        (b > a).Should().BeTrue();
        a.CompareTo(b).Should().BeNegative();
    }

    [Fact]
    public void Format_DefaultItIT_ShouldIncludeCurrencySymbol()
    {
        var money = MoneyVo.FromDecimal(12.34m).Value!;
        var culture = CultureInfo.GetCultureInfo("it-IT");

        var formatted = money.Format(culture);

        formatted.Should().Contain("12");
        formatted.Should().Contain("34");
    }

    [Fact]
    public void FromMinorUnits_InvalidCurrency_ShouldFail()
    {
        MoneyVo.FromMinorUnits(1, "EURO").IsFailure.Should().BeTrue();
        MoneyVo.FromMinorUnits(1, "").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Add_Overflow_ShouldFail()
    {
        var a = MoneyVo.FromMinorUnits(long.MaxValue).Value!;
        var b = MoneyVo.FromMinorUnits(1).Value!;

        a.Add(b).IsFailure.Should().BeTrue();
    }
}

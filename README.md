# StangaNetLib.Money

[![.NET CI](https://github.com/StangaNet/StangaNetLib.Money/actions/workflows/main.yml/badge.svg)](https://github.com/StangaNet/StangaNetLib.Money/actions/workflows/main.yml)
![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4)

Safe monetary primitives for .NET. Amounts live as **integer minor units** (cents) — never `float`/`double`. Failures surface as `Result` / `Result<T>` from [`StangaNetLib.Core`](https://github.com/StangaNet/StangaNetLib.Core).

## Installation

Hosted on **GitHub Packages**:

```xml
<PackageReference Include="StangaNetLib.Money" Version="1.0.0" />
```

See [CHANGELOG.md](./CHANGELOG.md) for release history.

## Core Components

| Namespace | Components | Purpose |
|---|---|---|
| `StangaNetLib.Money.ValueObjects` | `Money` | Minor-unit value object with EUR-first arithmetic |

## Quick Start

```csharp
var price = Money.FromDecimal(12.34m).Value!;          // 1234 cents EUR
var tax = price.Multiply(0.22m).Value!;
var total = price.Add(tax).Value!;

var shares = total.Allocate(3).Value!;                 // remainder to first shares
Console.WriteLine(total.Format());                     // it-IT currency format
```

## Design notes

- **No floating point** for amounts; storage is `long` minor units.
- **Primary currency: EUR.** Other ISO 4217 codes are allowed but arithmetic across currencies fails with a validation `Result`.
- **No FX / exchange rates** in v1.
- Allocate / split never loses cents (remainder assigned to earliest shares).

## License

Apache-2.0. See [LICENSE](./LICENSE).

---
name: newtonsoft-json
description: >
  Newtonsoft.Json (Json.NET) for .NET — serialization, deserialization, LINQ to JSON,
  attribute configuration, custom converters, and migration from System.Text.Json.
  Load this skill when working with JSON serialization in .NET, or when the user
  mentions "Newtonsoft.Json", "JsonSerializer", "JsonProperty", "JsonConvert",
  "LINQ to JSON", "JObject", "custom converter", or "JSON serialization".
---

# Newtonsoft.Json (Json.NET)

## Core Principles

1. **Newtonsoft.Json is the battle-tested default** — 1B+ NuGet downloads. Use it unless .NET 10's built-in `System.Text.Json` covers your needs (and it rarely does for complex projects).
2. **Settings once, reuse everywhere** — Configure `JsonSerializerSettings` globally in `Program.cs`; don't configure per-call.
3. **Attributes over fluent API** — `[JsonProperty]` is more discoverable and self-documenting than builder patterns.
4. **LINQ to JSON for dynamic payloads** — `JObject` / `JArray` for schema-less or hybrid JSON consumption.

## Patterns

### Global Configuration

```csharp
// Program.cs — single source of truth
var jsonSettings = new JsonSerializerSettings
{
    NullValueHandling = NullValueHandling.Ignore,
    DefaultValueHandling = DefaultValueHandling.Populate,
    ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
    DateParseHandling = DateParseHandling.DateTimeOffset,
    Formatting = Formatting.Indented,
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    Converters =
    {
        new StringEnumConverter { CamelCaseNames = true },
        new IsoDateTimeConverter { DateTimeFormat = "yyyy-MM-dd" }
    }
};

// In ASP.NET Core, register as singleton and use via IHttpContextAccessor
builder.Services.AddSingleton(jsonSettings);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

### Basic Serialize / Deserialize

```csharp
// Serialize
string json = JsonConvert.SerializeObject(order, Formatting.Indented, jsonSettings);

// Deserialize
var order = JsonConvert.DeserializeObject<Order>(json, jsonSettings);

// Deserialized to new instance (always creates new object)
var order = JsonConvert.DeserializeObject<Order>(json, jsonSettings);
```

### Attribute-Based Configuration

```csharp
public class Order
{
    [JsonProperty("order_id")]
    public Guid Id { get; set; }

    [JsonProperty("customer_name", NullValueHandling = NullValueHandling.Include)]
    public string? CustomerName { get; set; }

    [JsonProperty("created_at")]
    [JsonConverter(typeof(IsoDateTimeConverter), "yyyy-MM-ddTHH:mm:ssZ")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("items")]
    public List<OrderItem> Items { get; set; } = new();

    [JsonIgnore]
    public decimal ComputedTotal => Items.Sum(i => i.Price * i.Quantity);
}
```

### LINQ to JSON (Dynamic/Hybrid)

```csharp
// Parse without a type
JObject jObj = JObject.Parse(json);
string name = (string)jObj["customer_name"];
int count = (int)jObj["items"]!.Count();

// Navigate nested
var total = (decimal)jObj["items"]![0]["price"];

// Build dynamically
var response = new JObject
{
    ["status"] = "ok",
    ["data"] = new JObject
    {
        ["id"] = orderId,
        ["name"] = orderName
    }
};
```

### Custom Converter

```csharp
public class MoneyConverter : JsonConverter<Money>
{
    public override Money ReadJson(JsonReader reader, Type objectType, Money existing,
        bool hasExistingValue, JsonSerializer serializer)
    {
        return new Money((decimal)reader.Value!, reader.GetString()?.ToUpper() ?? "USD");
    }

    public override void WriteJson(JsonWriter writer, Money value, JsonSerializer serializer)
    {
        writer.WriteValue(value.Amount);
        writer.WritePropertyName("currency");
        writer.WriteValue(value.Currency);
    }
}

// Register
jsonSettings.Converters.Add(new MoneyConverter());
```

### Handling Reference Loops

```csharp
// Option 1: Preserve references (useful for serialization round-trips)
jsonSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
// Produces $id / $ref in JSON

// Option 2: Ignore and break the cycle
jsonSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

// Option 3: Use [JsonIgnore] on the back-reference
public class Customer
{
    public List<Order> Orders { get; set; } = new();
}
public class Order
{
    [JsonIgnore]
    public Customer Customer { get; set; } = null!;
    public Guid CustomerId { get; set; }
}
```

## Anti-patterns

### Don't Use JsonSerializerSettings Per-Call

```csharp
// BAD —每次都创建新设置，性能差且不一致
var settings = new JsonSerializerSettings { ... };
var json = JsonConvert.SerializeObject(obj, settings);

// GOOD — global singleton
builder.Services.AddSingleton(CreateSettings());
```

### Don't Serialize Sensitive Data

```csharp
// BAD — serializing the full entity including password hash
var json = JsonConvert.SerializeObject(user);

// GOOD — use a dedicated DTO
var json = JsonConvert.SerializeObject(user.ToPublicDto(), settings);
```

### Don't Mix JsonConvert.DeserializeObject with Dynamic

```csharp
// BAD — type confusion between JObject and strongly-typed
var obj = JsonConvert.DeserializeObject<dynamic>(json); // avoid

// GOOD — use JObject for dynamic, concrete types for known schemas
var jObj = JObject.Parse(json);
var order = JsonConvert.DeserializeObject<Order>(json, settings);
```

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| Simple CRUD API with known schema | System.Text.Json (built-in) |
| Complex nested object graphs | Newtonsoft.Json with PreserveReferencesHandling |
| API consuming third-party JSON with varying schemas | JObject / LINQ to JSON |
| Custom serialization (Money, DomainIds, etc.) | Custom `JsonConverter<T>` |
| Legacy .NET Framework project | Newtonsoft.Json (no alternative) |
| GraphQL response parsing | JObject |
| High-throughput microsecond-critical path | System.Text.Json (faster than Newtonsoft) |
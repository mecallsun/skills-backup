---
name: polly
description: >
  Polly resilience library for .NET. Covers Retry, Circuit Breaker, Timeout,
  Fallback, Bulkhead Isolation, Rate Limiter, and Policy composition.
  Load this skill when building resilient HTTP clients, database connections,
  or external service calls in .NET, or when the user mentions "Polly",
  "retry", "circuit breaker", "resilience", "fallback", "rate limit",
  "bulkhead", "timeout", "TransientFaultHandling", or "Hedging".
---

# Polly

## Core Principles

1. **Polly is for transient failures, not business logic** — Retry on network blips, not on validation errors. A retrying request that fails because of bad input will just fail again.
2. **Policy composition over monoliths** — Chain `Retry` → `CircuitBreaker` → `Fallback` to express intent at every layer. Don't try to do everything in one policy.
3. **Async-first, sync-safe** — Use the `.Async` APIs. Polly's synchronous APIs are maintained for legacy but async is the primary path in .NET 8+.
4. **Telemetry matters** — Every policy should report metrics (via `Polly.Metrics` or `Microsoft.Extensions.Resilience`) so you can distinguish "throttled" from "broken".

## Patterns

### Basic Retry with Backoff

```csharp
// Exponential backoff with jitter — the gold standard
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .OrHandle<TimeoutException>()
    .WaitAndRetryAsync(3, attempt =>
        TimeSpan.FromSeconds(Math.Pow(2, attempt))
            .Add(TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500))),
        onRetry: (outcome, elapsed, retryCount, context) =>
        {
            logger.LogWarning(
                "Call to {Operation} failed (attempt {RetryCount}/{MaxRetries}), elapsed {Elapsed}s",
                context.OperationKey, retryCount, 3, elapsed.TotalSeconds);
        });
```

### Circuit Breaker

```csharp
// Opens after 5 failures in 30s; auto-recovers after 10s
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(10),
        onBreak: (ex, breakDelay, ctx) =>
            logger.LogWarning("Circuit OPEN for {Operation} — {ExceptionType}", ctx.OperationKey, ex.GetType().Name),
        onReset: ctx =>
            logger.LogInformation("Circuit CLOSED for {Operation}", ctx.OperationKey));
```

### Policy Chain (Retry + Circuit Breaker + Fallback)

```csharp
// Compose: retry first, then circuit, then fallback
var resilientPipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,        // 50% failure rate
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 10,    // don't trip on low traffic
        BreakDuration = TimeSpan.FromSeconds(10),
    })
    .AddFallback(new FallbackStrategyOptions
    {
        FallbackAction = async (ctx, ct) =>
        {
            logger.LogWarning("Using fallback for {Operation}", ctx.OperationKey);
            return new OrderResponse(Id: Guid.Empty, Status: OrderStatus.Fallback);
        },
    })
    .Build();

// Execute
var result = await resilientPipeline.ExecuteAsync(async ctx =>
{
    var response = await httpClient.GetAsync("/api/orders", ctx.CancellationToken);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<OrderResponse>(ctx.CancellationToken);
}, new ExecutionContext { OperationKey = "GetOrder" });
```

### Typed Resilience Pipeline (Microsoft.Extensions.Resilience)

```csharp
// Registration in DI — recommended for ASP.NET Core apps
builder.Services.AddResiliencePipeline("order-api", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions<ResilienceContext>
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ResilienceContext>
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(10),
        });
});

// Consumption
[FromKeyedServices("order-api")] ResiliencePipeline orderPipeline;

// In a handler
var result = await orderPipeline.ExecuteAsync(async context =>
{
    var resp = await client.GetAsync("/orders", context.CancellationToken);
    return await resp.Content.ReadFromJsonAsync<Order[]>(context.CancellationToken);
}, new ResilienceContext { OperationKey = "GetOrders" });
```

### IHttpClientFactory Integration

```csharp
builder.Services
    .AddHttpClient<OrderClient>()
    .AddStandardResilienceHandler()   // Microsoft.Extensions.Http.Resilience
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
```

### Timeout Policy

```csharp
// Hard timeout — throws OperationCanceledException when exceeded
var timeoutPolicy = Policy.TimeoutAsync(
    TimeSpan.FromSeconds(30),
    TimeoutStrategy.Pessimistic,   // waits for current operation to complete
    onTimeout: (ctx, span, task) =>
        logger.LogWarning("Request timed out after {Span}ms", span.TotalMilliseconds));
```

### Bulkhead Isolation

```csharp
// Limit concurrent executions to prevent cascading failure
var bulkheadPolicy = Policy.BulkheadAsync(
    maxParallelization: 20,
    maxQueuingActions: 10);

// Used with IHttpClientFactory
services.AddHttpClient()
    .AddPolicyHandler(bulkheadPolicy);
```

## Anti-patterns

### Don't Retry Idempotent Writes Without Caution

```csharp
// BAD — retrying a POST that creates an order may create duplicates
await Policy.Handle<HttpRequestException>().RetryAsync().ExecuteAsync(async () =>
    await client.PostAsync("/api/orders", body));

// GOOD — add idempotency key or use a query for idempotent operations
await Policy.Handle<HttpRequestException>().RetryAsync().ExecuteAsync(async () =>
    await client.PutAsync($"/api/orders?IdempotencyKey={guid}", body));
```

### Don't Catch and Swallow

```csharp
// BAD — swallowing exceptions in onRetry defeats resilience
.onRetry = (_, __) => { /* silent */ }

// GOOD — log and let the exception propagate if all retries fail
.onRetry = (outcome, elapsed, retries) =>
    logger.LogWarning("Retry {n}: {Exception}", retries, outcome.Exception?.Message);
```

### Don't Use Too Many Retries

```csharp
// BAD — 10 retries means 30+ seconds of waiting
.WaitAndRetryAsync(10, _ => TimeSpan.FromSeconds(1));

// GOOD — 3 attempts is the sweet spot for most HTTP calls
.WaitAndRetryAsync(3, attempt =>
    TimeSpan.FromSeconds(Math.Pow(2, attempt)).Add(Jitter));
```

### Don't Mix Circuit Breaker on Every Call

```csharp
// BAD — circuit breaker on a fire-and-forget background job
// (noise in metrics, no impact on caller)

// GOOD — circuit breaker where it matters: HTTP calls to external services
```

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| Transient HTTP failure | Retry with exponential backoff + jitter |
| Downstream service degraded | Circuit breaker + fallback |
| External dependency overload | Bulkhead isolation |
| Slow response | Timeout policy (pessimistic) |
| Multiple policies needed | Compose with `ResiliencePipelineBuilder` |
| ASP.NET Core app | `Microsoft.Extensions.Resilience` + DI registration |
| Simple script/CLI | Standalone `Policy` class |
| Long-running background job | Hedging policy (parallel attempts) |
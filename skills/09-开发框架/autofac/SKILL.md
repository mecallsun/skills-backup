---
name: autofac
description: >
  Autofac IoC/DI container for .NET. Covers registration patterns, lifetime scopes,
  module composition, property injection, interception/AOP, and migration from built-in DI.
  Load this skill when working with advanced dependency injection in .NET, or when the
  user mentions "Autofac", "IoC container", "lifetime scope", "property injection",
  "module", "AOP interceptor", "container builder", or "advanced DI".
---

# Autofac

## Core Principles

1. **Built-in DI is sufficient for most cases** — Use `IServiceProvider` first; reach for Autofac only when you need what it provides (lifetime scopes, property injection, interception).
2. **Modules over builder chaining** — Group related registrations in `Module` subclasses for testability and reusability.
3. **LifetimeScope is the unit of work** — Each web request, background job, or message handler should have its own `LifetimeScope`; register as `InstancePerLifetimeScope` or `Owned<T>`.
4. **Override, don't replace** — Use `OverrideRegistration` for test doubles; don't rebuild the entire container.

## Patterns

### Basic Module Registration

```csharp
public class DataModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Singleton — one instance per container
        builder.RegisterType<AppDbContext>()
            .As<IAppDbContext>()
            .SingleInstance();

        // Scoped — one instance per LifetimeScope (HTTP request)
        builder.RegisterType<OrderRepository>()
            .As<IOrderRepository>()
            .InstancePerLifetimeScope();

        // Transient — new instance every resolution
        builder.RegisterType<PricingService>()
            .As<IPricingService>()
            .InstancePerDependency();
    }
}
```

### Container Setup with ASP.NET Core Integration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register built-in services first
builder.Services.AddControllers();
builder.Services.AddApplicationServices(); // your own extension

// Build the Autofac container
var container = new ContainerBuilder()
    .RegisterModule(new DataModule())
    .RegisterModule(new ApplicationModule())
    .Populate(builder.Services)   // merge built-in DI
    .Build();

// Replace the default IServiceProvider
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory(container));
```

### Property Injection (When Constructor Injection Isn't Enough)

```csharp
// Class
public class ReportService
{
    [Inject]
    public ILogger<ReportService> Logger { get; set; } = null!;

    [Inject]
    public ICacheService Cache { get; set; } = null!;
}

// Module — opt-in per-property
builder.RegisterType<ReportService>()
    .As<IReportService>()
    .InstancePerLifetimeScope()
    .PropertiesAutowired();
```

### Owned<T> for Disposables Within a Scope

```csharp
// Module
builder.RegisterType<EmailSender>()
    .As<IEmailSender>()
    .InstancePerOwned<ITenantContext>();

// Consumer
public class TenantWorker(ITenantContext context, IOwnedServiceFactory factory)
{
    public async Task ProcessAsync(Guid tenantId)
    {
        using var owned = factory.CreateOwned<ITenantContext>(ctx =>
        {
            ctx.TenantId = tenantId;
        });
        await owned.Value.SendNotificationAsync("welcome");
    }
}
```

### AOP Interception

```csharp
// Define an interceptor
public class LoggingInterceptor : IInterceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;
    public LoggingInterceptor(ILogger<LoggingInterceptor> logger) => _logger = logger;

    public void Intercept(IInvocation invocation)
    {
        _logger.LogInformation("Calling {Method}", invocation.Method.Name);
        invocation.Proceed();
        _logger.LogInformation("Completed {Method}", invocation.Method.Name);
    }
}

// Register with proxy generation
builder.RegisterType<LoggingInterceptor>()
    .AsSelf()
    .SingleInstance();

builder.RegisterType<OrderService>()
    .As<IOrderService>()
    .InstancePerLifetimeScope()
    .EnableInterfaceInterceptors()      // or .EnableClassInterceptors()
    .InterceptedBy(typeof(LoggingInterceptor));
```

### Factory Pattern with Autofac

```csharp
// Func<T> registration
builder.Register(c =>
{
    var scope = c.Resolve<IDependencyScope>();
    return (Func<string, IProcessor>)name =>
        scope.ResolveKeyed<IProcessor>(name);
}).As<Func<string, IProcessor>>();

// keyed registration
builder.RegisterType<OrderProcessor>()
    .As<IProcessor>()
    .Keyed<IProcessor>("order");

builder.RegisterType<InventoryProcessor>()
    .As<IProcessor>()
    .Keyed<IProcessor>("inventory");
```

## Anti-patterns

### Don't Use Autofac as a Service Locator

```csharp
// BAD — service locator anti-pattern
var service = container.Resolve<ISomeService>(); // where container is stored globally

// GOOD — inject explicitly
public class Worker(ISomeService service) { ... }
```

### Don't Overuse Property Injection

```csharp
// BAD — hard to test, hidden dependencies
[Inject] public IDependency A { get; set; }
[Inject] public IDependency B { get; set; }

// GOOD — prefer constructor injection
public class Service(IDependency a, IDependency b) { ... }
```

### Don't Create a New Container Per Request

```csharp
// BAD — creating container inside request handler is expensive
var container = new ContainerBuilder().Build(); // each request = new container

// GOOD — one container, many LifetimeScopes
// Container is singleton; ASP.NET Core integration handles scopes automatically
```

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| Standard web API DI | Built-in `IServiceProvider` |
| Need property injection | Autofac `.PropertiesAutowired()` |
| Need interception/AOP | Autofac `EnableInterfaceInterceptors()` |
| Need keyed/conditional resolution | Autofac `.Keyed<T>()` |
| Need custom factory | Autofac `.Register<Func<T>>()` |
| Unit testing with mocks | Autofac `.OverrideRegistration()` |
| Simple console app DI | Built-in or Scrutor |
| .NET MAUI / Uno / minimal API | Built-in is usually enough |
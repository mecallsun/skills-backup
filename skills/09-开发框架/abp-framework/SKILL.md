---
name: abp-framework
description: >
  ABP Framework — full-stack open-source application framework for ASP.NET Core.
  Covers modular architecture, DDD templates, permission/tenant system,
  entity extension, and module lifecycle.
  Load this skill when building enterprise applications with ABP, or when the
  user mentions "ABP Framework", "abp.io", "modular architecture", "tenant isolation",
  "permission system", "entity extension", "module zero", "DDD template",
  "AbpApplicationInitialization", or "Application Service".
---

# ABP Framework

## Core Principles

1. **ABP is an application framework, not a library** — It provides end-to-end conventions for controllers, application services, domain services, and persistence — all opinionated but extensible.
2. **Modularity over monoliths** — Every feature belongs in a module; modules compose at runtime. This enables microservice boundaries without losing shared infrastructure.
3. **DDD-first by convention** — Entities, value objects, domain services, and aggregates are first-class citizens. Don't fight the framework; follow its layered structure.
4. **Tenant isolation is a feature, not an afterthought** — Every entity can be multi-tenant out of the box. Configure it early; retrofitting is painful.

## Patterns

### Module Structure

```
MyApp.Domain          ← entities, value objects, domain services
MyApp.Application     ← application services, DTOs, command handlers
MyApp.Application.Contracts ← DTOs + interfaces (shared between tiers)
MyApp.EntityFrameworkCore  ← repository implementations, EF Core config
MyApp.HttpApi         ← controllers, model binding
MyApp.HttpApi.Host    ← web host (Kestrel, Swagger, DI)
```

### Application Service (CRUD API)

```csharp
// MyApp.Application/Orders/OrderAppService.cs
public class OrderAppService : ApplicationService, IOrderAppService
{
    private readonly IRepository<Order, Guid> _orderRepo;
    private readonly IOrderDomainService _orderDomainService;

    public OrderAppService(
        IRepository<Order, Guid> orderRepo,
        IOrderDomainService orderDomainService)
    {
        _orderRepo = orderRepo;
        _orderDomainService = orderDomainService;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto input)
    {
        var order = await _orderDomainService.CreateOrderAsync(
            input.CustomerId, input.Items);

        await _orderRepo.InsertAsync(order, autoSave: true);
        return ObjectMapper.Map<Order, OrderDto>(order);
    }

    public async Task<PagedResultDto<OrderDto>> GetListAsync(
        PagedAndSortedInput input, string? filter = null)
    {
        var query = _orderRepo.GetQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
            query = query.Where(o => o.CustomerName.Contains(filter));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<OrderDto>
        {
            Items = ObjectMapper.Map<List<Order>, List<OrderDto>>(items),
            TotalCount = totalCount
        };
    }
}
```

### Multi-Tenancy Configuration

```csharp
// In the domain module
public class MyAppDomainModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // Enable multi-tenancy globally
        PreConfigure<AbpMultiTenancyOptions>(options =>
            options.IsEnabled = true);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddDbContext<MyAppDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString);
            // Auto-filter by TenantId for all entities implementing IMultiTenant
            options.AddAbp<MyAppDbContext>(new AbpDbContextConfiguringOptions());
        });
    }
}

// Entity automatically gets TenantId
public class Order : AggregateRoot<Guid>, IMultiTenant
{
    public Guid TenantId { get; set; }
    // ...
}
```

### Entity Extension (No Model Modification)

```csharp
// Extend existing ABP entities without touching the original class
public class MyCustomerExtension : EntityExtensionModule
{
    public override void Configure(EntityConfigurationContext context)
    {
        context.Configure<EasyAbp.IdentityServer.User, AbpUser<Guid>>(
            configuration =>
            {
                configuration.AddOrUpdateProperty<string>(
                    "PhoneNumber",
                    configuration => configuration
                        .HasMaxLength(20)
                        .IsRequired());
            });
    }
}
```

### Permission System

```csharp
// Define permissions in a module
public class MyAppPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myApp = context.AddGroup("MyApp", L("Permission:MyApp"));

        myApp.AddPermission(MyAppPermissions.Orders.Read, L("Permission:Orders.Read"));
        myApp.AddPermission(MyAppPermissions.Orders.Create, L("Permission:Orders.Create"));
        myApp.AddPermission(MyAppPermissions.Orders.Delete, L("Permission:Orders.Delete"));
    }
}

// Use in application service
[Authorize(MyAppPermissions.Orders.Delete)]
public async Task DeleteAsync(Guid id) { ... }
```

### Background Job

```csharp
// IBackgroundJobManager for deferred execution
public class OrderBackgroundJob : IBackgroundJobHandler
{
    public async Task ExecuteAsync(BackgroundJobArgs args)
    {
        var orderId = Guid.Parse(args.Properties["orderId"]);
        // Process order asynchronously
        await ProcessOrderAsync(orderId);
    }
}

// Trigger from application service
await _backgroundJobManager.EnqueueAsync<OrderBackgroundJob>(
    new Dictionary<string, object> { ["orderId"] = orderId.ToString() });
```

## Anti-patterns

### Don't Mix Application and Domain Logic

```csharp
// BAD — business logic in the application service layer
public async Task CreateOrder(CreateOrderDto input)
{
    var order = new Order(...);  // domain logic in app service
    order.Validate();             // should be in domain service
    await _repo.InsertAsync(order);
}

// GOOD — delegate to domain service
public async Task CreateOrder(CreateOrderDto input)
{
    var order = await _domainService.CreateOrderAsync(input.CustomerId, input.Items);
    await _repo.InsertAsync(order, autoSave: true);
}
```

### Don't Bypass the Repository

```csharp
// BAD — direct DbContext access in application service
await _dbContext.Orders.Where(...).ToListAsync();

// GOOD — use the repository abstraction
await _orderRepo.GetListAsync(...);
```

### Don't Create Custom Filters for Everything

```csharp
// BAD — recreating ABP's built-in filtering
// ABP already provides PagedAndSortedResultRequestDto, filtering, sorting

// GOOD — use ABP's built-in pagination/filtering
public async Task GetListAsync(PagedAndSortedInput input, string? filter = null)
```

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| New greenfield enterprise app | ABP Framework + Angular/Blazor UI |
| Simple CRUD microservice | Minimal APIs + EF Core (no ABP) |
| Existing ASP.NET Core project adding modularity | ABP modules as plugins |
| Multi-tenant SaaS | ABP (tenant management built-in) |
| Single-tenant internal tool | Consider skipping ABP overhead |
| Need identity/permission system | ABP Identity (pre-built, configurable) |
| Need background jobs | ABP Background Jobs or Hangfire |
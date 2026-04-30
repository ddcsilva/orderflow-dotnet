# Fase 3 — CQRS com MediatR e Application Layer

> **Objetivo:** Implementar a camada Application do Orders API usando CQRS com MediatR, Pipeline Behaviors, Result Pattern, e separação de leitura (Dapper) / escrita (EF Core).

> **Pré-requisito:** Fase 2 concluída (domínio rico com aggregate, value objects, domain events).

### 🎯 O que você vai aprender nesta fase

- Separar **Commands** (escrita) de **Queries** (leitura) com CQRS
- Implementar handlers com **MediatR** e injeção automática
- Criar **Pipeline Behaviors** para validação, logging e tratamento de erros
- Aplicar o **Result Pattern** para fluxo de erros sem exceções
- Usar **Dapper** para queries de leitura performáticas
- Configurar **EF Core** com interceptors para dispatch de domain events
- Validar Commands com **FluentValidation**

---

## Sumário

1. [Visão da Fase](#1-visão-da-fase)
2. [Decisões Arquiteturais](#2-decisões-arquiteturais)
3. [Conceitos Chave](#3-conceitos-chave)
4. [Passo a Passo de Implementação](#4-passo-a-passo-de-implementação)
5. [Código de Referência Completo](#5-código-de-referência-completo)
6. [Testes](#6-testes)
7. [Checkpoint](#7-checkpoint)

---

## 1. Visão da Fase

### O Que Vamos Construir

```
src/Services/Orders/
├── OrderFlow.Orders.Application/
│   ├── Common/
│   │   ├── Interfaces/
│   │   │   ├── IOrderReadRepository.cs     # Read side (Dapper)
│   │   │   └── ICurrentUserService.cs
│   │   ├── Behaviors/
│   │   │   ├── ValidationBehavior.cs       # FluentValidation pipeline
│   │   │   ├── LoggingBehavior.cs          # Request/Response logging
│   │   │   └── TransactionBehavior.cs      # Unit of Work per request
│   │   ├── Result.cs                       # Result Pattern
│   │   └── Error.cs
│   ├── Orders/
│   │   ├── Commands/
│   │   │   ├── CreateOrder/
│   │   │   │   ├── CreateOrderCommand.cs
│   │   │   │   ├── CreateOrderCommandHandler.cs
│   │   │   │   └── CreateOrderCommandValidator.cs
│   │   │   ├── AddOrderItem/
│   │   │   │   ├── AddOrderItemCommand.cs
│   │   │   │   ├── AddOrderItemCommandHandler.cs
│   │   │   │   └── AddOrderItemCommandValidator.cs
│   │   │   ├── ConfirmOrder/
│   │   │   │   ├── ConfirmOrderCommand.cs
│   │   │   │   └── ConfirmOrderCommandHandler.cs
│   │   │   └── CancelOrder/
│   │   │       ├── CancelOrderCommand.cs
│   │   │       ├── CancelOrderCommandHandler.cs
│   │   │       └── CancelOrderCommandValidator.cs
│   │   ├── Queries/
│   │   │   ├── GetOrderById/
│   │   │   │   ├── GetOrderByIdQuery.cs
│   │   │   │   ├── GetOrderByIdQueryHandler.cs
│   │   │   │   └── OrderDetailDto.cs
│   │   │   └── GetOrdersByCustomer/
│   │   │       ├── GetOrdersByCustomerQuery.cs
│   │   │       ├── GetOrdersByCustomerQueryHandler.cs
│   │   │       └── OrderSummaryDto.cs
│   │   └── EventHandlers/
│   │       └── OrderCreatedDomainEventHandler.cs
│   └── DependencyInjection.cs
│
├── OrderFlow.Orders.Infrastructure/
│   ├── Persistence/
│   │   ├── OrdersDbContext.cs
│   │   ├── Configurations/
│   │   │   ├── OrderConfiguration.cs
│   │   │   └── OrderItemConfiguration.cs
│   │   ├── Repositories/
│   │   │   ├── OrderRepository.cs
│   │   │   └── OrderReadRepository.cs       # Dapper queries
│   │   └── UnitOfWork.cs
│   ├── Extensions/
│   │   └── MediatorExtensions.cs            # Dispatch domain events
│   └── DependencyInjection.cs
│
└── OrderFlow.Orders.Api/
    ├── Controllers/
    │   └── OrdersController.cs
    └── Program.cs
```

### Tópicos do Mercado Cobertos

| Tópico | Detalhe |
|--------|---------|
| **CQRS** | Command e Query segregados com modelos diferentes |
| **MediatR** | IRequest, IRequestHandler, INotificationHandler |
| **Pipeline Behaviors** | Cross-cutting concerns via IPipelineBehavior |
| **FluentValidation** | Validação automática por behavior |
| **Result Pattern** | Sem exceptions para fluxo de controle |
| **Dapper** | Read-side otimizado com SQL manual |
| **EF Core** | Write-side com tracking e migrations |
| **Unit of Work** | Transação coordenando DbContext + Domain Events |
| **Domain Event Dispatching** | Publicação via MediatR antes do SaveChanges |

---

## 2. Decisões Arquiteturais

> 🤔 **Pense antes de ler:**
> 1. Se Commands e Queries usam o **mesmo banco de dados**, qual é a vantagem real de separar os caminhos? (Dica: não é sobre bancos diferentes.)
> 2. O que acontece se a validação de um Command está no Controller em vez de num Pipeline Behavior? Como isso afeta testabilidade?
> 3. Por que retornar `Result<T>` é melhor que `throw new NotFoundException()`? Quando exceções *são* apropriadas?
>
> A resposta curta: CQRS otimiza cada caminho independentemente. A longa: veja as decisões abaixo.

### ADR-006: CQRS — Separação de Leitura e Escrita

> 🧠 **Analogia — O Balcão da Biblioteca:** Em uma biblioteca, você tem dois balcões diferentes: um para **devolver/reservar livros** (escrita) e outro para **consultar o catálogo** (leitura). O balcão de devolução precisa verificar multas, atualizar o sistema, carimbar o livro — é lento e cuidadoso. O balcão de consulta só precisa de uma tela com busca rápida — não mexe em nada, só lê. **CQRS é essa separação**: quem escreve (Command) tem um caminho complexo com domínio rico e validações; quem lê (Query) tem um caminho otimizado para velocidade.

**Contexto:** Reads e Writes têm necessidades diferentes: escrita precisa de validação e domínio rico; leitura precisa de performance e DTOs planos.

**Decisão:** Usar MediatR para rotear Commands (escrita via EF Core) e Queries (leitura via Dapper).

```
                    ┌─────────────────────┐
                    │   OrdersController  │
                    └──────┬──────────────┘
                           │
                    ┌──────▼──────┐
                    │   MediatR   │
                    │  (mediator) │
                    └────┬───┬────┘
                         │   │
              ┌──────────┘   └──────────┐
              │                         │
    ┌─────────▼─────────┐   ┌──────────▼──────────┐
    │   Command Handler │   │   Query Handler     │
    │   (Write Side)    │   │   (Read Side)       │
    └─────────┬─────────┘   └──────────┬──────────┘
              │                         │
    ┌─────────▼─────────┐   ┌──────────▼──────────┐
    │   EF Core         │   │   Dapper            │
    │   (Domain Model)  │   │   (Raw SQL → DTO)   │
    └───────────────────┘   └─────────────────────┘
```

**Consequências:**
- (+) Cada lado otimizado independentemente
- (+) Read DTOs planos (sem tracking overhead)
- (+) Write com domain model completo
- (-) Dois modelos para manter

### ADR-007: Result Pattern

> 🧠 **Analogia — O Envelope de Resposta:** Quando você envia um formulário para um órgão público, o retorno vem num envelope. Dentro pode ter: (a) o documento aprovado (✅ sucesso), ou (b) uma carta explicando por que foi negado e o que corrigir (❌ erro tipado). Você **abre o envelope e verifica**, sem surpresas. Usar `throw new Exception("not found")` é como o órgão jogar o formulário na sua cara — imprevisível, caro (stack trace) e impossível de tratar elegantemente. O `Result<T>` é o envelope: sempre tem uma resposta clara.

**Contexto:** Usar exceptions para fluxo de controle (ex: "order not found") é caro e inelegante.

**Decisão:** Commands e Queries retornam `Result<T>` com erros tipados.

```csharp
// ❌ Exception como fluxo de controle
public Order GetOrder(Guid id)
{
    return _repo.GetById(id) ?? throw new NotFoundException("Order not found");
}

// ✅ Result Pattern
public async Task<Result<OrderDetailDto>> Handle(GetOrderByIdQuery query, CancellationToken ct)
{
    var order = await _readRepository.GetByIdAsync(query.OrderId, ct);
    return order is null
        ? Result<OrderDetailDto>.Failure(OrderErrors.NotFound(query.OrderId))
        : Result<OrderDetailDto>.Success(order);
}
```

### ADR-008: Pipeline Behaviors

> 🧠 **Analogia — O Controle de Segurança do Aeroporto:** Quando você embarca num avião, passa por uma sequência fixa: (1) **check-in** — registram que você apareceu (logging); (2) **detector de metais** — barram itens proibidos antes de entrar (validation); (3) **portão de embarque** — só aqui você realmente entra no avião (transaction + handler). Se você não passa no detector, nem chega ao portão — economia de tempo e recursos. Se inverter a ordem (embarcar antes de verificar), é desastre. Pipeline Behaviors são esses checkpoints em sequência.

**Contexto:** Validação, logging e transações são cross-cutting concerns — repetir em cada handler é DRY violation.

**Decisão:** Usar `IPipelineBehavior<TRequest, TResponse>` do MediatR.

```
Request → [Logging] → [Validation] → [Transaction] → Handler → Response
```

**Por que essa ordem importa?**
1. **Logging** primeiro — registra todas as requests, inclusive as que falham na validação
2. **Validation** antes de Transaction — rejeita requests inválidas sem abrir transação (economia de recursos)
3. **Transaction** por último — envolve apenas o handler em transação real do banco

> ⚠️ A ordem de registro no `AddBehavior()` define a ordem de execução. Registrar `TransactionBehavior` antes de `ValidationBehavior` abriria transações para requests inválidas — desperdício.

---

## 3. Conceitos Chave

### 3.1 MediatR — Mediator Pattern

> 🧠 **Analogia — A Recepcionista do Hotel:** Imagine que você chega num hotel e precisa de várias coisas: quarto limpo, transfer para o aeroporto, reserva no restaurante. Você **não** liga para a camareira, o motorista e o chef diretamente — você fala com a **recepcionista** e ela roteia cada pedido para o responsável certo. O MediatR é essa recepcionista: o Controller faz um `Send(pedido)` e o MediatR descobre qual Handler deve atender. O Controller nunca sabe (nem precisa saber) quem resolve — isso é **desacoplamento**.

O MediatR implementa o **Mediator Pattern**: objetos não se comunicam diretamente; tudo passa por um mediador central.

```csharp
// Sem MediatR: Controller conhece o Handler diretamente
public class OrdersController(CreateOrderCommandHandler handler)
{
    public IActionResult Post(CreateOrderRequest req) => handler.Handle(req); // Acoplamento!
}

// Com MediatR: Controller só conhece a interface IMediator
public class OrdersController(IMediator mediator)
{
    public async Task<IActionResult> Post(CreateOrderRequest req)
    {
        var result = await mediator.Send(new CreateOrderCommand(req.CustomerId, ...));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
```

### 3.2 Command vs Query

| | Command | Query |
|---|---------|-------|
| **Intenção** | Mudar estado | Ler dados |
| **Retorno** | `Result<Guid>` ou `Result` | `Result<Dto>` |
| **Efeito colateral** | Sim (escrita no banco) | Não |
| **Validação** | Sim (FluentValidation) | Mínima |
| **Handler usa** | Repository (EF Core) + Domain | Read Repository (Dapper) |

### 3.3 Unit of Work Pattern

O Unit of Work garante que **todas as operações de um command** sejam atômicas: ou tudo funciona, ou nada funciona.

```
Command → Begin Transaction
            ├── Handler altera aggregate
            ├── Repository salva
            ├── Domain Events disparados
            └── Commit Transaction (ou Rollback)
```

---

## 4. Passo a Passo de Implementação

### 4.1 Criar Projetos

```bash
# Application
dotnet new classlib -n OrderFlow.Orders.Application -o src/Services/Orders/OrderFlow.Orders.Application
dotnet sln add src/Services/Orders/OrderFlow.Orders.Application
dotnet add src/Services/Orders/OrderFlow.Orders.Application reference src/BuildingBlocks/OrderFlow.SharedKernel
dotnet add src/Services/Orders/OrderFlow.Orders.Application reference src/Services/Orders/OrderFlow.Orders.Domain

# Pacotes da Application
dotnet add src/Services/Orders/OrderFlow.Orders.Application package MediatR
dotnet add src/Services/Orders/OrderFlow.Orders.Application package FluentValidation
dotnet add src/Services/Orders/OrderFlow.Orders.Application package FluentValidation.DependencyInjectionExtensions
dotnet add src/Services/Orders/OrderFlow.Orders.Application package Microsoft.Extensions.Logging.Abstractions

# Infrastructure
dotnet new classlib -n OrderFlow.Orders.Infrastructure -o src/Services/Orders/OrderFlow.Orders.Infrastructure
dotnet sln add src/Services/Orders/OrderFlow.Orders.Infrastructure
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure reference src/Services/Orders/OrderFlow.Orders.Domain
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure reference src/Services/Orders/OrderFlow.Orders.Application

# Pacotes da Infrastructure
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure package Microsoft.EntityFrameworkCore.Tools
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure package Dapper
dotnet add src/Services/Orders/OrderFlow.Orders.Infrastructure package MediatR

# API
dotnet new webapi -n OrderFlow.Orders.Api -o src/Services/Orders/OrderFlow.Orders.Api
dotnet sln add src/Services/Orders/OrderFlow.Orders.Api
dotnet add src/Services/Orders/OrderFlow.Orders.Api reference src/Services/Orders/OrderFlow.Orders.Application
dotnet add src/Services/Orders/OrderFlow.Orders.Api reference src/Services/Orders/OrderFlow.Orders.Infrastructure

# Pacotes da API
dotnet add src/Services/Orders/OrderFlow.Orders.Api package Serilog.AspNetCore
dotnet add src/Services/Orders/OrderFlow.Orders.Api package Serilog.Sinks.Seq

# Testes
dotnet new xunit -n OrderFlow.Orders.Application.Tests -o tests/OrderFlow.Orders.Application.Tests
dotnet sln add tests/OrderFlow.Orders.Application.Tests
dotnet add tests/OrderFlow.Orders.Application.Tests reference src/Services/Orders/OrderFlow.Orders.Application
dotnet add tests/OrderFlow.Orders.Application.Tests package FluentAssertions
dotnet add tests/OrderFlow.Orders.Application.Tests package Moq
```

---

## 5. Código de Referência Completo

> **🛠 Notas de Engenharia (deviações aplicadas no código real)**
>
> 1. **`IRepository<T>` define `Remove(T)`** (não `Delete(T)`) — a `OrderRepository` implementa `Remove(Order)` para honrar o contrato do SharedKernel (Fase 1). Ela também implementa `GetAllAsync(CancellationToken)` herdado de `IRepository<Order>`.
> 2. **Suppressões de analisadores em `OrderFlow.Orders.Application.csproj`:** `<NoWarn>$(NoWarn);CA1716;CA1711;CA1000;CA1725;CA1848;CA1873</NoWarn>` — necessárias porque o estilo idiomático do MediatR/Result Pattern colide com regras como _"não nomeie tipo Error"_ (CA1716), _"handlers que terminam em EventHandler"_ (CA1711), _"static em Result&lt;T&gt;"_ (CA1000), _"parâmetro `ct` deve se chamar `cancellationToken`"_ (CA1725) e exigência de `LoggerMessage`-source-generators (CA1848/CA1873).
> 3. **`OrderFlow.Orders.Infrastructure.csproj`** suprime `CA1725;CA1848;CA1873` pelo mesmo motivo.
> 4. **`OrderFlow.Orders.Api.csproj`** suprime `CA1848;CA1873;CA1515` (controller público, logging extensions).
> 5. **`MediatR 13.x`:** a assinatura `RequestHandlerDelegate<TResponse>` aceita `CancellationToken` como parâmetro — daí o uso de `next(ct)` em todos os behaviors. Em MediatR 12.x a assinatura é `next()` sem parâmetros.
> 6. **`SharedKernel` agora referencia `MediatR`** porque `IDomainEvent : INotification` (acoplamento documentado e pragmático).
> 7. **`ICurrentUserService`** já é declarado nesta fase em `Common/Interfaces/` (será implementado na Fase 4).

### 5.1 Result Pattern

**`src/Services/Orders/OrderFlow.Orders.Application/Common/Error.cs`**

```csharp
namespace OrderFlow.Orders.Application.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided.");
}

public static class OrderErrors
{
    public static Error NotFound(Guid orderId) =>
        new("Order.NotFound", $"Order with ID '{orderId}' was not found.");

    public static Error AlreadyConfirmed =>
        new("Order.AlreadyConfirmed", "The order has already been confirmed.");

    public static Error EmptyOrder =>
        new("Order.Empty", "Cannot confirm an order with no items.");

    public static Error InvalidStatusTransition(string from, string to) =>
        new("Order.InvalidTransition", $"Cannot transition from '{from}' to '{to}'.");
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Common/Result.cs`**

```csharp
namespace OrderFlow.Orders.Application.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failed result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result.");

    private Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<T> Success(T value) => new(value, true, Error.None);
    public new static Result<T> Failure(Error error) => new(default, false, error);

    public static implicit operator Result<T>(T value) => Success(value);
}
```

### 5.2 Commands

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/CreateOrder/CreateOrderCommand.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    string Street,
    string Number,
    string Neighborhood,
    string City,
    string State,
    string ZipCode,
    string? Complement = null) : IRequest<Result<Guid>>;
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/CreateOrder/CreateOrderCommandValidator.cs`**

```csharp
using FluentValidation;

namespace OrderFlow.Orders.Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required.");

        RuleFor(x => x.Street)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.Number)
            .NotEmpty().MaximumLength(20);

        RuleFor(x => x.Neighborhood)
            .NotEmpty().MaximumLength(100);

        RuleFor(x => x.City)
            .NotEmpty().MaximumLength(100);

        RuleFor(x => x.State)
            .NotEmpty().Length(2);

        RuleFor(x => x.ZipCode)
            .NotEmpty().Matches(@"^\d{5}-?\d{3}$")
            .WithMessage("ZipCode must be in format 00000-000 or 00000000.");
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/CreateOrder/CreateOrderCommandHandler.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var address = Address.Create(
            request.Street, request.Number, request.Neighborhood,
            request.City, request.State, request.ZipCode,
            request.Complement);

        var order = Order.Create(request.CustomerId, address);

        await orderRepository.AddAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Guid>.Success(order.Id);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/AddOrderItem/AddOrderItemCommand.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;

public sealed record AddOrderItemCommand(
    Guid OrderId,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity) : IRequest<Result>;
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/AddOrderItem/AddOrderItemCommandValidator.cs`**

```csharp
using FluentValidation;

namespace OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;

public sealed class AddOrderItemCommandValidator : AbstractValidator<AddOrderItemCommand>
{
    public AddOrderItemCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UnitPrice).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/AddOrderItem/AddOrderItemCommandHandler.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;

public sealed class AddOrderItemCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AddOrderItemCommand, Result>
{
    public async Task<Result> Handle(AddOrderItemCommand request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(OrderErrors.NotFound(request.OrderId));

        try
        {
            var unitPrice = Money.FromDecimal(request.UnitPrice);
            order.AddItem(request.ProductId, request.ProductName, unitPrice, request.Quantity);

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (OrderDomainException ex)
        {
            return Result.Failure(new Error(ex.Code, ex.Message));
        }
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/ConfirmOrder/ConfirmOrderCommand.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Commands.ConfirmOrder;

public sealed record ConfirmOrderCommand(Guid OrderId) : IRequest<Result>;
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/ConfirmOrder/ConfirmOrderCommandHandler.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands.ConfirmOrder;

public sealed class ConfirmOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ConfirmOrderCommand, Result>
{
    public async Task<Result> Handle(ConfirmOrderCommand request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(OrderErrors.NotFound(request.OrderId));

        try
        {
            order.Confirm();
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (OrderDomainException ex)
        {
            return Result.Failure(new Error(ex.Code, ex.Message));
        }
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/CancelOrder/CancelOrderCommand.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<Result>;
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/CancelOrder/CancelOrderCommandValidator.cs`**

```csharp
using FluentValidation;

namespace OrderFlow.Orders.Application.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Commands/CancelOrder/CancelOrderCommandHandler.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(OrderErrors.NotFound(request.OrderId));

        try
        {
            order.Cancel(request.Reason);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (OrderDomainException ex)
        {
            return Result.Failure(new Error(ex.Code, ex.Message));
        }
    }
}
```

### 5.3 Queries (Read Side com Dapper)

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Queries/GetOrderById/OrderDetailDto.cs`**

```csharp
namespace OrderFlow.Orders.Application.Orders.Queries.GetOrderById;

public sealed record OrderDetailDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string? CancellationReason { get; init; }
    public string ShippingAddress { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public List<OrderItemDto> Items { get; init; } = [];
}

public sealed record OrderItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal TotalPrice { get; init; }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Queries/GetOrderById/GetOrderByIdQuery.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderDetailDto>>;
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Queries/GetOrderById/GetOrderByIdQueryHandler.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Common.Interfaces;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler(IOrderReadRepository readRepository)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderDetailDto>>
{
    public async Task<Result<OrderDetailDto>> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        var order = await readRepository.GetOrderDetailAsync(request.OrderId, ct);

        return order is null
            ? Result<OrderDetailDto>.Failure(OrderErrors.NotFound(request.OrderId))
            : Result<OrderDetailDto>.Success(order);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Queries/GetOrdersByCustomer/OrderSummaryDto.cs`**

```csharp
namespace OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

public sealed record OrderSummaryDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Queries/GetOrdersByCustomer/GetOrdersByCustomerQuery.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

public sealed record GetOrdersByCustomerQuery(
    Guid CustomerId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<IReadOnlyList<OrderSummaryDto>>>;
```

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/Queries/GetOrdersByCustomer/GetOrdersByCustomerQueryHandler.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Common.Interfaces;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

public sealed class GetOrdersByCustomerQueryHandler(IOrderReadRepository readRepository)
    : IRequestHandler<GetOrdersByCustomerQuery, Result<IReadOnlyList<OrderSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<OrderSummaryDto>>> Handle(
        GetOrdersByCustomerQuery request, CancellationToken ct)
    {
        var orders = await readRepository.GetOrdersByCustomerAsync(
            request.CustomerId, request.Page, request.PageSize, ct);

        return Result<IReadOnlyList<OrderSummaryDto>>.Success(orders);
    }
}
```

### 5.4 Read Repository Interface

**`src/Services/Orders/OrderFlow.Orders.Application/Common/Interfaces/IOrderReadRepository.cs`**

```csharp
using OrderFlow.Orders.Application.Orders.Queries.GetOrderById;
using OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

namespace OrderFlow.Orders.Application.Common.Interfaces;

public interface IOrderReadRepository
{
    Task<OrderDetailDto?> GetOrderDetailAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummaryDto>> GetOrdersByCustomerAsync(
        Guid customerId, int page, int pageSize, CancellationToken ct = default);
}
```

### 5.5 Pipeline Behaviors

**`src/Services/Orders/OrderFlow.Orders.Application/Common/Behaviors/ValidationBehavior.cs`**

```csharp
using FluentValidation;
using MediatR;

namespace OrderFlow.Orders.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next(ct);

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next(ct);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Common/Behaviors/LoggingBehavior.cs`**

```csharp
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Orders.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);

        var stopwatch = Stopwatch.StartNew();
        var response = await next(ct);
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > 500)
        {
            logger.LogWarning("Long running request: {RequestName} ({ElapsedMs}ms)",
                requestName, stopwatch.ElapsedMilliseconds);
        }

        logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms",
            requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Application/Common/Behaviors/TransactionBehavior.cs`**

> **Conceito:** O TransactionBehavior envolve commands em uma transação explícita do EF Core. Se o handler falha, a transação faz rollback. Se já existe uma transação ativa (ex: testes), ele não cria outra.

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Orders.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(
    DbContext dbContext,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Só aplica transação para commands (não para queries)
        var requestName = typeof(TRequest).Name;
        if (!requestName.EndsWith("Command"))
            return await next(ct);

        // Se já há uma transação ativa, não cria outra
        if (dbContext.Database.CurrentTransaction is not null)
            return await next(ct);

        IDbContextTransaction? transaction = null;

        try
        {
            transaction = await dbContext.Database.BeginTransactionAsync(ct);
            logger.LogInformation("Begin transaction for {RequestName} ({TransactionId})",
                requestName, transaction.TransactionId);

            var response = await next(ct);

            await transaction.CommitAsync(ct);
            logger.LogInformation("Transaction committed for {RequestName} ({TransactionId})",
                requestName, transaction.TransactionId);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transaction rolled back for {RequestName}", requestName);

            if (transaction is not null)
                await transaction.RollbackAsync(ct);

            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }
}
```

> **Por que `DbContext` e não `IUnitOfWork`?** O behavior precisa acessar `Database.BeginTransactionAsync()`, que é uma feature do EF Core. O handler continua usando `IUnitOfWork.SaveChangesAsync()` para salvar — o behavior apenas garante que essa operação está dentro de uma transação explícita.

### 5.6 Domain Event Handler

**`src/Services/Orders/OrderFlow.Orders.Application/Orders/EventHandlers/OrderCreatedDomainEventHandler.cs`**

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Orders.EventHandlers;

public sealed class OrderCreatedDomainEventHandler(
    ILogger<OrderCreatedDomainEventHandler> logger)
    : INotificationHandler<OrderCreatedDomainEvent>
{
    public Task Handle(OrderCreatedDomainEvent notification, CancellationToken ct)
    {
        logger.LogInformation(
            "Domain Event: Order created. OrderId={OrderId}, OrderNumber={OrderNumber}, CustomerId={CustomerId}",
            notification.OrderId, notification.OrderNumber, notification.CustomerId);

        // Na Fase 5, aqui publicaremos um Integration Event para o RabbitMQ
        return Task.CompletedTask;
    }
}
```

> **Nota:** Para o MediatR publicar Domain Events como `INotification`, os records de domain event precisam implementar `INotification`. Atualize o `IDomainEvent` no SharedKernel:

**Atualização no `src/BuildingBlocks/OrderFlow.SharedKernel/IDomainEvent.cs`**

```csharp
using MediatR;

namespace OrderFlow.SharedKernel;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}
```

### 5.7 Application DI

**`src/Services/Orders/OrderFlow.Orders.Application/DependencyInjection.cs`**

```csharp
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Orders.Application.Common.Behaviors;

namespace OrderFlow.Orders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline order matters! A ordem de registro define a ordem de execução:
            // 1. Logging: registra request/response (inclusive falhas de validação)
            // 2. Validation: rejeita requests inválidas ANTES de abrir transação
            // 3. Transaction: envolve o handler em transação real do DbContext
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
```

### 5.8 Infrastructure — EF Core (Write Side)

**`src/Services/Orders/OrderFlow.Orders.Infrastructure/Persistence/OrdersDbContext.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

namespace OrderFlow.Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

namespace OrderFlow.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        // OrderNumber (Value Object → Owned Type)
        builder.OwnsOne(o => o.OrderNumber, on =>
        {
            on.Property(n => n.Value)
                .HasColumnName("OrderNumber")
                .HasMaxLength(20)
                .IsRequired();

            on.HasIndex(n => n.Value).IsUnique();
        });

        // ShippingAddress (Value Object → Owned Type)
        builder.OwnsOne(o => o.ShippingAddress, a =>
        {
            a.Property(p => p.Street).HasColumnName("ShippingStreet").HasMaxLength(200).IsRequired();
            a.Property(p => p.Number).HasColumnName("ShippingNumber").HasMaxLength(20).IsRequired();
            a.Property(p => p.Complement).HasColumnName("ShippingComplement").HasMaxLength(100);
            a.Property(p => p.Neighborhood).HasColumnName("ShippingNeighborhood").HasMaxLength(100).IsRequired();
            a.Property(p => p.City).HasColumnName("ShippingCity").HasMaxLength(100).IsRequired();
            a.Property(p => p.State).HasColumnName("ShippingState").HasMaxLength(2).IsRequired();
            a.Property(p => p.ZipCode).HasColumnName("ShippingZipCode").HasMaxLength(8).IsRequired();
            a.Property(p => p.Country).HasColumnName("ShippingCountry").HasMaxLength(50).IsRequired();
        });

        // Status (Value Object → conversão)
        builder.Property(o => o.Status)
            .HasConversion(
                s => s.Value,
                s => OrderStatus.FromString(s))
            .HasMaxLength(20)
            .IsRequired();

        // TotalAmount (Value Object → Owned Type)
        builder.OwnsOne(o => o.TotalAmount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("TotalAmount").HasPrecision(18, 2).IsRequired();
            m.Property(p => p.Currency).HasColumnName("TotalCurrency").HasMaxLength(3).IsRequired();
        });

        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.Property(o => o.CustomerId).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();

        // Shadow property para concurrency
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // Items (navigation)
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Order.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Ignorar domain events (não persistir)
        builder.Ignore(o => o.DomainEvents);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Infrastructure/Persistence/Configurations/OrderItemConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

namespace OrderFlow.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductId).IsRequired();
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();

        builder.OwnsOne(i => i.UnitPrice, m =>
        {
            m.Property(p => p.Amount).HasColumnName("UnitPrice").HasPrecision(18, 2).IsRequired();
            m.Property(p => p.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3).IsRequired();
        });

        // TotalPrice é calculado — ignorar no EF
        builder.Ignore(i => i.TotalPrice);
        builder.Ignore(i => i.DomainEvents);
    }
}
```

### 5.9 Infrastructure — Repositories

**`src/Services/Orders/OrderFlow.Orders.Infrastructure/Persistence/Repositories/OrderRepository.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Interfaces;

namespace OrderFlow.Orders.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(OrdersDbContext dbContext) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task AddAsync(Order entity, CancellationToken ct = default)
    {
        await dbContext.Orders.AddAsync(entity, ct);
    }

    public void Update(Order entity)
    {
        dbContext.Orders.Update(entity);
    }

    public void Remove(Order entity)
    {
        dbContext.Orders.Remove(entity);
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .ToListAsync(ct);
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber.Value == orderNumber, ct);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Infrastructure/Persistence/Repositories/OrderReadRepository.cs`** (Dapper)

```csharp
using System.Data;
using Dapper;
using OrderFlow.Orders.Application.Common.Interfaces;
using OrderFlow.Orders.Application.Orders.Queries.GetOrderById;
using OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

namespace OrderFlow.Orders.Infrastructure.Persistence.Repositories;

public sealed class OrderReadRepository(IDbConnection dbConnection) : IOrderReadRepository
{
    public async Task<OrderDetailDto?> GetOrderDetailAsync(Guid orderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                o.Id, o.OrderNumber, o.CustomerId, o.Status,
                o.TotalAmount, o.TotalCurrency, o.CancellationReason,
                CONCAT(o.ShippingStreet, ', ', o.ShippingNumber, ' - ',
                       o.ShippingNeighborhood, ', ', o.ShippingCity, '/',
                       o.ShippingState) AS ShippingAddress,
                o.CreatedAt, o.UpdatedAt
            FROM Orders o
            WHERE o.Id = @OrderId;

            SELECT
                i.Id, i.ProductId, i.ProductName,
                i.UnitPrice, i.Quantity,
                (i.UnitPrice * i.Quantity) AS TotalPrice
            FROM OrderItems i
            WHERE i.OrderId = @OrderId;
            """;

        using var multi = await dbConnection.QueryMultipleAsync(sql, new { OrderId = orderId });

        var order = await multi.ReadSingleOrDefaultAsync<OrderDetailDto>();
        if (order is null)
            return null;

        var items = (await multi.ReadAsync<OrderItemDto>()).ToList();
        return order with { Items = items };
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetOrdersByCustomerAsync(
        Guid customerId, int page, int pageSize, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                o.Id, o.OrderNumber, o.Status, o.TotalAmount,
                (SELECT COUNT(*) FROM OrderItems i WHERE i.OrderId = o.Id) AS ItemCount,
                o.CreatedAt
            FROM Orders o
            WHERE o.CustomerId = @CustomerId
            ORDER BY o.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var orders = await dbConnection.QueryAsync<OrderSummaryDto>(sql, new
        {
            CustomerId = customerId,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        });

        return orders.ToList().AsReadOnly();
    }
}
```

### 5.10 Infrastructure — Unit of Work com Domain Event Dispatching

**`src/Services/Orders/OrderFlow.Orders.Infrastructure/Extensions/MediatorExtensions.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Infrastructure.Extensions;

public static class MediatorExtensions
{
    public static async Task DispatchDomainEventsAsync(
        this IMediator mediator, OrdersDbContext dbContext, CancellationToken ct = default)
    {
        var domainEntities = dbContext.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent, ct);
        }
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Infrastructure/Persistence/UnitOfWork.cs`**

```csharp
using MediatR;
using OrderFlow.Orders.Infrastructure.Extensions;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Infrastructure.Persistence;

public sealed class UnitOfWork(OrdersDbContext dbContext, IMediator mediator) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Dispatch domain events ANTES do SaveChanges
        // Isso garante que handlers de domain events façam alterações
        // na mesma transação
        await mediator.DispatchDomainEventsAsync(dbContext, ct);

        return await dbContext.SaveChangesAsync(ct);
    }
}
```

### 5.11 Infrastructure DI

**`src/Services/Orders/OrderFlow.Orders.Infrastructure/DependencyInjection.cs`**

```csharp
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Orders.Application.Common.Interfaces;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.Orders.Infrastructure.Persistence.Repositories;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException("Connection string 'OrdersDb' not found.");

        // EF Core (Write side)
        services.AddDbContext<OrdersDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(OrdersDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(3);
            }));

        // Dapper (Read side)
        services.AddScoped<IDbConnection>(_ => new SqlConnection(connectionString));

        // Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // DbContext como alias — necessário para TransactionBehavior que injeta DbContext genérico
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<OrdersDbContext>());

        return services;
    }
}
```

### 5.12 API — Controller e Program.cs

**`src/Services/Orders/OrderFlow.Orders.Api/Controllers/OrdersController.cs`**

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;
using OrderFlow.Orders.Application.Orders.Commands.CancelOrder;
using OrderFlow.Orders.Application.Orders.Commands.ConfirmOrder;
using OrderFlow.Orders.Application.Orders.Commands.CreateOrder;
using OrderFlow.Orders.Application.Orders.Queries.GetOrderById;
using OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

namespace OrderFlow.Orders.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem(Guid id, AddOrderItemCommand command, CancellationToken ct)
    {
        if (id != command.OrderId)
            return BadRequest(new Error("Route.Mismatch", "Route ID does not match command OrderId."));

        var result = await mediator.Send(command, ct);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ConfirmOrderCommand(id), ct);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancelOrderCommand command, CancellationToken ct)
    {
        if (id != command.OrderId)
            return BadRequest(new Error("Route.Mismatch", "Route ID does not match command OrderId."));

        var result = await mediator.Send(command, ct);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpGet("customer/{customerId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCustomer(
        Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetOrdersByCustomerQuery(customerId, page, pageSize), ct);

        return Ok(result.Value);
    }

    private IActionResult HandleFailure(Result result)
    {
        return result.Error.Code.Contains("NotFound")
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }
}
```

**`src/Services/Orders/OrderFlow.Orders.Api/Program.cs`**

```csharp
using FluentValidation;
using OrderFlow.Orders.Application;
using OrderFlow.Orders.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341"));

// Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Global exception handler
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware para FluentValidation exceptions → ProblemDetails
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Validation Error",
                status = 400,
                errors
            });
        }
    });
});

app.MapControllers();

app.Run();

// Para testes de integração
public partial class Program;
```

> **Nota de implementação (CA1305):** O analisador `CA1305` exige `IFormatProvider` em `WriteTo.Console(...)`. Use `formatProvider: System.Globalization.CultureInfo.InvariantCulture` (e `using System.Globalization;` no topo). Quando `Seq` ainda não estiver provisionado nesta fase, omita `WriteTo.Seq(...)`.
>
> **Nota de implementação (RunAsync):** O analisador `CA2007`/padrões modernos preferem `await app.RunAsync();` em vez de `app.Run();` no topo do `Program.cs`.

**`src/Services/Orders/OrderFlow.Orders.Api/appsettings.json`**

```json
{
  "ConnectionStrings": {
    "OrdersDb": "Server=localhost,1433;Database=OrderFlow_Orders;User Id=sa;Password=YourStr0ng!Pass;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

---

## 6. Testes

### 6.1 Testes dos Command Handlers

**`tests/OrderFlow.Orders.Application.Tests/Orders/Commands/CreateOrderCommandHandlerTests.cs`**

```csharp
using FluentAssertions;
using Moq;
using OrderFlow.Orders.Application.Orders.Commands.CreateOrder;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Tests.Orders.Commands;

public class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new CreateOrderCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessWithOrderId()
    {
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            "Rua Teste", "100", "Centro", "São Paulo", "SP", "01001000");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _repositoryMock.Verify(r => r.AddAsync(
            It.Is<Order>(o => o.Status == OrderStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### 6.2 Testes do AddOrderItem Handler

**`tests/OrderFlow.Orders.Application.Tests/Orders/Commands/AddOrderItemCommandHandlerTests.cs`**

```csharp
using FluentAssertions;
using Moq;
using OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Tests.Orders.Commands;

public class AddOrderItemCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AddOrderItemCommandHandler _handler;

    public AddOrderItemCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new AddOrderItemCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_OrderExists_AddsItemSuccessfully()
    {
        var order = Order.Create(Guid.NewGuid(),
            Address.Create("Rua", "1", "Bairro", "Cidade", "SP", "01001000"));

        _repositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AddOrderItemCommand(order.Id, Guid.NewGuid(), "Laptop", 2500m, 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsFailure()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new AddOrderItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Laptop", 2500m, 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NotFound");
    }
}
```

### 6.3 Testes do Validation Behavior

**`tests/OrderFlow.Orders.Application.Tests/Common/ValidationBehaviorTests.cs`**

```csharp
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Common.Behaviors;

namespace OrderFlow.Orders.Application.Tests.Common;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, Result>(
            Enumerable.Empty<IValidator<TestRequest>>());

        var nextCalled = false;
        MediatR.RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithFailures_ThrowsValidationException()
    {
        var validatorMock = new Mock<IValidator<TestRequest>>();
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Name", "Name is required")]));

        var behavior = new ValidationBehavior<TestRequest, Result>(
            [validatorMock.Object]);

        MediatR.RequestHandlerDelegate<Result> next = _ => Task.FromResult(Result.Success());

        var act = () => behavior.Handle(new TestRequest(), next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    private sealed record TestRequest;
}
```

---

## ⚠️ Erros Comuns com CQRS + MediatR

| # | Erro | Consequência | Solução |
|---|---|---|---|
| 1 | **Injetar `IOrderRepository` (EF Core) em QueryHandlers** | Quebra a separação CQRS. Queries ficam lentas com change tracking | Queries devem usar `IOrderReadRepository` (Dapper). Repositório EF é só para Commands |
| 2 | **Registrar behaviors na ordem errada** | Validation depois de Transaction = transação aberta para request inválida | Ordem correta: Logging → Validation → Transaction. Verifique `AddBehavior<>()` no DI |
| 3 | **Esquecer `CancellationToken` nos handlers** | MediatR propaga o token, mas se o handler ignora, requests canceladas continuam processando | Propague `ct` para todo `async` call: repositório, EF Core, Dapper |
| 4 | **Result<T> com `catch(Exception)`** | Captura exceções inesperadas (NullRef, SO) e converte em Result.Failure — esconde bugs | Só capture `DomainException`. Deixe `Exception` genérica borbulhar para o middleware |
| 5 | **Dapper SQL com string interpolation** | SQL injection: `$"WHERE Name = '{input}'"` | Sempre use parâmetros: `WHERE Name = @Name`, passando `new { Name = input }` |
| 6 | **DTOs com `init` em propriedades que Dapper precisa setar** | Dapper precisa de setters para popular objetos via reflection | Verifique se `init` funciona com sua versão do Dapper, ou use `{ get; set; }` nos DTOs de leitura |

---

## 🔧 Troubleshooting — Fase 03

| Sintoma | Causa Provável | Solução |
|---------|---------------|---------|
| "No handler registered for CreateOrderCommand" | `AddMediatR` recebe assembly errado; ou handler não implementa `IRequestHandler<TRequest, TResponse>` com tipo de retorno exato | Verifique: `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly))` |
| "Owned type 'Money' already configured" | Duas configurações conflitantes no `ModelBuilder` | Verifique se não há duplicação em `ApplyConfigurationsFromAssembly` |
| Dapper retorna null em tudo | Nomes de colunas no SQL não batem com propriedades do DTO | Dapper é case-insensitive mas exige match de nomes. Use `AS` no SQL: `SELECT order_id AS OrderId` |
| TransactionBehavior abre transação para queries | Filtro `EndsWith("Command")` falhando | Considere interface marker `ICommand` em vez de string matching |
| Domain Events não disparam | Entidades no ChangeTracker estão como `Detached` | Verifique se o aggregate foi obtido via `DbSet`, não criado com `new` fora do tracking |
| Validation nunca executa | `AbstractValidator<T>` não registrado no DI | Verifique `services.AddValidatorsFromAssembly(...)` |

### 💡 Exemplo Completo de Request Ida-e-Volta (curl)

```bash
# 1. Criar pedido
curl -X POST http://localhost:5003/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "550e8400-e29b-41d4-a716-446655440000", "shippingStreet": "Rua A", "shippingCity": "SP", "shippingState": "SP", "shippingZipCode": "01001-000", "shippingCountry": "BR"}'
# Retorna: { "isSuccess": true, "value": "guid-do-pedido" }

# 2. Adicionar item
curl -X POST http://localhost:5003/api/orders/{orderId}/items \
  -H "Content-Type: application/json" \
  -d '{"productId": "...", "productName": "Laptop", "unitPrice": 5000.00, "currency": "BRL", "quantity": 2}'

# 3. Consultar pedido (caminho de leitura — Dapper)
curl http://localhost:5003/api/orders/{orderId}
# Retorna: DTO completo com items, totais, status

# 4. Confirmar pedido
curl -X POST http://localhost:5003/api/orders/{orderId}/confirm

# 5. Validação falha (exemplo)
curl -X POST http://localhost:5003/api/orders/{orderId}/confirm
# Retorna 400: { "detail": "Can only confirm pending orders." }
```

---

## 🔗 Conectando os Pontos

### O que veio das fases anteriores

| Artefato | Origem | Transformação |
|---------|--------|--------------|
| `IDomainEvent` | Fase 01 SharedKernel | Agora herda de `INotification` (MediatR). **Breaking change conceitual:** estamos acoplando SharedKernel ao MediatR — decisão pragmática documentada no ADR |
| `Order`, `OrderItem`, VOs | Fase 02 Domain | Persistidos via EF Core com Owned Types (Money → colunas `TotalAmount` + `TotalCurrency`) |
| `IOrderRepository` | Fase 02 Domain | Implementação concreta em Infrastructure com EF Core |
| `ICurrentUserService` | Novo nesta fase | Será implementado na **Fase 04** com claims do JWT |

### Preview: O que vem nas próximas fases

> **Fase 04 (Auth):** O `ICurrentUserService` que declaramos aqui será implementado com `ClaimsPrincipal` do JWT. O `CustomerId` deixa de ser hardcoded e passa a vir do token.
>
> **Fase 05 (Mensageria):** Os Domain Event Handlers que criamos como loggers serão transformados em publishers de **Integration Events** via MassTransit. O `OrderCreatedDomainEvent` gerará um `OrderCreatedIntegrationEvent` que cruza fronteiras de serviço.

---

## 7. Checkpoint

> 💡 **Por que isso importa no dia-a-dia?** A Application Layer é o **maestro da orquestra** — ela não toca nenhum instrumento (não tem lógica de domínio nem de infra), mas coordena quem toca o quê e quando. Se o maestro erra a entrada, a sinfonia vira cacofonia: validação rodando depois da transação, domain events disparando antes do commit, erros silenciosos. Sêniores criam Application Layers que são **previsíveis e auditáveis** — você olha o pipeline e sabe exatamente o que acontece com cada request.

### Validação Completa

- [ ] **Projetos criados:** Application, Infrastructure, Api, Application.Tests
- [ ] **Result Pattern:** `Result<T>` e `Error` implementados
- [ ] **Commands implementados:** CreateOrder, AddOrderItem, ConfirmOrder, CancelOrder
- [ ] **Queries implementadas:** GetOrderById (Dapper), GetOrdersByCustomer (Dapper)
- [ ] **Pipeline Behaviors:** Validation, Logging, Transaction
- [ ] **Domain Event Dispatching:** MediatorExtensions + UnitOfWork
- [ ] **EF Core Configurations:** Order e OrderItem com owned types
- [ ] **Dapper Read Repository:** SQL otimizado com QueryMultiple
- [ ] **Controller funcional:** Todos os endpoints mapeados
- [ ] **Validação por FluentValidation:** Validators em commands
- [ ] **Testes:** Handlers com Moq, Behaviors testados
- [ ] **Swagger:** Endpoints documentados automaticamente
- [ ] **Migration criada:** `dotnet ef migrations add InitialOrders -p src/Services/Orders/OrderFlow.Orders.Infrastructure -s src/Services/Orders/OrderFlow.Orders.Api`
- [ ] **Commit:** `feat(orders): implement CQRS with MediatR, pipeline behaviors and Dapper read side`

### Comandos de Verificação

```bash
# Build completo
dotnet build

# Rodar testes
dotnet test tests/OrderFlow.Orders.Application.Tests --verbosity normal

# Criar migration
dotnet ef migrations add InitialOrders \
  -p src/Services/Orders/OrderFlow.Orders.Infrastructure \
  -s src/Services/Orders/OrderFlow.Orders.Api

# Rodar API
dotnet run --project src/Services/Orders/OrderFlow.Orders.Api

# Testar endpoint (com API rodando)
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"...", "street":"Rua Teste", "number":"100", "neighborhood":"Centro", "city":"São Paulo", "state":"SP", "zipCode":"01001000"}'
```

---

## 📋 Resumo de Artefatos Criados

| Artefato | Arquivo/Local |
|----------|---------------|
| Commands | `CreateOrderCommand.cs`, `ConfirmOrderCommand.cs`, `CancelOrderCommand.cs` |
| Queries | `GetOrderQuery.cs`, `GetOrdersByCustomerQuery.cs` |
| Handlers | `CreateOrderHandler.cs`, `ConfirmOrderHandler.cs`, `GetOrderHandler.cs` |
| Pipeline Behaviors | `ValidationBehavior.cs`, `LoggingBehavior.cs` |
| Validators | `CreateOrderValidator.cs` (FluentValidation) |
| DTOs/Responses | `OrderResponse.cs`, `OrderItemResponse.cs` |
| Repository | `OrderRepository.cs` (EF Core), `OrderReadRepository.cs` (Dapper) |
| Interceptor | `DomainEventInterceptor.cs` (dispatch de domain events no SaveChanges) |
| DbContext | `OrdersDbContext.cs` + EntityTypeConfigurations |
| Testes | `CreateOrderHandlerTests.cs`, `ValidationBehaviorTests.cs` |

---

## 💼 Perguntas Frequentes em Entrevistas — Fase 3

**1. "O que é CQRS e quando vale a pena usar?"**
— CQRS (Command Query Responsibility Segregation) separa modelos de leitura e escrita. **Escrita** usa EF Core com domínio rico + validações; **leitura** usa Dapper com SQL direto para performance. Vale quando: leitura e escrita têm necessidades diferentes (ex: escrita valida regras complexas, leitura precisa de joins rápidos). **Não** vale para CRUDs simples — adiciona complexidade desnecessária.

**2. "Para que servem Pipeline Behaviors no MediatR?"**
— São **middlewares** que interceptam toda request antes/depois do handler. `ValidationBehavior` roda FluentValidation automaticamente; `LoggingBehavior` loga tempo de execução. A cada novo cross-cutting concern (cache, auditoria), adicione um behavior — sem alterar handlers existentes. É o **Open/Closed Principle** na prática.

**3. "Por que usar Result Pattern ao invés de lançar exceções?"**
— Exceções são para situações **excepcionais** (banco fora, rede caiu), não para fluxo de negócio. "Pedido inválido" não é excepcional — é esperado. `Result<T>` retorna `Success(data)` ou `Failure(error)` sem custo de stack unwinding. O controller converte `Result` em HTTP status (200/400/404) de forma explícita e testável.

**4. "Por que Dapper para leitura e EF Core para escrita?"**
— **EF Core** brilha na escrita: change tracking, migrations, interceptors para domain events. **Dapper** brilha na leitura: SQL puro, sem overhead de tracking, perfeito para projeções (DTOs flat). Não é sobre "um ser melhor" — cada um no que faz melhor. Leitura com Dapper pode ser 5-10x mais rápida em cenários de reporting.

**5. "Como FluentValidation se integra com MediatR?"**
— Através do `ValidationBehavior<TRequest, TResponse>`. Ele recebe todos os `IValidator<TRequest>` via DI, executa validação **antes** do handler, e retorna `Result.Failure` se houver erros. O handler nunca recebe dados inválidos. Validators são auto-registrados via `Assembly.GetExecutingAssembly()`.

---

## 🔬 Aprofundamento Sênior

### A1. Pipeline Behaviors Avançados

MediatR pipelines = AOP elegante. Além de validação:

#### Caching Behavior
```csharp
public sealed class CachingBehavior<TRequest, TResponse>(IDistributedCache cache)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery<TResponse>
{
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var cached = await cache.GetAsync<TResponse>(request.CacheKey, ct);
        if (cached is not null) return cached;

        var response = await next();
        await cache.SetAsync(request.CacheKey, response, request.Ttl, ct);
        return response;
    }
}
```

#### Retry Behavior (combina com Polly)
#### Logging Behavior — log estruturado de toda Command
#### Transaction Behavior — abre/commita/rollback automático

### A2. Result Pattern — Sem Exceções para Fluxo

```csharp
public readonly record struct Result<T>(T? Value, Error? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error);
}

// Handler
public async Task<Result<OrderId>> Handle(CreateOrderCommand cmd, CancellationToken ct)
{
    if (await _customerExists(cmd.CustomerId, ct) is false)
        return Result<OrderId>.Failure(Errors.CustomerNotFound);
    // ... cria, salva
    return Result<OrderId>.Success(order.Id);
}

// Endpoint mapeia Result → IResult
result.Match(
    success: id => Results.Created($"/orders/{id}", id),
    failure: err => err.ToProblemDetails());
```

**Por quê:** exceções são caras (stack walk), pollutam logs, escondem fluxo. Result torna **fluxo esperado** explícito; exceções para **excepcional**.

### A3. Wolverine — Alternativa Moderna ao MediatR

[Wolverine](https://wolverinefx.io/) é um framework mais ambicioso:
- Sem `IRequestHandler` — métodos públicos são handlers automáticos
- Outbox e saga **embutidos**
- Sem licença comercial (MediatR v12+ tem)
- Source generators em vez de reflection

Para projetos novos em 2026, considere. Trade-off: comunidade menor.

### A4. Dapper Patterns Avançados

#### Multi-Mapping (1 query → 2 objetos)
```csharp
var sql = @"SELECT o.*, c.* FROM Orders o JOIN Customers c ON o.CustomerId = c.Id WHERE o.Id = @id";
var order = (await conn.QueryAsync<OrderDto, CustomerDto, OrderDto>(
    sql, (o, c) => { o.Customer = c; return o; }, new { id }, splitOn: "Id")).First();
```

#### Streaming (não carrega tudo)
```csharp
await foreach (var row in conn.QueryUnbufferedAsync<OrderRow>(sql, ct))
    yield return row;
```

#### Keyset Pagination — sempre prefira a OFFSET
```sql
-- ❌ OFFSET fica O(N) em páginas distantes
SELECT * FROM Orders ORDER BY CreatedAt DESC OFFSET 100000 ROWS FETCH NEXT 20 ROWS ONLY;

-- ✅ Keyset — usa índice, O(log N)
SELECT * FROM Orders WHERE CreatedAt < @lastCreatedAt ORDER BY CreatedAt DESC FETCH NEXT 20 ROWS ONLY;
```

### A5. CQRS Avançado — Read Model Separado

Quando query model fica muito diferente do write model, materialize **read model dedicado**:

```
Write model (SQL Orders + Items + ...) 
   → eventos (OrderConfirmed, ItemAdded) 
   → projector 
   → read model (NoSQL OrderSummary plana, otimizada para listagem)
```

Vantagens: leitura instantânea (single document), escala independente. Desvantagem: eventual consistency.

### 💼 Perguntas Sênior

**"Quando vale separar read model fisicamente?"** — Quando query exige join de 5+ tabelas, agregações pesadas, ou diferentes APIs querem visões muito distintas. Sintoma: query tem 200 linhas de SQL com CTEs.

**"Pipeline behaviors — qual ordem importa?"** — Logging > Validation > Transaction > Caching > Handler. Logging primeiro captura tudo (incluindo erros de validação); Validation antes de Transaction (não abre transação para request inválida — economia de recursos); Transaction antes do Handler (garante atomicidade); Caching opcional antes do Handler (evita reprocessar queries já cacheadas).

---

> **Próximo passo:** Avance para `fase-04-autenticacao-seguranca.md` para implementar Identity API.
>
> 🚀 **Trilha Sênior relacionada:** [`fase-09-resiliencia-polly.md`](./fase-09-resiliencia-polly.md) — Polly behaviors em handlers.

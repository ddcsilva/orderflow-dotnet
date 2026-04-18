# Fase 2 — Domínio Rico e DDD

> **Objetivo:** Construir o Orders API com domínio rico — Aggregates, Value Objects, Domain Events — e cobrir o domínio com testes unitários de alta qualidade.

> **Pré-requisito:** Fase 1 concluída (Catalog API funcional, SharedKernel criado).

### 🎯 O que você vai aprender nesta fase

- Modelar **Aggregates** com raiz (Order) e entidades filhas (OrderItem)
- Implementar **Value Objects** imutáveis (Money, Address, OrderNumber)
- Criar **máquina de estados** para OrderStatus com transições validadas
- Aplicar **Domain Events** para desacoplamento de side effects
- Escrever **testes unitários** de domínio com xUnit + FluentAssertions
- Entender a diferença entre **Domain Events** e **Integration Events**
- Proteger invariantes do aggregate via encapsulamento (private setters, factory methods)

---

## Sumário

1. [Visão da Fase](#1-visão-da-fase)
2. [Decisões Arquiteturais](#2-decisões-arquiteturais)
3. [Conceitos de DDD](#3-conceitos-de-ddd)
4. [Passo a Passo de Implementação](#4-passo-a-passo-de-implementação)
5. [Código de Referência Completo](#5-código-de-referência-completo)
6. [Testes](#6-testes)
7. [Checkpoint](#7-checkpoint)

---

## 1. Visão da Fase

### O Que Vamos Construir

```
src/Services/Orders/
├── OrderFlow.Orders.Domain/          ← FOCO DESTA FASE
│   ├── Aggregates/
│   │   └── OrderAggregate/
│   │       ├── Order.cs              # Aggregate Root
│   │       ├── OrderItem.cs          # Entity dentro do Aggregate
│   │       └── OrderStatus.cs        # Value Object (state machine)
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   ├── Address.cs
│   │   └── OrderNumber.cs
│   ├── Events/
│   │   ├── OrderCreatedDomainEvent.cs
│   │   ├── OrderConfirmedDomainEvent.cs
│   │   ├── OrderItemAddedDomainEvent.cs
│   │   └── OrderCancelledDomainEvent.cs
│   ├── Exceptions/
│   │   ├── OrderDomainException.cs
│   │   └── InsufficientStockException.cs
│   └── Interfaces/
│       └── IOrderRepository.cs

tests/
└── OrderFlow.Orders.Domain.Tests/     ← TESTES DESTA FASE
    ├── OrderTests.cs
    ├── OrderItemTests.cs
    ├── MoneyTests.cs
    ├── AddressTests.cs
    └── OrderStatusTests.cs
```

### O Que Você Vai Praticar

| Tópico | Detalhe |
|--------|---------|
| **Aggregate Root** | Order como raiz, protegendo invariantes |
| **Entity** | OrderItem como entity interna com identidade |
| **Value Objects** | Money, Address, OrderNumber — imutáveis, sem identidade |
| **Domain Events** | Notificações de coisas que aconteceram no domínio |
| **Encapsulamento forte** | Private setters, factory methods, sem public constructors |
| **State Machine** | OrderStatus com transições válidas |
| **Rich Domain Model** | Entidades com comportamento, não sacos de dados |
| **Domain Exceptions** | Exceções específicas do domínio |
| **Testes unitários** | xUnit + FluentAssertions, cobertura > 90% |

---

## 2. Decisões Arquiteturais

### 2.1 Rich Domain Model vs Anemic Domain Model

> 🧠 **Analogia — O Robô vs O Funcionário:** Imagine dois caixas de supermercado. O **robô (anêmico)** é um braço mecânico que só segura produtos — precisa de um *operador externo* que diga "agora some o total", "agora verifique o estoque", "agora aplique o desconto". Se o operador erra a ordem, dá caos. O **funcionário (rico)** sabe suas regras: "só aceito cupom válido", "não vendo álcool sem RG", "calculo o troco automaticamente". Você diz "adiciona este produto" e ele *se vira*. **O modelo rico é o funcionário — dados + comportamento + regras, tudo junto.**

**Anemic Domain Model (EVITAR):**
```csharp
// ❌ Entidade anêmica — apenas dados, sem comportamento
public class Order
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "Pending";
    public List<OrderItem> Items { get; set; } = [];
    public decimal Total { get; set; }
}

// A lógica fica solta em um "service"
public class OrderService
{
    public void AddItem(Order order, Product product, int qty)
    {
        order.Items.Add(new OrderItem { ... });
        order.Total = order.Items.Sum(i => i.Price * i.Quantity);
    }
}
```

**Rich Domain Model (USAR):**
```csharp
// ✅ Entidade rica — encapsula comportamento e protege invariantes
public sealed class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = [];

    // O próprio aggregate adiciona itens e garante regras
    public void AddItem(Guid productId, string productName, Money unitPrice, int quantity)
    {
        if (_status != OrderStatus.Pending)
            throw new OrderDomainException("Can only add items to pending orders.");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem is not null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            _items.Add(OrderItem.Create(Id, productId, productName, unitPrice, quantity));
        }

        RecalculateTotal();
        AddDomainEvent(new OrderItemAddedDomainEvent(Id, productId, quantity));
    }
}
```

**Por que Rich?**
1. **Invariantes protegidas** — Ninguém pode colocar o Order em estado inválido
2. **Comportamento junto dos dados** — OOP real, não procedural com classes
3. **Testável sem infraestrutura** — Testa o domínio sem banco, sem HTTP
4. **É o que DDD pede** — E o que entrevistadores procuram

### 2.2 Aggregate Boundaries

> 🧠 **Analogia — A Caixa-Forte do Banco:** Um Aggregate é como uma **caixa-forte**: tudo lá dentro (notas, joias, documentos) é gerenciado como uma unidade. Você não tira uma nota sem passar pela porta da caixa-forte (o Aggregate Root). E você não mistura o conteúdo de duas caixas-fortes — cada uma tem seu próprio inventario, seu próprio cadeado, suas próprias regras.

**Regra fundamental:** Um Aggregate é o menor grupo de objetos que deve ser consistente **juntos, na mesma transação**.

No nosso caso:
- **Order** é o Aggregate Root
- **OrderItem** é uma Entity dentro do aggregate
- **Money, Address, OrderNumber** são Value Objects

**Por que OrderItem está dentro do aggregate de Order?**
Porque um OrderItem não faz sentido sozinho. Ele sempre pertence a uma Order. Se eu adicionar ou remover um item, o total da Order precisa ser recalculado — **isso é consistência transacional**.

**Por que Product NÃO está no aggregate de Order?**
Porque Product pertence ao Catalog (outro bounded context). No Orders, mantemos apenas referências (ProductId, ProductName, UnitPrice no momento da compra).

### 2.3 Domain Events vs Integration Events

> 🧠 **Analogia — Grito na Sala vs Carta pelo Correio:** Um **Domain Event** é como gritar "o pedido foi criado!" dentro da mesma sala (mesmo processo, mesmo banco, mesma transação). Todos que estão ali ouvem instantâneamente. Um **Integration Event** é como enviar uma carta pelo correio para outro escritório (outro serviço, outro banco) — pode demorar, pode se perder, e o destinatário precisa confirmar recebimento. Domain Events são rápidos e confiáveis. Integration Events exigem **resiliência**: retry, idempotência, dead-letter queues.

| Domain Events | Integration Events |
|---------------|-------------------|
| Dentro do bounded context | Entre bounded contexts / serviços |
| In-process (MediatR) | Via message broker (MassTransit/RabbitMQ) |
| Síncrono na mesma transação | Assíncrono, eventualmente consistente |
| `OrderCreatedDomainEvent` | `OrderCreatedIntegrationEvent` |

**Nesta fase:** Criamos os Domain Events. Na Fase 5, transformamos os relevantes em Integration Events.

### 2.4 Value Object Equality

> 🧠 **Analogia — A Nota de R$10:** Você pega duas notas de R$10 do bolso. Elas são *a mesma nota*? Não — cada uma tem um número de série diferente. Mas elas *valem a mesma coisa*? Sim! Para o seu pagamento, tanto faz qual nota você usa — elas são **intercambiáveis pelo valor**. Isso é um Value Object: **não tem identidade própria, só importa o que ele carrega**. Dois `Money(100, "BRL")` são iguais porque representam o mesmo valor. Já uma `Entity` como Order tem identidade — dois pedidos de R$100 são pedidos *diferentes*.

Value Objects são comparados **por valor**, não por **referência**. Dois `Money.FromDecimal(100m, "BRL")` são iguais.

```csharp
var price1 = Money.FromDecimal(100m, "BRL");
var price2 = Money.FromDecimal(100m, "BRL");
price1 == price2; // true — mesmos valores
price1.Equals(price2); // true
```

Entities são comparadas por **identidade** (Id). Dois orders com dados idênticos mas IDs diferentes são diferentes.

---

## 3. Conceitos de DDD

> 🤔 **Pense antes de ler:**
> 1. O que impede alguém de criar uma Order com TotalAmount = -500? Quem deveria impedir — o banco de dados, o controller, ou o próprio objeto Order?
> 2. Se `Money` é imutável, como você "muda" o preço de um item? (Dica: você não muda — cria um novo.)
> 3. Por que `order.Cancel(reason)` é melhor que `order.Status = "Cancelled"`?
>
> Se você respondeu "o próprio objeto Order" para a pergunta 1, você já pensa em termos de DDD. O domínio rico coloca as regras **dentro** dos objetos, não espalhadas em services e controllers.

> 💡 **Antes de mergulhar no código:** DDD não é sobre patterns — é sobre **falar a língua do negócio** no código. Se o Product Owner diz "o cliente *cancela* o pedido", seu código deve ter `order.Cancel(reason)`, não `orderService.UpdateStatus(order, "cancelled")`. Os patterns abaixo (Aggregate, Value Object, Domain Event) são **ferramentas** para construir esse código expressivo.

### Aggregate Root

```
┌─────────────────────────────────────────────────────────┐
│                    ORDER (Aggregate Root)                │
│                                                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐    │
│  │  OrderItem  │  │  OrderItem  │  │  OrderItem  │    │
│  │  (Entity)   │  │  (Entity)   │  │  (Entity)   │    │
│  └─────────────┘  └─────────────┘  └─────────────┘    │
│                                                         │
│  ┌──────────┐  ┌───────────┐  ┌──────────────────┐    │
│  │  Money   │  │  Address  │  │  OrderNumber     │    │
│  │  (VO)    │  │  (VO)     │  │  (VO)            │    │
│  └──────────┘  └───────────┘  └──────────────────┘    │
│                                                         │
│  Invariantes:                                           │
│  • Pedido deve ter pelo menos 1 item                    │
│  • Total = soma dos itens                               │
│  • Apenas pending orders podem ter itens adicionados    │
│  • Status segue state machine                           │
└─────────────────────────────────────────────────────────┘

  Acesso externo APENAS pela raiz (Order).
  Nunca manipule OrderItem diretamente de fora.
```

### State Machine do OrderStatus

```
                    ┌──────────┐
                    │ Pending  │
                    └────┬─────┘
                         │
                ┌────────┴────────┐
                │                 │
         ┌──────▼──────┐   ┌─────▼──────┐
         │ Confirmed   │   │ Cancelled  │
         └──────┬──────┘   └────────────┘
                │
         ┌──────▼──────┐
         │  Shipped    │
         └──────┬──────┘
                │
         ┌──────▼──────┐
         │ Delivered   │
         └─────────────┘

Transições válidas:
  Pending    → Confirmed, Cancelled
  Confirmed  → Shipped, Cancelled
  Shipped    → Delivered
  Delivered  → (final)
  Cancelled  → (final)
```

---

## 4. Passo a Passo de Implementação

### 4.1 Criar os Projetos do Orders

```bash
# Domain
dotnet new classlib -n OrderFlow.Orders.Domain -o src/Services/Orders/OrderFlow.Orders.Domain
dotnet sln add src/Services/Orders/OrderFlow.Orders.Domain
dotnet add src/Services/Orders/OrderFlow.Orders.Domain reference src/BuildingBlocks/OrderFlow.SharedKernel

# Domain Tests
dotnet new xunit -n OrderFlow.Orders.Domain.Tests -o tests/OrderFlow.Orders.Domain.Tests
dotnet sln add tests/OrderFlow.Orders.Domain.Tests
dotnet add tests/OrderFlow.Orders.Domain.Tests reference src/Services/Orders/OrderFlow.Orders.Domain
dotnet add tests/OrderFlow.Orders.Domain.Tests package FluentAssertions
```

### 4.2 Estrutura de Pastas

```bash
# Domain
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/Aggregates/OrderAggregate
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/ValueObjects
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/Events
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/Exceptions
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/Interfaces
```

---

## 5. Código de Referência Completo

### 5.1 SharedKernel — AggregateRoot e ValueObject Base

**`src/BuildingBlocks/OrderFlow.SharedKernel/AggregateRoot.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public abstract class AggregateRoot : AuditableEntity
{
    // Aggregate root herda Entity (que já tem DomainEvents)
    // A diferença semântica é que apenas AggregateRoots são "raízes" de repositórios
}
```

**`src/BuildingBlocks/OrderFlow.SharedKernel/ValueObject.cs`**

```csharp
namespace OrderFlow.SharedKernel;

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        return Equals((ValueObject)obj);
    }

    public bool Equals(ValueObject? other)
    {
        if (other is null)
            return false;

        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(0, (current, component) =>
                HashCode.Combine(current, component?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
```

> 🤔 **"Por que não usar `record` para Value Objects?"** C# `record` já tem value equality built-in — por que essa classe base? Boas razões: (1) `record` compara **todas** as propriedades automaticamente — sem controle sobre *quais* componentes participam da igualdade. (2) Com `GetEqualityComponents()`, você pode excluir campos derivados ou incluir lógica customizada. (3) `record` não integra facilmente com EF Core Owned Types (precisa de construtores específicos). (4) A classe base fornece **consistência** — todo VO do projeto segue o mesmo padrão. **Dito isso:** para VOs simples (2-3 propriedades, sem lógica), `record struct` é perfeitamente válido e mais conciso.

### 5.2 Domain Exceptions

**`src/Services/Orders/OrderFlow.Orders.Domain/Exceptions/OrderDomainException.cs`**

```csharp
namespace OrderFlow.Orders.Domain.Exceptions;

public sealed class OrderDomainException : Exception
{
    public string Code { get; }

    public OrderDomainException(string message, string code = "ORDER_DOMAIN_ERROR")
        : base(message)
    {
        Code = code;
    }

    public OrderDomainException(string message, string code, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
```

### 5.3 Value Objects

**`src/Services/Orders/OrderFlow.Orders.Domain/ValueObjects/Money.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money FromDecimal(decimal amount, string currency = "BRL")
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency code is required.", nameof(currency));

        return new Money(Math.Round(amount, 2), currency.ToUpperInvariant());
    }

    public static Money Zero(string currency = "BRL") => new(0m, currency.ToUpperInvariant());

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var result = Amount - other.Amount;
        if (result < 0)
            throw new InvalidOperationException("Result of subtraction cannot be negative.");
        return new Money(result, Currency);
    }

    public Money Multiply(int quantity)
    {
        if (quantity < 0)
            throw new ArgumentException("Quantity cannot be negative.", nameof(quantity));
        return new Money(Amount * quantity, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException(
                $"Cannot operate on different currencies: {Currency} and {other.Currency}");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Currency} {Amount:N2}";
}
```

**`src/Services/Orders/OrderFlow.Orders.Domain/ValueObjects/Address.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; }
    public string Number { get; }
    public string? Complement { get; }
    public string Neighborhood { get; }
    public string City { get; }
    public string State { get; }
    public string ZipCode { get; }
    public string Country { get; }

    private Address(
        string street, string number, string? complement,
        string neighborhood, string city, string state,
        string zipCode, string country)
    {
        Street = street;
        Number = number;
        Complement = complement;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        ZipCode = zipCode;
        Country = country;
    }

    public static Address Create(
        string street, string number, string neighborhood,
        string city, string state, string zipCode,
        string? complement = null, string country = "Brasil")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(neighborhood);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode);

        return new Address(
            street.Trim(), number.Trim(), complement?.Trim(),
            neighborhood.Trim(), city.Trim(), state.Trim(),
            zipCode.Trim().Replace("-", ""), country.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return Number;
        yield return Complement;
        yield return Neighborhood;
        yield return City;
        yield return State;
        yield return ZipCode;
        yield return Country;
    }

    public override string ToString() =>
        $"{Street}, {Number}{(Complement is not null ? $" - {Complement}" : "")}, " +
        $"{Neighborhood}, {City}/{State}, {ZipCode}";
}
```

**`src/Services/Orders/OrderFlow.Orders.Domain/ValueObjects/OrderNumber.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.ValueObjects;

public sealed class OrderNumber : ValueObject
{
    public string Value { get; }

    private OrderNumber(string value)
    {
        Value = value;
    }

    public static OrderNumber Create()
    {
        // Formato: ORD-YYYYMMDD-XXXXX (ex: ORD-20260415-A3F8B)
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomPart = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        return new OrderNumber($"ORD-{datePart}-{randomPart}");
    }

    public static OrderNumber FromExisting(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new OrderNumber(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

### 5.4 OrderStatus — Value Object com State Machine

**`src/Services/Orders/OrderFlow.Orders.Domain/Aggregates/OrderAggregate/OrderStatus.cs`**

```csharp
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

public sealed class OrderStatus : ValueObject
{
    public static readonly OrderStatus Pending = new("Pending");
    public static readonly OrderStatus Confirmed = new("Confirmed");
    public static readonly OrderStatus Shipped = new("Shipped");
    public static readonly OrderStatus Delivered = new("Delivered");
    public static readonly OrderStatus Cancelled = new("Cancelled");

    public string Value { get; }

    private OrderStatus(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Transições válidas de status.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        [Pending.Value] = [Confirmed.Value, Cancelled.Value],
        [Confirmed.Value] = [Shipped.Value, Cancelled.Value],
        [Shipped.Value] = [Delivered.Value],
        [Delivered.Value] = [],
        [Cancelled.Value] = []
    };

    public bool CanTransitionTo(OrderStatus newStatus)
    {
        return ValidTransitions.TryGetValue(Value, out var validTargets)
               && validTargets.Contains(newStatus.Value);
    }

    public OrderStatus TransitionTo(OrderStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
        {
            throw new OrderDomainException(
                $"Invalid status transition from '{Value}' to '{newStatus.Value}'.",
                "INVALID_STATUS_TRANSITION");
        }

        return newStatus;
    }

    public bool IsFinal => this == Delivered || this == Cancelled;

    public static OrderStatus FromString(string status)
    {
        return status switch
        {
            "Pending" => Pending,
            "Confirmed" => Confirmed,
            "Shipped" => Shipped,
            "Delivered" => Delivered,
            "Cancelled" => Cancelled,
            _ => throw new OrderDomainException($"Unknown order status: '{status}'.", "UNKNOWN_STATUS")
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

### 5.5 Domain Events

**`src/Services/Orders/OrderFlow.Orders.Domain/Events/OrderCreatedDomainEvent.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Events;

public sealed record OrderCreatedDomainEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime OccurredOn) : IDomainEvent
{
    public OrderCreatedDomainEvent(Guid orderId, string orderNumber, Guid customerId, decimal totalAmount)
        : this(orderId, orderNumber, customerId, totalAmount, DateTime.UtcNow) { }
}
```

**`src/Services/Orders/OrderFlow.Orders.Domain/Events/OrderItemAddedDomainEvent.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Events;

public sealed record OrderItemAddedDomainEvent(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredOn) : IDomainEvent
{
    public OrderItemAddedDomainEvent(Guid orderId, Guid productId, int quantity)
        : this(orderId, productId, quantity, DateTime.UtcNow) { }
}
```

**`src/Services/Orders/OrderFlow.Orders.Domain/Events/OrderConfirmedDomainEvent.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Events;

public sealed record OrderConfirmedDomainEvent(
    Guid OrderId,
    string OrderNumber,
    decimal TotalAmount,
    DateTime OccurredOn) : IDomainEvent
{
    public OrderConfirmedDomainEvent(Guid orderId, string orderNumber, decimal totalAmount)
        : this(orderId, orderNumber, totalAmount, DateTime.UtcNow) { }
}
```

**`src/Services/Orders/OrderFlow.Orders.Domain/Events/OrderCancelledDomainEvent.cs`**

```csharp
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Events;

public sealed record OrderCancelledDomainEvent(
    Guid OrderId,
    string OrderNumber,
    string Reason,
    DateTime OccurredOn) : IDomainEvent
{
    public OrderCancelledDomainEvent(Guid orderId, string orderNumber, string reason)
        : this(orderId, orderNumber, reason, DateTime.UtcNow) { }
}
```

### 5.6 OrderItem — Entity dentro do Aggregate

**`src/Services/Orders/OrderFlow.Orders.Domain/Aggregates/OrderAggregate/OrderItem.cs`**

```csharp
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

public sealed class OrderItem : Entity
{
    public Guid OrderId { get; private init; }
    public Guid ProductId { get; private init; }
    public string ProductName { get; private init; } = string.Empty;
    public Money UnitPrice { get; private init; } = null!;
    public int Quantity { get; private set; }

    public Money TotalPrice => UnitPrice.Multiply(Quantity);

    private OrderItem() { } // EF Core

    internal static OrderItem Create(
        Guid orderId,
        Guid productId,
        string productName,
        Money unitPrice,
        int quantity)
    {
        if (quantity <= 0)
            throw new OrderDomainException("Quantity must be at least 1.", "INVALID_QUANTITY");

        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity
        };
    }

    internal void IncreaseQuantity(int additionalQuantity)
    {
        if (additionalQuantity <= 0)
            throw new OrderDomainException("Additional quantity must be positive.", "INVALID_QUANTITY");

        Quantity += additionalQuantity;
    }

    internal void DecreaseQuantity(int reduceBy)
    {
        if (reduceBy <= 0)
            throw new OrderDomainException("Reduce quantity must be positive.", "INVALID_QUANTITY");

        if (Quantity - reduceBy < 1)
            throw new OrderDomainException(
                "Cannot reduce quantity below 1. Remove the item instead.", "INVALID_QUANTITY");

        Quantity -= reduceBy;
    }
}
```

> **Nota:** Os métodos são `internal`, não `public`. Isso significa que **apenas outros tipos dentro do mesmo assembly (Orders.Domain) podem chamar esses métodos**. O acesso externo é SEMPRE pela Order (Aggregate Root).

### 5.7 Order — O Aggregate Root

**`src/Services/Orders/OrderFlow.Orders.Domain/Aggregates/OrderAggregate/Order.cs`**

```csharp
using OrderFlow.Orders.Domain.Events;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

public sealed class Order : AggregateRoot
{
    public OrderNumber OrderNumber { get; private init; } = null!;
    public Guid CustomerId { get; private init; }
    public Address ShippingAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;
    public string? CancellationReason { get; private set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { } // EF Core

    // === FACTORY METHOD ===
    public static Order Create(Guid customerId, Address shippingAddress)
    {
        if (customerId == Guid.Empty)
            throw new OrderDomainException("Customer ID is required.", "INVALID_CUSTOMER");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = OrderNumber.Create(),
            CustomerId = customerId,
            ShippingAddress = shippingAddress,
            Status = OrderStatus.Pending,
            TotalAmount = Money.Zero(),
            CreatedAt = DateTime.UtcNow
        };

        order.AddDomainEvent(new OrderCreatedDomainEvent(
            order.Id, order.OrderNumber.Value, customerId, 0m));

        return order;
    }

    // === BEHAVIORS ===

    public void AddItem(Guid productId, string productName, Money unitPrice, int quantity)
    {
        EnsurePendingStatus("add items");

        if (productId == Guid.Empty)
            throw new OrderDomainException("Product ID is required.", "INVALID_PRODUCT");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);

        if (existingItem is not null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            var item = OrderItem.Create(Id, productId, productName, unitPrice, quantity);
            _items.Add(item);
        }

        RecalculateTotal();
        SetUpdated();

        AddDomainEvent(new OrderItemAddedDomainEvent(Id, productId, quantity));
    }

    public void RemoveItem(Guid productId)
    {
        EnsurePendingStatus("remove items");

        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new OrderDomainException(
                $"Item with product ID '{productId}' not found in order.", "ITEM_NOT_FOUND");

        _items.Remove(item);
        RecalculateTotal();
        SetUpdated();
    }

    public void Confirm()
    {
        if (!_items.Any())
            throw new OrderDomainException(
                "Cannot confirm an order with no items.", "EMPTY_ORDER");

        Status = Status.TransitionTo(OrderStatus.Confirmed);
        SetUpdated();

        AddDomainEvent(new OrderConfirmedDomainEvent(Id, OrderNumber.Value, TotalAmount.Amount));
    }

    public void Ship()
    {
        Status = Status.TransitionTo(OrderStatus.Shipped);
        SetUpdated();
    }

    public void Deliver()
    {
        Status = Status.TransitionTo(OrderStatus.Delivered);
        SetUpdated();
    }

    public void Cancel(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Status = Status.TransitionTo(OrderStatus.Cancelled);
        CancellationReason = reason.Trim();
        SetUpdated();

        AddDomainEvent(new OrderCancelledDomainEvent(Id, OrderNumber.Value, reason));
    }

    public void UpdateShippingAddress(Address newAddress)
    {
        EnsurePendingStatus("update shipping address");

        ShippingAddress = newAddress;
        SetUpdated();
    }

    // === PRIVATE METHODS ===

    private void RecalculateTotal()
    {
        TotalAmount = _items.Aggregate(
            Money.Zero(),
            (total, item) => total.Add(item.TotalPrice));
    }

    private void EnsurePendingStatus(string action)
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException(
                $"Cannot {action} for an order with status '{Status.Value}'.",
                "INVALID_ORDER_STATUS");
    }
}
```

### 5.8 Repository Interface

**`src/Services/Orders/OrderFlow.Orders.Domain/Interfaces/IOrderRepository.cs`**

```csharp
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
}
```

---

## 6. Testes

> 🧠 **Analogia — O Crash Test do Carro:** Testes de domínio são como **crash tests** — você coloca o carro (aggregate) em situações extremas (adicionar item em pedido cancelado, criar Money com valor negativo) e verifica se as proteções funcionam. Um carro sem crash test pode parecer perfeito, mas ninguém confia nele. Domínio sem testes? Mesma coisa. A beleza dos testes de domínio é que são **os mais rápidos e baratos** de todo o sistema — sem banco, sem HTTP, sem Docker. Rodam em milissegundos.

### 6.1 Filosofia de Testes do Domínio

| Princípio | Aplicação |
|-----------|-----------|
| **Teste o comportamento, não a implementação** | Testar `order.AddItem()`, não o setter de uma propriedade |
| **Um conceito por teste** | Cada teste valida uma coisa |
| **Naming:** `Method_State_Expected` | `AddItem_WhenOrderIsPending_AddsItemAndRecalculatesTotal` |
| **Sem mocks** | Domínio puro não precisa de mocks |
| **Arrange-Act-Assert** | Estrutura clara e consistente |

### 6.2 Money Tests

**`tests/OrderFlow.Orders.Domain.Tests/ValueObjects/MoneyTests.cs`**

```csharp
using FluentAssertions;
using OrderFlow.Orders.Domain.ValueObjects;

namespace OrderFlow.Orders.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void FromDecimal_ValidAmount_CreatesMoney()
    {
        var money = Money.FromDecimal(100.50m);

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("BRL");
    }

    [Fact]
    public void FromDecimal_NegativeAmount_ThrowsArgumentException()
    {
        var act = () => Money.FromDecimal(-10m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromDecimal_RoundsToTwoDecimals()
    {
        var money = Money.FromDecimal(100.555m);

        money.Amount.Should().Be(100.56m);
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSummedMoney()
    {
        var a = Money.FromDecimal(100m);
        var b = Money.FromDecimal(50m);

        var result = a.Add(b);

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Add_DifferentCurrency_ThrowsInvalidOperation()
    {
        var brl = Money.FromDecimal(100m, "BRL");
        var usd = Money.FromDecimal(50m, "USD");

        var act = () => brl.Add(usd);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Multiply_ByQuantity_ReturnsCorrectAmount()
    {
        var unitPrice = Money.FromDecimal(49.99m);

        var total = unitPrice.Multiply(3);

        total.Amount.Should().Be(149.97m);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = Money.FromDecimal(100m, "BRL");
        var b = Money.FromDecimal(100m, "BRL");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = Money.FromDecimal(100m);
        var b = Money.FromDecimal(200m);

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Zero_ReturnsZeroAmount()
    {
        var zero = Money.Zero();

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("BRL");
    }
}
```

### 6.3 OrderStatus Tests

**`tests/OrderFlow.Orders.Domain.Tests/Aggregates/OrderStatusTests.cs`**

```csharp
using FluentAssertions;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Exceptions;

namespace OrderFlow.Orders.Domain.Tests.Aggregates;

public class OrderStatusTests
{
    [Theory]
    [InlineData("Pending", "Confirmed", true)]
    [InlineData("Pending", "Cancelled", true)]
    [InlineData("Confirmed", "Shipped", true)]
    [InlineData("Confirmed", "Cancelled", true)]
    [InlineData("Shipped", "Delivered", true)]
    [InlineData("Pending", "Shipped", false)]
    [InlineData("Pending", "Delivered", false)]
    [InlineData("Shipped", "Cancelled", false)]
    [InlineData("Delivered", "Pending", false)]
    [InlineData("Cancelled", "Pending", false)]
    public void CanTransitionTo_ValidatesCorrectly(
        string from, string to, bool expectedResult)
    {
        var fromStatus = OrderStatus.FromString(from);
        var toStatus = OrderStatus.FromString(to);

        fromStatus.CanTransitionTo(toStatus).Should().Be(expectedResult);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_ThrowsOrderDomainException()
    {
        var status = OrderStatus.Pending;

        var act = () => status.TransitionTo(OrderStatus.Delivered);

        act.Should().Throw<OrderDomainException>()
            .WithMessage("*Invalid status transition*");
    }

    [Fact]
    public void IsFinal_DeliveredAndCancelled_ReturnsTrue()
    {
        OrderStatus.Delivered.IsFinal.Should().BeTrue();
        OrderStatus.Cancelled.IsFinal.Should().BeTrue();
    }

    [Fact]
    public void IsFinal_NonFinalStatuses_ReturnsFalse()
    {
        OrderStatus.Pending.IsFinal.Should().BeFalse();
        OrderStatus.Confirmed.IsFinal.Should().BeFalse();
        OrderStatus.Shipped.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void FromString_UnknownStatus_ThrowsOrderDomainException()
    {
        var act = () => OrderStatus.FromString("Unknown");

        act.Should().Throw<OrderDomainException>();
    }
}
```

### 6.4 Order Aggregate Tests

**`tests/OrderFlow.Orders.Domain.Tests/Aggregates/OrderTests.cs`**

```csharp
using FluentAssertions;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Events;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.ValueObjects;

namespace OrderFlow.Orders.Domain.Tests.Aggregates;

public class OrderTests
{
    private static Address CreateTestAddress() =>
        Address.Create("Rua Exemplo", "123", "Centro", "São Paulo", "SP", "01001-000");

    private static Order CreatePendingOrder()
    {
        var order = Order.Create(Guid.NewGuid(), CreateTestAddress());
        order.ClearDomainEvents(); // Limpar evento de criação para testes isolados
        return order;
    }

    // === Factory Tests ===

    [Fact]
    public void Create_ValidInput_CreatesOrderWithPendingStatus()
    {
        var customerId = Guid.NewGuid();
        var address = CreateTestAddress();

        var order = Order.Create(customerId, address);

        order.Id.Should().NotBeEmpty();
        order.CustomerId.Should().Be(customerId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Amount.Should().Be(0m);
        order.Items.Should().BeEmpty();
        order.OrderNumber.Value.Should().StartWith("ORD-");
    }

    [Fact]
    public void Create_ValidInput_RaisesOrderCreatedDomainEvent()
    {
        var order = Order.Create(Guid.NewGuid(), CreateTestAddress());

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCreatedDomainEvent>();
    }

    [Fact]
    public void Create_EmptyCustomerId_ThrowsOrderDomainException()
    {
        var act = () => Order.Create(Guid.Empty, CreateTestAddress());

        act.Should().Throw<OrderDomainException>();
    }

    // === AddItem Tests ===

    [Fact]
    public void AddItem_WhenPending_AddsItemAndUpdatesTotal()
    {
        var order = CreatePendingOrder();
        var unitPrice = Money.FromDecimal(49.99m);

        order.AddItem(Guid.NewGuid(), "Laptop", unitPrice, 2);

        order.Items.Should().HaveCount(1);
        order.TotalAmount.Amount.Should().Be(99.98m);
    }

    [Fact]
    public void AddItem_SameProductTwice_IncreasesQuantity()
    {
        var order = CreatePendingOrder();
        var productId = Guid.NewGuid();
        var unitPrice = Money.FromDecimal(10m);

        order.AddItem(productId, "Mouse", unitPrice, 2);
        order.AddItem(productId, "Mouse", unitPrice, 3);

        order.Items.Should().HaveCount(1);
        order.Items.First().Quantity.Should().Be(5);
        order.TotalAmount.Amount.Should().Be(50m);
    }

    [Fact]
    public void AddItem_MultipleProducts_CalculatesTotalCorrectly()
    {
        var order = CreatePendingOrder();

        order.AddItem(Guid.NewGuid(), "Laptop", Money.FromDecimal(2000m), 1);
        order.AddItem(Guid.NewGuid(), "Mouse", Money.FromDecimal(50m), 2);
        order.AddItem(Guid.NewGuid(), "Keyboard", Money.FromDecimal(150m), 1);

        order.Items.Should().HaveCount(3);
        order.TotalAmount.Amount.Should().Be(2250m); // 2000 + 100 + 150
    }

    [Fact]
    public void AddItem_WhenConfirmed_ThrowsOrderDomainException()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(10m), 1);
        order.Confirm();

        var act = () => order.AddItem(Guid.NewGuid(), "Another", Money.FromDecimal(20m), 1);

        act.Should().Throw<OrderDomainException>()
            .WithMessage("*Cannot add items*");
    }

    [Fact]
    public void AddItem_RaisesOrderItemAddedDomainEvent()
    {
        var order = CreatePendingOrder();

        order.AddItem(Guid.NewGuid(), "Product", Money.FromDecimal(10m), 1);

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderItemAddedDomainEvent>();
    }

    // === RemoveItem Tests ===

    [Fact]
    public void RemoveItem_ExistingProduct_RemovesAndRecalculates()
    {
        var order = CreatePendingOrder();
        var productId = Guid.NewGuid();
        order.AddItem(productId, "Item", Money.FromDecimal(100m), 1);
        order.AddItem(Guid.NewGuid(), "Other", Money.FromDecimal(50m), 1);

        order.RemoveItem(productId);

        order.Items.Should().HaveCount(1);
        order.TotalAmount.Amount.Should().Be(50m);
    }

    [Fact]
    public void RemoveItem_NonExistentProduct_ThrowsOrderDomainException()
    {
        var order = CreatePendingOrder();

        var act = () => order.RemoveItem(Guid.NewGuid());

        act.Should().Throw<OrderDomainException>()
            .WithMessage("*not found*");
    }

    // === Confirm Tests ===

    [Fact]
    public void Confirm_WithItems_ChangesStatusToConfirmed()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_WithoutItems_ThrowsOrderDomainException()
    {
        var order = CreatePendingOrder();

        var act = () => order.Confirm();

        act.Should().Throw<OrderDomainException>()
            .WithMessage("*no items*");
    }

    [Fact]
    public void Confirm_RaisesOrderConfirmedDomainEvent()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);
        order.ClearDomainEvents();

        order.Confirm();

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderConfirmedDomainEvent>();
    }

    // === State Machine Flow ===

    [Fact]
    public void FullLifecycle_PendingToDelivered_Works()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);

        order.Confirm();
        order.Status.Should().Be(OrderStatus.Confirmed);

        order.Ship();
        order.Status.Should().Be(OrderStatus.Shipped);

        order.Deliver();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Cancel_PendingOrder_ChangesStatusToCancelled()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);

        order.Cancel("Customer changed their mind");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Customer changed their mind");
    }

    [Fact]
    public void Cancel_DeliveredOrder_ThrowsOrderDomainException()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);
        order.Confirm();
        order.Ship();
        order.Deliver();

        var act = () => order.Cancel("Too late");

        act.Should().Throw<OrderDomainException>();
    }

    [Fact]
    public void Cancel_RaisesOrderCancelledDomainEvent()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);
        order.ClearDomainEvents();

        order.Cancel("Reason");

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCancelledDomainEvent>();
    }
}
```

### 6.5 Address Tests

**`tests/OrderFlow.Orders.Domain.Tests/ValueObjects/AddressTests.cs`**

```csharp
using FluentAssertions;
using OrderFlow.Orders.Domain.ValueObjects;

namespace OrderFlow.Orders.Domain.Tests.ValueObjects;

public class AddressTests
{
    [Fact]
    public void Create_ValidInput_CreatesAddress()
    {
        var address = Address.Create(
            "Rua das Flores", "100", "Jardim Primavera",
            "São Paulo", "SP", "01234-567");

        address.Street.Should().Be("Rua das Flores");
        address.Number.Should().Be("100");
        address.City.Should().Be("São Paulo");
        address.ZipCode.Should().Be("01234567"); // Sem o hífen
    }

    [Fact]
    public void Create_MissingStreet_ThrowsArgumentException()
    {
        var act = () => Address.Create(
            "", "100", "Bairro", "Cidade", "SP", "01234567");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = Address.Create("Rua A", "1", "Bairro", "SP", "SP", "01001000");
        var b = Address.Create("Rua A", "1", "Bairro", "SP", "SP", "01001000");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = Address.Create("Rua A", "1", "Bairro", "SP", "SP", "01001000");
        var b = Address.Create("Rua B", "2", "Bairro", "SP", "SP", "01001000");

        a.Should().NotBe(b);
    }
}
```

---

## ⚠️ Erros Comuns em DDD

> Estes são os erros mais frequentes na primeira implementação de um domínio rico. Cada um deles é um padrão que já queimou projetos reais.

| # | Erro | Por Que é Perigoso | Como Evitar |
|---|---|---|---|
| 1 | **Aggregate muito grande** — colocar Product, Customer e Order no mesmo aggregate | Lock contention em transações, performance degradada, serialização gigante | Um aggregate = uma boundary de consistência. Se não precisa ser consistente *atomicamente*, são aggregates separados |
| 2 | **Expor `List<OrderItem>` como propriedade pública** | Qualquer código externo pode `.Add()`, `.Clear()`, `.Remove()` — bypassando validações do aggregate | Use `IReadOnlyCollection<T>` para leitura. Mutações só via métodos do aggregate (`AddItem`, `RemoveItem`) |
| 3 | **Value Object com setter público** | Destrói imutabilidade, quebra equality (dois VOs "iguais" podem mudar independentemente) | Properties devem ser `init` ou no setter. Mudança = criar nova instância |
| 4 | **Domain Event com lógica** | Eventos devem ser DTOs imutáveis que registram *o que aconteceu* — não ter comportamento | Use `sealed record` para eventos. Lógica fica nos handlers |
| 5 | **Testar com `new Order()` direto** | Cria objeto em estado potencialmente inválido, bypassa factory method | Sempre use `Order.Create(...)` nos testes. Se não compila com `new`, é feature — não bug |
| 6 | **Status como `string` em vez de enum/VO** | `"Pending"`, `"pending"`, `"PENDING"`, `"peding"` — tipografia é o inimigo | Use enum ou Value Object com transições validadas. `OrderStatus.Pending` é type-safe |

### O Que Acontece Quando Você Viola uma Invariante

> 💡 **Exemplo prático:** Quando alguém tenta confirmar um pedido sem itens, o domínio lança `OrderDomainException("Cannot confirm order without items")`. Na Fase 03, o middleware de exceção converte isso em HTTP 400:
> ```json
> {
>   "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
>   "title": "Domain Error",
>   "status": 400,
>   "detail": "Cannot confirm order without items."
> }
> ```
> O controller **nunca** precisa verificar se há itens — o domínio garante. Essa é a essência do Rich Domain Model.

---

## 🔧 Troubleshooting — Fase 02

| Sintoma | Causa Provável | Solução |
|---------|---------------|---------|
| Teste falha: `DomainEvents.Should().ContainSingle()` retorna 0 eventos | `ClearDomainEvents()` chamado no helper sem perceber, ou factory method não emite evento | Verifique se `Order.Create(...)` chama `AddDomainEvent(new OrderCreatedDomainEvent(...))` |
| `OrderNumber.Create()` gera duplicatas | 5 chars hex = ~1M combinações — colisão possível em alto volume | Para produção, use sequência do banco ou algoritmo com timestamp. A implementação didática é simplificada intencionalmente |
| "Cannot access private constructor" no EF Core | EF Core precisa de construtor sem parâmetros | Adicione `private Order() { }` — EF usa reflection para instanciar |
| Money equality falha no teste | `GetEqualityComponents()` não implementado, ou comparando com `==` sem override de operador | Verifique se `ValueObject` base class tem `operator ==` e `GetHashCode` corretos |
| "EF Core dá erro ao mapear Money" | Owned Types não configurados ainda | A persistência dos VOs será configurada na **Fase 03** com `OwnsOne<Money>(...)`. Se tentou rodar antes, é normal |

---

## 🔗 Conectando os Pontos

### O que veio da Fase 01 e usamos aqui

| Da Fase 01 | Usado como |
|-----------|-----------|
| `Entity` base class | `OrderItem` herda de `Entity` (tem identidade) |
| `AuditableEntity` | Poderia ser usado se quiséssemos `CreatedAt` auto |
| `IDomainEvent` | Todos os events (`OrderCreatedDomainEvent`, etc.) implementam |
| `IRepository<T>` | `IOrderRepository` herda e adiciona métodos específicos |
| `IUnitOfWork` | Será usado na Fase 03 para commit transacional |

### Preview: O que vem na Fase 03

> Na Fase 03, esses Domain Events que criamos serão **publicados via MediatR**. Os Value Objects serão **mapeados para SQL Server via EF Core Owned Types** (`OwnsOne<Money>(...)`). O `IOrderRepository` ganhará uma implementação concreta. E a separação leitura/escrita (CQRS) permitirá queries SQL puras via Dapper para performance máxima. **O domínio que construímos aqui é o coração — a Fase 03 dá vida a ele.**

---

## ⚠️ Erros Comuns em DDD

> Estes são os erros mais frequentes na primeira implementação de um domínio rico. Cada um deles é um padrão que já queimou projetos reais.

| # | Erro | Por Que é Perigoso | Como Evitar |
|---|---|---|---|
| 1 | **Aggregate muito grande** — colocar Product, Customer e Order no mesmo aggregate | Lock contention em transações, performance degradada, serialização gigante | Um aggregate = uma boundary de consistência. Se não precisa ser consistente *atomicamente*, são aggregates separados |
| 2 | **Expor `List<OrderItem>` como propriedade pública** | Qualquer código externo pode `.Add()`, `.Clear()`, `.Remove()` — bypassando validações do aggregate | Use `IReadOnlyCollection<T>` para leitura. Mutações só via métodos do aggregate (`AddItem`, `RemoveItem`) |
| 3 | **Value Object com setter público** | Destrói imutabilidade, quebra equality (dois VOs "iguais" podem mudar independentemente) | Properties devem ser `init` ou no setter. Mudança = criar nova instância |
| 4 | **Domain Event com lógica** | Eventos devem ser DTOs imutáveis que registram *o que aconteceu* — não ter comportamento | Use `sealed record` para eventos. Lógica fica nos handlers |
| 5 | **Testar com `new Order()` direto** | Cria objeto em estado potencialmente inválido, bypassa factory method | Sempre use `Order.Create(...)` nos testes. Se não compila com `new`, é feature — não bug |
| 6 | **Status como `string` em vez de enum/VO** | `"Pending"`, `"pending"`, `"PENDING"`, `"peding"` — tipografia é o inimigo | Use enum ou Value Object com transições validadas. `OrderStatus.Pending` é type-safe |

### O Que Acontece Quando Você Viola uma Invariante

> 💡 **Exemplo prático:** Quando alguém tenta confirmar um pedido sem itens, o domínio lança `OrderDomainException("Cannot confirm order without items")`. Na Fase 03, o middleware de exceção converte isso em HTTP 400:
> ```json
> {
>   "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
>   "title": "Domain Error",
>   "status": 400,
>   "detail": "Cannot confirm order without items."
> }
> ```
> O controller **nunca** precisa verificar se há itens — o domínio garante. Essa é a essência do Rich Domain Model.

---

## 🔧 Troubleshooting — Fase 02

| Sintoma | Causa Provável | Solução |
|---------|---------------|---------|
| Teste falha: `DomainEvents.Should().ContainSingle()` retorna 0 eventos | `ClearDomainEvents()` chamado no helper sem perceber, ou factory method não emite evento | Verifique se `Order.Create(...)` chama `AddDomainEvent(new OrderCreatedDomainEvent(...))` |
| `OrderNumber.Create()` gera duplicatas | 5 chars hex = ~1M combinações — colisão possível em alto volume | Para produção, use sequência do banco ou algoritmo com timestamp. A implementação didática é simplificada intencionalmente |
| "Cannot access private constructor" no EF Core | EF Core precisa de construtor sem parâmetros | Adicione `private Order() { }` — EF usa reflection para instanciar |
| Money equality falha no teste | `GetEqualityComponents()` não implementado, ou comparando com `==` sem override de operador | Verifique se `ValueObject` base class tem `operator ==` e `GetHashCode` corretos |
| "EF Core dá erro ao mapear Money" | Owned Types não configurados ainda | A persistência dos VOs será configurada na **Fase 03** com `OwnsOne<Money>(...)`. Se tentou rodar antes, é normal |

---

## 🔗 Conectando os Pontos

### O que veio da Fase 01 e usamos aqui

| Da Fase 01 | Usado como |
|-----------|-----------|
| `Entity` base class | `OrderItem` herda de `Entity` (tem identidade) |
| `AuditableEntity` | Poderia ser usado se quiséssemos `CreatedAt` auto |
| `IDomainEvent` | Todos os events (`OrderCreatedDomainEvent`, etc.) implementam |
| `IRepository<T>` | `IOrderRepository` herda e adiciona métodos específicos |
| `IUnitOfWork` | Será usado na Fase 03 para commit transacional |

### Preview: O que vem na Fase 03

> Na Fase 03, esses Domain Events que criamos serão **publicados via MediatR**. Os Value Objects serão **mapeados para SQL Server via EF Core Owned Types** (`OwnsOne<Money>(...)`). O `IOrderRepository` ganhará uma implementação concreta. E a separação leitura/escrita (CQRS) permitirá queries SQL puras via Dapper para performance máxima. **O domínio que construímos aqui é o coração — a Fase 03 dá vida a ele.**

---

## 7. Checkpoint

> 💡 **Por que isso importa no dia-a-dia?** O domínio é o **coração** do sistema — é onde mora o dinheiro. Se o domínio permite que um pedido seja confirmado sem itens, ou que o total fique negativo, nenhuma camada de validação na API vai salvar você. Sêniores dizem: *"Se o domínio está correto, o resto é encanamento. Se o domínio está errado, o resto é ilusão."*

### Como Validar que a Fase 2 Está Completa

- [ ] **Projetos criados:** `OrderFlow.Orders.Domain` e `OrderFlow.Orders.Domain.Tests`
- [ ] **Referências corretas:** Domain ← SharedKernel
- [ ] **Value Objects implementados:** Money, Address, OrderNumber, OrderStatus
- [ ] **Aggregate Root implementado:** Order com factory method e behaviors
- [ ] **Entity implementada:** OrderItem com `internal` methods
- [ ] **Domain Events criados:** OrderCreated, OrderItemAdded, OrderConfirmed, OrderCancelled
- [ ] **Domain Exceptions:** `OrderDomainException` com code
- [ ] **State Machine:** Todas as transições validadas por testes
- [ ] **Encapsulamento:** Nenhuma propriedade com `public set`
- [ ] **Zero dependência de infra:** Domain não referencia EF Core, HTTP, etc.
- [ ] **Testes passam:** `dotnet test tests/OrderFlow.Orders.Domain.Tests`
- [ ] **Cobertura > 90%** no domínio
- [ ] **Commit:** `feat(orders): implement rich domain model with aggregates, value objects and domain events`

### Comandos de Verificação

```bash
# Build do domínio
dotnet build src/Services/Orders/OrderFlow.Orders.Domain

# Rodar testes do domínio
dotnet test tests/OrderFlow.Orders.Domain.Tests --verbosity normal

# Verificar que o domínio NÃO tem dependências externas
# O .csproj deve referenciar APENAS OrderFlow.SharedKernel
cat src/Services/Orders/OrderFlow.Orders.Domain/OrderFlow.Orders.Domain.csproj
```

---

## 📋 Resumo de Artefatos Criados

| Artefato | Arquivo/Local |
|----------|---------------|
| Aggregate Root | `Order.cs` (com AddItem, Confirm, Cancel, Ship) |
| Entity | `OrderItem.cs` (value dentro do aggregate) |
| Value Objects | `Money.cs`, `Address.cs`, `OrderNumber.cs`, `OrderStatus.cs` |
| Base Classes | `Entity.cs`, `AggregateRoot.cs`, `ValueObject.cs` (SharedKernel) |
| Domain Events | `OrderCreatedEvent.cs`, `OrderConfirmedEvent.cs`, `OrderCancelledEvent.cs` |
| Domain Errors | `OrderErrors` static class |
| Interface | `IOrderRepository.cs` |
| Testes | `OrderTests.cs`, `MoneyTests.cs`, `AddressTests.cs`, `OrderStatusTests.cs` |

---

## 💼 Perguntas Frequentes em Entrevistas — Fase 2

**1. "O que é um Aggregate no DDD? Dê um exemplo real."**
— Um Aggregate é um cluster de objetos de domínio tratados como unidade para mudanças de dados. Tem uma **raiz** (Aggregate Root) que é o único ponto de entrada. No OrderFlow: `Order` é a raiz, `OrderItem` é uma entidade interna. Você **nunca** acessa `OrderItem` diretamente — sempre via `Order.AddItem()`. Isso garante que invariantes (total > 0, status válido) sejam verificadas em toda operação.

**2. "Qual a diferença entre Entity e Value Object?"**
— **Entity** tem identidade (`Id`) — duas entidades com mesmos dados mas IDs diferentes são objetos diferentes. **Value Object** é definido por seus atributos — dois `Money(100, "BRL")` são iguais, não importa onde estão. Consequência: Value Objects são **imutáveis** (para mudar, crie um novo). No OrderFlow: `Order` é Entity (tem Id), `Money` é Value Object (100 reais = 100 reais).

**3. "Por que máquina de estados no OrderStatus e não if/else no Service?"**
— Centraliza regras de transição em um único lugar. Em vez de espalhar `if (status == Pending && newStatus == Confirmed)` por todo o código, a máquina de estados sabe quais transições são legais. `OrderStatus.CanTransitionTo(target)` é **puro domínio** — testável sem infraestrutura. Se adicionar um novo status (ex: `Refunded`), muda apenas o enum.

**4. "O que são Domain Events e para que servem?"**
— Domain Events são notificações de que algo aconteceu no domínio. `OrderCreatedEvent` não executa nada — apenas **avisa**. Quem se interessa (handlers) decide o que fazer: atualizar cache, publicar Integration Event, enviar notificação. Benefício: o domínio não conhece os side effects. Adicionar novo side effect = adicionar novo handler, sem alterar `Order.cs`.

**5. "Por que o Domain não pode ter dependências externas (NuGet, EF Core)?"**
— O domínio é a camada mais estável do sistema. Se depender de EF Core, uma breaking change no ORM quebra regras de negócio. Mantendo o domínio **puro** (só C# + SharedKernel), ele é testável sem banco, sem container, sem framework. É a essência da Dependency Rule: o centro não conhece a periferia.

---

## 🔬 Aprofundamento Sênior

### A1. Aggregate Design — Regras Não Óbvias

**Regra:** *"1 transação = 1 aggregate."*

Se você precisa atualizar 2 aggregates atomicamente, **eles deveriam ser um só** — ou você precisa de **eventual consistency** entre eles via Domain Event.

```csharp
// ❌ Anti-padrão: atualizar Order e Inventory na mesma transação
public async Task ConfirmAsync(Order order)
{
    order.Confirm();
    inventory.Decrement(order.Items);  // ← outro aggregate
    await _db.SaveChangesAsync();
}

// ✅ Correto: cada aggregate em sua transação, conectado por evento
public async Task ConfirmAsync(Order order)
{
    order.Confirm();   // emite OrderConfirmedEvent
    await _db.SaveChangesAsync();
}
// InventoryService recebe OrderConfirmedEvent e decrementa em SUA transação
```

### A2. Optimistic Concurrency em Aggregates

Dois usuários abrindo o mesmo pedido — quem confirma primeiro vence:

```csharp
public class Order : AggregateRoot
{
    public int Version { get; private set; }   // incrementa a cada mudança
    
    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Order cannot be confirmed in current state");
        Status = OrderStatus.Confirmed;
        Version++;
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }
}

// EF Core: builder.Property(o => o.Version).IsConcurrencyToken();
// Conflito → DbUpdateConcurrencyException → API retorna 409 Conflict
```

### A3. Domain Events vs Integration Events — Diferença Estrita

| | Domain Event | Integration Event |
|---|---|---|
| Audiência | **Dentro** do mesmo bounded context | **Outros** bounded contexts |
| Transporte | MediatR (in-process) | Broker (RabbitMQ, Kafka) |
| Disparo | Síncrono na transação | Assíncrono pós-commit |
| Acoplamento | Forte com domínio | Fraco — versionável |
| Exemplo | `OrderConfirmedEvent` (interno) | `OrderConfirmedIntegrationEvent` (Notification ouve) |

> **Regra:** nunca publique Domain Event fora do processo. Crie um handler que **traduz** Domain → Integration Event antes de publicar no broker.

### A4. Anti-Corruption Layer

Quando integrar com legado ou serviço externo com modelo incompatível, **não polua seu domínio**. Use ACL:

```csharp
public class LegacyCrmAdapter : ICustomerProvider
{
    public async Task<Customer> GetAsync(Guid id)
    {
        var raw = await _legacy.GET(id);   // modelo legado feio
        return new Customer(   // tradução para SEU modelo
            CustomerId.From(raw.cust_no),
            new CustomerName(raw.fname, raw.lname));
    }
}
```

### A5. Specification Pattern

Query complexa reusável sem vazar para Application:

```csharp
public sealed class HighValueOrderSpec : Specification<Order>
{
    public HighValueOrderSpec(decimal threshold)
        : base(o => o.Total.Amount > threshold && o.Status == OrderStatus.Confirmed) { }
}

var orders = await _repo.FindAsync(new HighValueOrderSpec(10_000m));
```

### A6. Event Sourcing — Preview

Em vez de salvar o **estado**, salva-se a sequência de **eventos**. Estado = `reduce(eventos)`. Aprofundamento completo na [Fase 13](./fase-13-grpc-kafka-eventsourcing.md#6-event-sourcing--conceito).

### 💼 Perguntas Sênior

**"Como você modelaria 'cada item do pedido pode ter desconto independente'?"** — `OrderItem` como entidade dentro do aggregate `Order`, com seu próprio `Discount` value object. `Order.AddItem(productId, qty, discount)` valida invariantes (desconto máximo, política de combinação). Nunca expor `Items` mutável — encapsular.

**"Como dois aggregates ficam consistentes sem 2PC?"** — Eventual consistency via Domain Event publicado pós-commit. Aggregate A salva, evento sai (Outbox), Aggregate B em outro processo recebe e atualiza. Aceita janela de inconsistência (ms a s) em troca de escalabilidade e resiliência.

---

> **Próximo passo:** Avance para `fase-03-cqrs-application.md` para implementar CQRS com MediatR.
>
> 🚀 **Trilha Sênior relacionada:** [`fase-13-grpc-kafka-eventsourcing.md`](./fase-13-grpc-kafka-eventsourcing.md) — Event Sourcing como modelo alternativo.

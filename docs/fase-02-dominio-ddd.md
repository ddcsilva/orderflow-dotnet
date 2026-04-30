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

> 📖 **Eric Evans — "Domain-Driven Design: Atacando as Complexidades no Coração do Software" (2003, Alta Books):**
> *"O objetivo do design orientado ao domínio é criar um modelo tão rico em conhecimento do domínio que qualquer desenvolvedor lendo o código consiga entender as regras de negócio sem sair da base de código."*
>
> Essa fase é onde essa visão se materializa. O código que vamos escrever **é** a documentação do negócio.

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

> 📐 **Mentalidade de Arquiteto:** Cada decisão nesta seção segue o formato **ADR (Architecture Decision Record)**, proposto por Michael Nygard. A ideia é simples: quando você toma uma decisão técnica, registre o **contexto** (por que estávamos discutindo isso), a **decisão** (o que escolhemos), as **alternativas** (o que descartamos e por quê) e as **consequências** (o que ganhamos e o que perdemos). Isso treina o músculo mais importante de um arquiteto: **pensar em trade-offs, não em soluções perfeitas**.
>
> 📖 **Vaughn Vernon — "Implementando Domain-Driven Design" (2013, Alta Books):**
> *"Toda decisão de design tem trade-offs. O papel do arquiteto não é evitar trade-offs, mas torná-los explícitos e intencionais."*

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

> 🔍 **Nota de Engenharia — O que há de errado acima:**
> - `public set` em todas as propriedades significa que **qualquer parte do código** pode mudar `Status` para `"banana"` ou `Total` para `-999`. Não há barreira.
> - `List<OrderItem>` exposta publicamente permite `.Clear()` direto, sem recalcular o total.
> - A lógica no `OrderService` opera sobre dados **externos** a ele — se outro service ou controller alterar `order.Items` diretamente, o total fica dessincronizado.
> - É programação **procedural disfarçada de OOP**: classes existem só como containers de dados, enquanto a lógica vive em outro lugar.

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

> 🔍 **Nota de Engenharia — O que está acontecendo acima:**
> - **`private readonly List<OrderItem> _items`**: A lista é backing field privado. Ninguém de fora acessa diretamente — o único jeito de modificar é via `AddItem()`. O `readonly` garante que a referência da lista não é substituída (a lista em si pode ter itens adicionados/removidos, mas apenas pelos métodos internos).
> - **Guard clause `if (_status != OrderStatus.Pending)`**: Fail-fast. Antes de qualquer operação, valida a pré-condição. Isso é **Design by Contract** (Bertrand Meyer) — o método tem um contrato: "só funciono se o pedido estiver pendente".
> - **`_items.FirstOrDefault(i => i.ProductId == productId)`**: Busca linear O(n) na lista. Para um pedido típico (5-20 itens), isso é eficiente. Se tivéssemos milhares de itens, trocaríamos para `Dictionary<Guid, OrderItem>` com busca O(1). A decisão de usar `List` é intencional: simplicidade > otimização prematura.
> - **Bifurcação `if (existingItem is not null)`**: Padrão "upsert" — se o produto já existe, incrementa a quantidade em vez de criar duplicata. Isso é uma **invariante de negócio**: "um pedido não pode ter duas linhas para o mesmo produto".
> - **`RecalculateTotal()`**: Garante que o total **nunca** fica dessincronizado dos itens. É chamado após toda mutação — esse é o padrão **Self-Consistency** do aggregate.
> - **`AddDomainEvent(...)`**: Registra o evento mas **não o publica** aqui. A publicação acontece no momento do `SaveChanges` (infraestrutura). Isso mantém o domínio puro.

**Por que Rich?**
1. **Invariantes protegidas** — Ninguém pode colocar o Order em estado inválido
2. **Comportamento junto dos dados** — OOP real, não procedural com classes
3. **Testável sem infraestrutura** — Testa o domínio sem banco, sem HTTP
4. **É o que DDD pede** — E o que entrevistadores procuram

> 📐 **ADR-001: Rich Domain Model sobre Anemic Domain Model**
>
> | Campo | Detalhe |
> |-------|---------|
> | **Contexto** | Precisamos modelar o domínio de Orders. Temos duas opções: modelo anêmico (dados + services separados) ou modelo rico (dados + comportamento na mesma classe). |
> | **Decisão** | Adotar **Rich Domain Model** para todas as entidades do bounded context de Orders. |
> | **Alternativas descartadas** | (1) **Anemic Model + Domain Services**: mais simples para CRUD, mas invariantes ficam espalhadas. (2) **Transaction Script**: procedural puro, sem modelagem OO — não escala em complexidade. |
> | **Consequências positivas** | Invariantes centralizadas, testabilidade sem infra, código que expressa a linguagem do negócio. |
> | **Consequências negativas** | Curva de aprendizado maior, configuração de ORM (EF Core Owned Types) mais trabalhosa, e serialização JSON exige cuidado. |
> | **Trade-off consciente** | Aceitamos a complexidade adicional do modelo rico porque o domínio de Orders tem regras de negócio reais (state machine, cálculos, invariantes). Para CRUDs simples (ex: cadastro de tags), anêmico seria suficiente. |
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 5 — "Um Modelo Expresso em Software":**
> *"Quando um processo ou transformação significativa no domínio não é responsabilidade natural de uma Entidade ou Objeto de Valor, adicione uma operação ao modelo como uma interface independente declarada como Serviço. Mas primeiro, certifique-se de que o comportamento não pertence naturalmente a um objeto de modelo existente."*
>
> **Leitura do arquiteto:** Evans diz: primeiro tente colocar o comportamento na Entity. Só crie um Service se o comportamento **não pertence** a nenhum objeto. O modelo anêmico inverte isso — coloca *tudo* em Services. O modelo rico segue a recomendação original do Evans.

### 2.2 Aggregate Boundaries

> 🧠 **Analogia — A Caixa-Forte do Banco:** Um Aggregate é como uma **caixa-forte**: tudo lá dentro (notas, joias, documentos) é gerenciado como uma unidade. Você não tira uma nota sem passar pela porta da caixa-forte (o Aggregate Root). E você não mistura o conteúdo de duas caixas-fortes — cada uma tem seu próprio inventário, seu próprio cadeado, suas próprias regras.

**Regra fundamental:** Um Aggregate é o menor grupo de objetos que deve ser consistente **juntos, na mesma transação**.

No nosso caso:
- **Order** é o Aggregate Root
- **OrderItem** é uma Entity dentro do aggregate
- **Money, Address, OrderNumber** são Value Objects

**Por que OrderItem está dentro do aggregate de Order?**
Porque um OrderItem não faz sentido sozinho. Ele sempre pertence a uma Order. Se eu adicionar ou remover um item, o total da Order precisa ser recalculado — **isso é consistência transacional**.

**Por que Product NÃO está no aggregate de Order?**
Porque Product pertence ao Catalog (outro bounded context). No Orders, mantemos apenas referências (ProductId, ProductName, UnitPrice no momento da compra).

> 📐 **ADR-002: Aggregate Boundary — Order como raiz, OrderItem como entity interna**
>
> | Campo | Detalhe |
> |-------|---------|
> | **Contexto** | Precisamos definir quais objetos fazem parte do aggregate de Order e quais são aggregates separados. |
> | **Decisão** | `Order` é Aggregate Root. `OrderItem` é entity interna. `Product` é referenciado apenas por ID (outro bounded context). |
> | **Alternativas descartadas** | (1) **OrderItem como aggregate separado**: permite manipulação independente, mas quebra a invariante "total = soma dos itens" — precisaríamos de eventual consistency para algo que deveria ser imediato. (2) **Product dentro do aggregate de Order**: acoplamento cross-context, locking desnecessário no catálogo quando pedido muda. |
> | **Consequências positivas** | Transação atômica garante consistência total↔itens. Aggregate compacto (baixo lock contention). |
> | **Consequências negativas** | Se o nome do produto mudar no Catalog, a cópia em OrderItem fica "stale" — aceitável, pois registra o valor no momento da compra. |
> | **Regra de ouro** | *"Prefira aggregates menores"* — Vaughn Vernon, Implementando DDD, Cap. 10. |
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 6 — "Aggregates":**
> *"Agrupe as Entidades e Objetos de Valor em Aggregates e defina limites ao redor de cada um. Escolha uma Entidade para ser a raiz de cada Aggregate e controle todo o acesso aos objetos dentro do limite através da raiz."*
>
> 📖 **Vaughn Vernon — "Implementando DDD" (2013, Alta Books), Cap. 10 — "Aggregates":**
> *"Projete Aggregates pequenos. [...] Um Aggregate deve se alinhar com invariantes verdadeiras — um limite de consistência que deve ser transacionalmente consistente."*
>
> **Exercício de raciocínio arquitetural:** Se amanhã o negócio exigir que "ao adicionar item, o estoque deve ser decrementado imediatamente", como você resolveria? (Dica: não coloque Inventory dentro de Order. Use Domain Event para eventual consistency. Se o negócio insistir em atomicidade, discuta o trade-off de performance.)

### 2.3 Domain Events vs Integration Events

> 🧠 **Analogia — Grito na Sala vs Carta pelo Correio:** Um **Domain Event** é como gritar "o pedido foi criado!" dentro da mesma sala (mesmo processo, mesmo banco, mesma transação). Todos que estão ali ouvem instantaneamente. Um **Integration Event** é como enviar uma carta pelo correio para outro escritório (outro serviço, outro banco) — pode demorar, pode se perder, e o destinatário precisa confirmar recebimento. Domain Events são rápidos e confiáveis. Integration Events exigem **resiliência**: retry, idempotência, dead-letter queues.

| Domain Events | Integration Events |
|---------------|-------------------|
| Dentro do bounded context | Entre bounded contexts / serviços |
| In-process (MediatR) | Via message broker (MassTransit/RabbitMQ) |
| Síncrono na mesma transação | Assíncrono, eventualmente consistente |
| `OrderCreatedDomainEvent` | `OrderCreatedIntegrationEvent` |

**Nesta fase:** Criamos os Domain Events. Na Fase 5, transformamos os relevantes em Integration Events.

> 📐 **ADR-003: Domain Events in-process via coleção na Entity**
>
> | Campo | Detalhe |
> |-------|---------|
> | **Contexto** | Precisamos notificar side effects (atualizar cache, enviar email) quando algo acontece no domínio (pedido criado, confirmado, cancelado). |
> | **Decisão** | Domain Events são armazenados como lista na base `Entity` e publicados via MediatR no momento do `SaveChanges`. Não usam message broker. |
> | **Alternativas descartadas** | (1) **Publicar direto no handler do command**: acopla o aggregate ao publisher. (2) **Publicar via message broker (RabbitMQ)**: overhead para side effects in-process, latência desnecessária. (3) **Static event bus**: global state, difícil de testar. |
> | **Consequências positivas** | Domínio puro (sem dependência de infra), testável, domain events visíveis nos testes via `order.DomainEvents`. |
> | **Consequências negativas** | Side effects cross-service precisarão de Integration Events (Fase 5). Domain Events não sobrevivem a crash antes do publish. |
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 8 — sobre Domain Events (conceito expandido na comunidade):**
> Evans não formalizou Domain Events no livro original (2003), mas o conceito se tornou canônico no DDD. O crédito pelo padrão formal vai para **Udi Dahan** e foi consolidado por **Martin Fowler** no artigo *"Domain Event"* (2005):
> *"Um Domain Event captura a memória de algo interessante que afeta o domínio."*
>
> **Ponto de decisão para o arquiteto:** Domain Events são **notificações**, não **comandos**. `OrderCreatedDomainEvent` diz "isso aconteceu" — nunca "faça isso". Se um handler falha, o evento já ocorreu no domínio. Essa distinção é fundamental para não criar acoplamento temporal.

### 2.4 Value Object Equality

> 🧠 **Analogia — A Nota de R$10:** Você pega duas notas de R$10 do bolso. Elas são *a mesma nota*? Não — cada uma tem um número de série diferente. Mas elas *valem a mesma coisa*? Sim! Para o seu pagamento, tanto faz qual nota você usa — elas são **intercambiáveis pelo valor**. Isso é um Value Object: **não tem identidade própria, só importa o que ele carrega**. Dois `Money(100, "BRL")` são iguais porque representam o mesmo valor. Já uma `Entity` como Order tem identidade — dois pedidos de R$100 são pedidos *diferentes*.

Value Objects são comparados **por valor**, não por **referência**. Dois `Money.FromDecimal(100m, "BRL")` são iguais.

```csharp
var price1 = Money.FromDecimal(100m, "BRL");
var price2 = Money.FromDecimal(100m, "BRL");
price1 == price2; // true — mesmos valores
price1.Equals(price2); // true
```

> 🔍 **Nota de Engenharia — Equality em C#:**
> - Por padrão, C# compara objetos por **referência** (`ReferenceEquals`). Duas instâncias `new Money(100, "BRL")` são objetos **diferentes** na heap, então `==` retornaria `false` sem override.
> - O `ValueObject` base class sobrescreve `Equals()`, `GetHashCode()` e os operadores `==`/`!=` para comparar por **componentes de valor** (via `GetEqualityComponents()`).
> - **Por que `GetHashCode()` importa?** Sem ele, Value Objects não funcionam corretamente como chaves de `Dictionary<TKey, TValue>` ou em `HashSet<T>`. O contrato do .NET diz: se `a.Equals(b)` é `true`, então `a.GetHashCode() == b.GetHashCode()` deve ser `true`. Violar isso causa bugs sutis em coleções hash — itens "desaparecem" do dicionário.

Entities são comparadas por **identidade** (Id). Dois orders com dados idênticos mas IDs diferentes são diferentes.

> 📐 **ADR-004: ValueObject base class sobre C# record**
>
> | Campo | Detalhe |
> |-------|---------|
> | **Contexto** | C# oferece `record` com structural equality built-in. Precisamos decidir se usamos `record` ou classe base customizada para Value Objects. |
> | **Decisão** | Usar `ValueObject` base class com `GetEqualityComponents()` para VOs complexos. `record` é aceitável para VOs simples sem lógica. |
> | **Por que não record para tudo** | (1) `record` compara todas as propriedades — sem controle fino. (2) EF Core Owned Types funciona melhor com classes convencionais. (3) Consistência: todo VO segue o mesmo padrão no projeto. |
> | **Quando record é ok** | Domain Events (são DTOs imutáveis, `sealed record` é perfeito). VOs triviais sem operações (ex: um wrapper de string). |
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 5 — "Objetos de Valor":**
> *"Quando você se importa apenas com os atributos de um elemento do modelo, classifique-o como um Objeto de Valor. Faça-o expressar o significado dos atributos que ele carrega e dê a ele funcionalidade relacionada. Trate o Objeto de Valor como imutável."*
>
> **Raciocínio de estrutura de dados:** A imutabilidade de Value Objects não é capricho — é uma garantia de **thread-safety** grátis. Se um `Money` nunca muda, múltiplas threads podem lê-lo sem lock. Se fosse mutável, você precisaria de `lock`, `Interlocked`, ou `ConcurrentDictionary` para evitar race conditions.

---

## 3. Conceitos de DDD

> 🤔 **Pense antes de ler:**
> 1. O que impede alguém de criar uma Order com TotalAmount = -500? Quem deveria impedir — o banco de dados, o controller, ou o próprio objeto Order?
> 2. Se `Money` é imutável, como você "muda" o preço de um item? (Dica: você não muda — cria um novo.)
> 3. Por que `order.Cancel(reason)` é melhor que `order.Status = "Cancelled"`?
>
> Se você respondeu "o próprio objeto Order" para a pergunta 1, você já pensa em termos de DDD. O domínio rico coloca as regras **dentro** dos objetos, não espalhadas em services e controllers.

> 💡 **Antes de mergulhar no código:** DDD não é sobre patterns — é sobre **falar a língua do negócio** no código. Se o Product Owner diz "o cliente *cancela* o pedido", seu código deve ter `order.Cancel(reason)`, não `orderService.UpdateStatus(order, "cancelled")`. Os patterns abaixo (Aggregate, Value Object, Domain Event) são **ferramentas** para construir esse código expressivo.

> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 2 — "Comunicação e o Uso da Linguagem":**
> *"Use o modelo como a espinha dorsal de uma linguagem. Comprometa a equipe a exercitar essa linguagem incansavelmente em toda comunicação dentro do time e no código. Use a mesma linguagem em diagramas, escrita e especialmente na fala."*
>
> Isso é a **Ubiquitous Language**: se o time de negócio fala "confirmar pedido", o código deve ter `order.Confirm()`, não `orderProcessor.ProcessConfirmation(orderId)`. Se o Product Owner não consegue ler seu código de domínio e entender o fluxo de negócio, a linguagem ubíqua falhou.

> 📖 **Martin Fowler — "Padrões de Arquitetura de Aplicações Corporativas" (2002, Bookman):**
> Fowler definiu o **Anemic Domain Model** como anti-pattern em seu catálogo: *"O horror fundamental deste anti-pattern é que ele é tão contrário à ideia básica do design orientado a objetos, que é combinar dados e processo juntos."*

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

> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 6:**
> *"Invariantes, que são regras de consistência que devem ser mantidas sempre que dados mudam, envolvem relacionamentos entre membros do Aggregate. Não se espera que qualquer regra que atravesse Aggregates esteja atualizada o tempo todo. Através de processamento de eventos, processamento em lote ou outros mecanismos de atualização, outras dependências podem ser resolvidas dentro de algum tempo especificado."*
>
> **Tradução para o nosso código:** "Total = soma dos itens" é invariante **dentro** do aggregate → deve ser mantida a todo momento. "Estoque disponível para o item" é regra que **cruza** aggregates (Order + Inventory) → aceita eventual consistency.

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

> 🔍 **Nota de Engenharia — Por que State Machine e não Enum com if/else:**
> Uma state machine codificada em um Value Object centraliza **todas** as regras de transição. Isso é o padrão **State** do GoF (Gang of Four) simplificado. A alternativa — `if/else` espalhados — é frágil: se amanhã você adicionar o estado `Refunded`, precisaria caçar todos os `if` no código. Com a state machine, adiciona o estado no dicionário de transições e pronto.
>
> A estrutura de dados `Dictionary<string, HashSet<string>>` funciona como um **grafo de adjacência**: cada estado é um nó, e as transições válidas são as arestas. `CanTransitionTo()` faz uma consulta O(1) no `Dictionary` + O(1) no `HashSet`. É a mesma estrutura que motores de workflow usam.

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

> 🔍 **Nota de Engenharia — Por que projetos separados:**
> - **Domain sem referência a infra**: O `.csproj` do Domain referencia apenas SharedKernel. Zero NuGet de infra (EF Core, HTTP, etc.). Isso é a **Dependency Rule** da Arquitetura Limpa (Robert C. Martin, 2017): *"Dependências de código-fonte devem apontar apenas para dentro, em direção a políticas de nível mais alto."*
> - **Tests separado do Domain**: Permite rodar testes sem deployar a aplicação. O projeto de testes referencia o Domain, não o contrário — o Domain não sabe que testes existem.
> - **FluentAssertions**: DSL para assertions legíveis. `order.Status.Should().Be(OrderStatus.Confirmed)` lê como inglês. Isso importa porque testes **são documentação executável**.

### 4.2 Estrutura de Pastas

```bash
# Domain
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/Aggregates/OrderAggregate
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/ValueObjects
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/Events
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/Exceptions
mkdir -p src/Services/Orders/OrderFlow.Orders.Domain/Interfaces
```

> 🔍 **Nota de Engenharia — Convenção de pastas:**
> - **`Aggregates/OrderAggregate/`**: Agrupa o aggregate root + entities internas + value objects de suporte. Isso reflete a boundary: tudo nesta pasta pertence ao mesmo aggregate.
> - **`ValueObjects/`**: VOs compartilhados (usados por múltiplos aggregates neste bounded context).
> - **`Events/`**: Domain Events — DTOs imutáveis que registram o que aconteceu.
> - **`Interfaces/`**: Contratos do repositório. O **Domain** define a interface; a **Infrastructure** implementa. Isso é **Dependency Inversion Principle** (DIP): o módulo de alto nível (Domain) não depende do módulo de baixo nível (Infrastructure), ambos dependem da abstração (interface).

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

> 🔍 **Nota de Engenharia — Design da hierarquia:**
> ```
> Entity (Id, DomainEvents, AddDomainEvent, ClearDomainEvents)
>   └── AuditableEntity (CreatedAt, UpdatedAt, SetUpdated)
>         └── AggregateRoot (marcador semântico)
> ```
> - **Por que `AggregateRoot` é uma classe vazia?** Serve como **marker class** (similar a marker interface). O repositório genérico `IRepository<T> where T : AggregateRoot` usa isso como constraint — impede que alguém crie um repositório para `OrderItem` (que não é root).
> - **Herança vs Composição aqui**: Normalmente preferimos composição, mas para base classes de domínio, herança é idiomática em DDD. Evans e Vernon usam este padrão. A cadeia é curta (3 níveis) e cada nível adiciona responsabilidade concreta.
> - **`abstract`**: Não pode ser instanciada diretamente — força a criação de classes concretas (`Order`, `Product`). Isso é **Template Method** do GoF na forma mais simples.

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

> 🔍 **Nota de Engenharia — Dissecando a ValueObject base:**
>
> **`GetEqualityComponents()` — Template Method Pattern:**
> Cada subclasse decide *quais* campos participam da igualdade via `yield return`. O método base `Equals()` usa esses componentes sem conhecer os detalhes — é o **Template Method** do GoF: "define o esqueleto do algoritmo, delegando passos para subclasses".
>
> **`SequenceEqual()` — Comparação elemento a elemento:**
> Compara dois `IEnumerable` na ordem. Se `Money` retorna `[100m, "BRL"]` e outro retorna `[100m, "BRL"]`, são iguais. Se retorna `[100m, "USD"]`, são diferentes. Complexidade: O(n) onde n = número de componentes.
>
> **`GetHashCode()` com `Aggregate` + `HashCode.Combine`:**
> - `Aggregate` é um fold (reduce) — percorre os componentes acumulando um hash.
> - `HashCode.Combine` é API do .NET que faz hash mixing — distribui os bits para reduzir colisões em hash tables.
> - **Por que `?? 0`?** Componentes nullable (`Complement` de Address) podem ser `null`. `null.GetHashCode()` lançaria `NullReferenceException`. O `?? 0` trata esse caso.
>
> **Operadores `==` e `!=`:**
> Sem esses overrides, `money1 == money2` faria comparação de referência (endereço na heap). Com os overrides, delega para `Equals()` que usa os componentes. O `null` check é necessário porque `null == null` deve ser `true`, mas chamar `.Equals()` em `null` lançaria exception.
>
> **`IEquatable<ValueObject>`:**
> Interface que sinaliza ao compilador e a coleções genéricas que este tipo sabe se comparar. `List<T>.Contains()` e `Dictionary<TKey, TValue>` usam `IEquatable<T>` quando disponível, evitando boxing de value types.

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

> 🔍 **Nota de Engenharia — Exception Design:**
> - **`sealed class`**: Não pode ser herdada. Exceções de domínio devem ser específicas — se precisar de outra exceção, crie uma nova classe. `sealed` também permite otimizações do JIT (devirtualização).
> - **`Code` property**: Machine-readable. A `Message` é para humanos; o `Code` (`"INVALID_STATUS_TRANSITION"`, `"EMPTY_ORDER"`) é para código — ideal para o frontend mostrar mensagens localizadas ou para monitoramento agrupar erros.
> - **Dois construtores**: O segundo com `innerException` preserva o stack trace original em cenários de wrap (captura exceção X, lança Y com contexto de domínio).
> - **Por que `Exception` e não uma base custom?** Simplicidade. Em domínios maiores, você pode criar `DomainException` base com factory methods para diferentes contextos. Aqui, `OrderDomainException` é suficiente.
>
> 📖 **Robert C. Martin — "Código Limpo" (2008, Alta Books), Cap. 7 — "Tratamento de Erros":**
> *"Crie mensagens de erro informativas e passe-as junto com suas exceções. Mencione a operação que falhou e o tipo de falha."*
>
> Note como cada throw inclui mensagem descritiva + código: `throw new OrderDomainException("Cannot confirm an order with no items.", "EMPTY_ORDER")`. Na Fase 03, o middleware converte isso em HTTP 400 com corpo estruturado.

**`src/Services/Orders/OrderFlow.Orders.Domain/Exceptions/InsufficientStockException.cs`**

```csharp
namespace OrderFlow.Orders.Domain.Exceptions;

public sealed class InsufficientStockException : Exception
{
    public static string Code => "INSUFFICIENT_STOCK";
    public Guid ProductId { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(Guid productId, int requested, int available)
        : base($"Insufficient stock for product '{productId}'. Requested: {requested}, Available: {available}.")
    {
        ProductId = productId;
        Requested = requested;
        Available = available;
    }
}
```

> 🔍 **Nota de Engenharia — Por que herda de `Exception` (e não de `OrderDomainException`)?**
> `OrderDomainException` é `sealed` (não pode ser herdada — decisão de Exception Design). Então criamos `InsufficientStockException` derivando diretamente de `Exception`. O `Code` é `static` (não acessa estado da instância — `CA1822` do analyzer obriga). A exceção carrega contexto rico (`ProductId`, `Requested`, `Available`) para o middleware da Fase 3 transformar em ProblemDetails estruturado.

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

> 🔍 **Nota de Engenharia — Money como VO rico:**
>
> **Construtor privado + Factory Method (`FromDecimal`)**:
> O padrão **Static Factory Method** (Joshua Bloch, "Java Efetivo", Alta Books — aplica-se igualmente a C#) tem vantagens sobre construtor público: (1) nomes descritivos (`FromDecimal`, `Zero`), (2) pode retornar instância cacheada, (3) validação antes da criação — se falhar, o objeto nunca existe em estado inválido.
>
> **`Math.Round(amount, 2)` — Arredondamento Banker's:**
> `decimal` em C# usa 128 bits com aritmética de ponto fixo — ideal para dinheiro (ao contrário de `double` que tem erros de precisão: `0.1 + 0.2 != 0.3`). O `Round` com 2 decimais normaliza centavos. O comportamento padrão é `MidpointRounding.ToEven` (Banker's rounding) — arredonda 0.5 para o par mais próximo, reduzindo viés estatístico em somas grandes.
>
> **`currency.ToUpperInvariant()` — Normalização:**
> Garante que `"brl"`, `"Brl"`, `"BRL"` viram todos `"BRL"`. Sem isso, `Money("brl") != Money("BRL")` — bug sutil. `Invariant` ignora cultura do SO (evita surpresas em deploys em servidores com locale diferente).
>
> **Operações retornam novo `Money` (imutabilidade)**:
> `Add` não modifica `this` — cria e retorna uma nova instância. Isso é fundamental: se dois threads compartilham a mesma referência de `Money`, nenhum pode corromper o outro. É o mesmo princípio de `string` em C# — toda operação retorna nova string.
>
> **`EnsureSameCurrency` — Guard privado:**
> Impede somar BRL com USD. Em domínios financeiros reais, você teria um `ExchangeRateService` para conversão. Aqui, o VO **rejeita** a operação inválida em vez de tentar converter — fail-fast.
>
> **Estrutura de dados implícita — `GetEqualityComponents`:**
> O `yield return` cria um iterator (state machine gerada pelo compilador). Para `Money`, são 2 componentes: `Amount` e `Currency`. Dois `Money(100, "BRL")` produzem sequências idênticas → são iguais.
>
> 📖 **Martin Fowler — "Padrões de Arquitetura de Aplicações Corporativas" (2002, Bookman), pattern "Money":**
> *"Uma grande proporção dos computadores neste mundo manipula dinheiro, então sempre me intrigou que dinheiro não seja realmente um tipo de dado de primeira classe em nenhuma linguagem de programação mainstream. A falta de um tipo causa problemas, sendo os mais óbvios relacionados a moedas."*
>
> Fowler propôs o padrão `Money` exatamente como implementamos aqui: valor + moeda, imutável, com operações aritméticas que validam moeda.

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

> 🔍 **Nota de Engenharia — Address como VO:**
>
> **8 propriedades read-only**:
> Todas sem setter — definidas no construtor e imutáveis para sempre. `Complement` é nullable (`string?`) porque complemento é opcional. As demais são non-nullable — o factory method garante isso com `ThrowIfNullOrWhiteSpace`.
>
> **`ArgumentException.ThrowIfNullOrWhiteSpace` (.NET 7+):**
> Substituição moderna de `if (string.IsNullOrWhiteSpace(x)) throw new ArgumentException(...)`. Mais conciso, lança exceção com `paramName` automaticamente. É API do .NET — não precisa de NuGet.
>
> **Normalização no factory method:**
> - `.Trim()` remove espaços nas bordas
> - `.Replace("-", "")` no CEP normaliza `"01234-567"` para `"01234567"`
> Isso garante que `Address.Create("Rua A", "1", ..., "01234-567") == Address.Create("Rua A", "1", ..., "01234567")` — sem a normalização, seriam desiguais.
>
> **`GetEqualityComponents` com 8 campos:**
> Todos participam da igualdade — dois endereços são iguais se e somente se **todos** os campos são iguais. `Complement` nullable é tratado pelo `?? 0` no `GetHashCode` da base class.
>
> **`ToString()` com interpolação condicional:**
> O ternário `(Complement is not null ? $" - {Complement}" : "")` omite complemento quando é null. Isso é **pattern matching** do C# (`is not null` em vez de `!= null`) — mais expressivo e null-safe.
>
> 📖 **Evans — DDD (2003, Alta Books), sobre Value Objects como Domain Primitives:**
> *"Quando uma decisão de design é baseada em um tipo primitivo, como um int ou uma string, a porta está aberta para todo tipo de erro. [...] Substitua primitivos por Objetos de Valor para tornar conceitos implícitos explícitos."*
>
> `Address` transforma 8 strings soltas em um **conceito de domínio**. Em vez de `string street, string number, string city...` espalhados por parâmetros, você passa `Address` — tipo-safe, validado, com comportamento.

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
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
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

> 🔍 **Nota de Engenharia — OrderNumber:**
>
> **Dois factory methods com propósitos diferentes:**
> - `Create()`: Gera novo número para pedidos recém-criados. Chamado em `Order.Create()`.
> - `FromExisting(string)`: Reconstrói a partir de string do banco. Chamado pelo ORM ao hidratar a entidade. Não gera novo número — apenas reconstrói o VO.
>
> **Anatomia do número `ORD-20260415-A3F8B`:**
> - `ORD-`: Prefixo legível (humanos vêem e sabem que é pedido).
> - `20260415`: Data UTC — útil para debugging e ordenação visual.
> - `A3F8B`: 5 chars hexadecimais do Guid — 16^5 = 1.048.576 combinações/dia.
>
> **`Guid.NewGuid().ToString("N")[..5]`:**
> - `ToString("N")` formata Guid sem hifens: `"a3f8b7c2d4e6..."` (32 chars hex).
> - `[..5]` é range syntax (C# 8+) — pega os primeiros 5 caracteres.
> - `ToUpperInvariant()` normaliza para maiúsculas.
>
> **Limitação conhecida (produção):**
> 5 chars hex = ~1M combinações. Em alto volume (>100K pedidos/dia), risco de colisão. Em produção real, use sequência do banco (SQL Server `SEQUENCE`) ou algoritmo como Snowflake ID. A implementação didática é simplificada intencionalmente.
>
> **Por que VO e não `string`?**
> Se fosse `public string OrderNumber { get; set; }`, qualquer código poderia atribuir `"BANANA"`. Com o VO, o único jeito de criar um `OrderNumber` é via `Create()` (formato garantido) ou `FromExisting()` (validação de não-vazio).

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

> 🔍 **Nota de Engenharia — State Machine como grafo de adjacência:**
>
> **`static readonly` instances — Padrão "Smart Enum":**
> Instâncias estáticas pré-criadas (`Pending`, `Confirmed`, etc.) simulam um enum mas com **comportamento**. `OrderStatus.Pending` é a única instância de status "Pending" — reutilizada por toda a aplicação. O construtor `private` impede criação de instâncias desconhecidas.
>
> **`Dictionary<string, HashSet<string>> ValidTransitions` — Grafo dirigido:**
> ```
> Estrutura de dados subjacente:
>
>   Key (nó)      → Values (arestas)
>   "Pending"     → {"Confirmed", "Cancelled"}
>   "Confirmed"   → {"Shipped", "Cancelled"}
>   "Shipped"     → {"Delivered"}
>   "Delivered"   → {}  (nó terminal)
>   "Cancelled"   → {}  (nó terminal)
> ```
> Isso é literalmente um **grafo de adjacência** — a mesma estrutura usada em algoritmos como BFS/DFS. Cada chave é um nó e o `HashSet` de valores são os nós adjacentes (transições válidas).
>
> **Complexidade de `CanTransitionTo`:**
> - `Dictionary.TryGetValue`: O(1) amortizado (hash table lookup).
> - `HashSet.Contains`: O(1) amortizado (hash lookup).
> - **Total: O(1)** — independente do número de estados.
>
> Alternativa com `if/else` seria O(n) no número de regras e difícil de manter. A estrutura de dados **é** a documentação das regras.
>
> **`TransitionTo` retorna `newStatus` (não modifica `this`):**
> Mantém a imutabilidade do VO. No aggregate, o uso é: `Status = Status.TransitionTo(OrderStatus.Confirmed)` — substituição, não mutação. Isso garante thread-safety e rastreabilidade.
>
> **`FromString` com switch expression:**
> Mapeamento stringly-typed → strongly-typed. Necessário para deserialização (banco de dados, JSON). O `_` default lança exceção — não aceita strings desconhecidas, fail-fast.
>
> **`IsFinal` — Propriedade computada:**
> `this == Delivered || this == Cancelled` usa o `==` do ValueObject (compara por `Value`). Estados finais são terminais no grafo — sem arestas de saída.
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 5 — sobre VO com comportamento:**
> *"Objetos de Valor podem ter métodos que encapsulam lógica de domínio. [...] Operações de um Objeto de Valor devem ser Funções Livres de Efeitos Colaterais — elas retornam resultados sem modificar o estado observável do sistema."*
>
> `TransitionTo` é exatamente isso: retorna um novo `OrderStatus` sem modificar o atual. Efeito colateral zero.
>
> 📖 **Gamma, Helm, Johnson, Vlissides — "Padrões de Projeto" (GoF, 1994, Bookman), State Pattern:**
> A implementação aqui é uma versão simplificada do State Pattern. Em vez de classes separadas para cada estado, usamos instâncias únicas + tabela de transições. Para máquinas mais complexas (com ações de entrada/saída por estado), a versão com classes separadas seria mais apropriada.

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

> 🔍 **Nota de Engenharia — Domain Events como `sealed record`:**
>
> **Por que `sealed record` e não `class`?**
> - `record` em C# gera automaticamente: `Equals()`, `GetHashCode()`, `ToString()`, desconstrutor, e `with` expression. Para DTOs imutáveis, é perfeito — zero boilerplate.
> - `sealed` impede herança. Eventos são contratos finais — se precisar de variação, crie um novo tipo.
> - **Positional syntax** (`OrderCreatedDomainEvent(Guid OrderId, ...)`) gera construtor primário + propriedades `init` automáticas.
>
> **Dois construtores — Primary + Convenience:**
> ```csharp
> // Primary (gerado pelo record) — completo, com OccurredOn explícito
> OrderCreatedDomainEvent(Guid orderId, string orderNumber, Guid customerId, decimal totalAmount, DateTime occurredOn)
>
> // Convenience — omite OccurredOn, usa DateTime.UtcNow
> OrderCreatedDomainEvent(Guid orderId, string orderNumber, Guid customerId, decimal totalAmount)
> ```
> O construtor de conveniência delega para o primário via `: this(...)`. O domínio chama o de conveniência (simples); testes podem usar o primário para controlar o timestamp.
>
> **`OccurredOn` — Audit trail:**
> Registra **quando** o evento ocorreu no domínio (não quando foi publicado ou processado). Essencial para event sourcing, debugging, e compliance.
>
> **Dados no evento — "foto do momento":**
> `OrderCreatedDomainEvent` carrega `TotalAmount` como snapshot. Mesmo que o total mude depois (adição de itens), o evento registra o valor no momento da criação. Eventos são **imutáveis** — nunca altere um evento depois de criado.
>
> 📖 **Greg Young (2010) — sobre Event Design:**
> *"Um evento é um fato. Algo que aconteceu. Você não pode mudar fatos. Se algo aconteceu de forma diferente, isso é um novo evento."*
>
> 📐 **Decisão arquitetural: por que `decimal TotalAmount` no evento e não `Money`?**
> Domain Events podem ser serializados (para logs, outbox, etc.). Value Objects complexos complicam a serialização. Usar primitivas (`decimal`, `string`, `Guid`) no evento garante **portabilidade** — qualquer consumer pode ler, mesmo em outra linguagem. É uma forma de **Anti-Corruption Layer** dentro do mesmo bounded context: o evento é a fronteira.

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

> 🔍 **Nota de Engenharia — Dissecando OrderItem:**
>
> **`private init` vs `private set`:**
> - `private init`: Pode ser definida apenas no construtor ou object initializer. Depois, imutável. Usado em `OrderId`, `ProductId`, `ProductName`, `UnitPrice` — coisas que não mudam.
> - `private set`: Pode ser alterada por métodos internos da classe. Usado em `Quantity` — que muda via `IncreaseQuantity`/`DecreaseQuantity`.
> - Essa distinção é intencional: comunica ao leitor o que pode e o que não pode mudar.
>
> **`Money TotalPrice => UnitPrice.Multiply(Quantity)` — Computed property:**
> Não armazena valor — calcula sob demanda. Vantagem: nunca fica dessincronizado. Desvantagem: recalcula a cada acesso. Para `Money.Multiply(int)` com aritmética decimal, o custo é desprezível (nanossegundos).
>
> **`private OrderItem() { }` — Construtor para EF Core:**
> EF Core precisa de construtor sem parâmetros para instanciar a entidade via reflection. `private` garante que ninguém fora da classe (ou via reflection do EF) pode usá-lo. Sem isso, EF lança `InvalidOperationException` ao materializar a query.
>
> **`internal` nos métodos — Access Modifier Strategy:**
> ```
> Visibilidade em C#:
>   public    → qualquer assembly
>   internal  → mesmo assembly (OrderFlow.Orders.Domain)
>   protected → classe + subclasses
>   private   → apenas a própria classe
> ```
> `internal` aqui significa: `Order` (que está no mesmo assembly) pode chamar `IncreaseQuantity()`, mas o código da Application layer (outro assembly) **não pode**. Acesso externo é **sempre** pela raiz (`Order.AddItem()`).
>
> **Guard clauses em `IncreaseQuantity` e `DecreaseQuantity`:**
> - `additionalQuantity <= 0`: Impede incremento de 0 ou negativo.
> - `Quantity - reduceBy < 1`: Impede que o item fique com quantidade 0 (para isso, use `RemoveItem`).
> Isso é **Design by Contract** — cada método declara suas pré-condições. Violação = exceção imediata.
>
> **Object initializer no `Create`:**
> ```csharp
> return new OrderItem
> {
>     Id = Guid.NewGuid(),
>     OrderId = orderId,
>     // ...
> };
> ```
> Usa `private init` properties — funciona no object initializer mas não em atribuição posterior. É mais legível que um construtor com 5 parâmetros posicionais.
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 5 — "Entidades":**
> *"Quando um objeto é distinguido por sua identidade, em vez de seus atributos, torne isso primário em sua definição no modelo. Mantenha a definição da classe simples e focada na continuidade do ciclo de vida e na identidade."*
>
> `OrderItem` tem identidade (`Id`) porque precisamos rastrear "qual item teve a quantidade alterada". Dois itens com mesmo ProductId e Quantity são itens **diferentes** se têm IDs diferentes.

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

> 🔍 **Nota de Engenharia — O Aggregate Root completo, linha por linha:**
>
> **Propriedades — Encapsulamento deliberado:**
> | Propriedade | Setter | Por quê |
> |-------------|--------|---------|
> | `OrderNumber` | `private init` | Definido na criação, nunca muda |
> | `CustomerId` | `private init` | Pedido não troca de dono |
> | `ShippingAddress` | `private set` | Pode mudar via `UpdateShippingAddress()` (apenas enquanto pending) |
> | `Status` | `private set` | Muda via métodos de transição (`Confirm`, `Ship`, etc.) |
> | `TotalAmount` | `private set` | Muda via `RecalculateTotal()` (efeito colateral de Add/Remove) |
> | `CancellationReason` | `private set` | Definido apenas em `Cancel()` |
>
> **`private readonly List<OrderItem> _items = []` + `IReadOnlyCollection` público:**
> - `_items` é o backing field — lista mutável, mas só acessível dentro da classe.
> - `Items` expõe via `AsReadOnly()` — quem recebe **não pode** `.Add()`, `.Remove()`, `.Clear()`.
> - `[]` é collection expression (C# 12) — equivalente a `new List<OrderItem>()`.
>
> **Factory Method `Create()` — O único ponto de criação:**
> ```csharp
> public static Order Create(Guid customerId, Address shippingAddress)
> ```
> - `static` — não precisa de instância para chamar. `Order.Create(...)`.
> - Cria o objeto em estado válido: status `Pending`, total zero, sem itens.
> - Emite `OrderCreatedDomainEvent` — todo pedido criado gera um evento.
> - O construtor `private Order() { }` impede `new Order()` de fora.
>
> **`AddItem()` — Comportamento rico em ação:**
> 1. **Guard clause** `EnsurePendingStatus("add items")` — fail-fast se não pendente.
> 2. **Validação de productId** — evita `Guid.Empty`.
> 3. **Busca existente** `FirstOrDefault` — O(n), aceitável para <100 itens.
> 4. **Branch upsert** — incrementa se existe, cria se não.
> 5. **`RecalculateTotal()`** — garante consistência.
> 6. **`SetUpdated()`** — atualiza `UpdatedAt` (auditoria).
> 7. **Domain Event** — notifica que item foi adicionado.
>
> Essa sequência não é acidental — é um **protocolo de mutação**: validate → mutate → recalculate → audit → notify.
>
> **`RemoveItem()` com null-coalescing throw:**
> ```csharp
> var item = _items.FirstOrDefault(i => i.ProductId == productId)
>     ?? throw new OrderDomainException(...);
> ```
> O `??` (null-coalescing) com `throw` é C# 7+ — se `FirstOrDefault` retorna null, lança exceção diretamente. Evita `if (item == null) throw ...`. Elegante e conciso.
>
> **`Confirm()` — Invariante + Transição + Evento:**
> ```csharp
> if (!_items.Any())                                    // invariante: pedido precisa de itens
>     throw new OrderDomainException(...);
> Status = Status.TransitionTo(OrderStatus.Confirmed);  // transição validada pela state machine
> SetUpdated();                                          // auditoria
> AddDomainEvent(new OrderConfirmedDomainEvent(...));    // notificação
> ```
> Note que `Status.TransitionTo()` faz **sua própria validação** (pode lançar se transição inválida). O `Confirm()` adiciona validação extra (precisa ter itens). São camadas de proteção complementares.
>
> **`RecalculateTotal()` — Aggregate (fold) funcional:**
> ```csharp
> TotalAmount = _items.Aggregate(
>     Money.Zero(),                              // seed (valor inicial)
>     (total, item) => total.Add(item.TotalPrice) // acumulador
> );
> ```
> Isso é um **fold/reduce** — operação fundamental em programação funcional:
> - Começa com `Money.Zero()` (0 BRL)
> - Para cada item, soma `item.TotalPrice` ao acumulador
> - Resultado: soma de todos os itens
>
> É equivalente ao `Array.reduce()` do JavaScript ou `functools.reduce()` do Python. A complexidade é O(n) onde n = número de itens.
>
> **`EnsurePendingStatus()` — Guard reutilizável:**
> Extraído como método privado porque múltiplos behaviors precisam da mesma pré-condição. O parâmetro `string action` torna a mensagem de erro contextual: *"Cannot **add items** for an order with status 'Confirmed'"*.
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 6 — sobre Aggregate Root:**
> *"A Entidade raiz tem identidade global e é ultimamente responsável por verificar invariantes. Entidades raiz têm identidade global. Entidades dentro do limite têm identidade local, única apenas dentro do Aggregate."*
>
> No nosso código: `Order.Id` é global (referenciável de qualquer contexto). `OrderItem.Id` é local (relevante apenas dentro da Order). Ninguém faz `_repo.GetOrderItemById(itemId)` — sempre `_repo.GetByIdAsync(orderId)` e então navega pelos itens.
>
> 📖 **Vaughn Vernon — Implementando DDD (2013, Alta Books), Cap. 10 — Regras de Design de Aggregates:**
> 1. *"Proteja invariantes de negócio dentro dos limites do Aggregate"* → `RecalculateTotal()`, `EnsurePendingStatus()`
> 2. *"Projete Aggregates pequenos"* → Apenas Order + OrderItems, sem Product ou Customer
> 3. *"Referencie outros Aggregates apenas por identidade"* → `CustomerId` (Guid), não `Customer customer`
> 4. *"Atualize outros Aggregates usando consistência eventual"* → Domain Events

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

> 🔍 **Nota de Engenharia — Repository Pattern:**
>
> **Herança de `IRepository<Order>`:**
> O repositório genérico (do SharedKernel) fornece operações básicas (`GetByIdAsync`, `AddAsync`, etc.). `IOrderRepository` **estende** com queries específicas do domínio de Orders.
>
> **Constraint implícita `where T : AggregateRoot`:**
> `IRepository<T>` no SharedKernel provavelmente tem `where T : AggregateRoot`. Isso significa que **apenas** aggregate roots podem ter repositórios. `IRepository<OrderItem>` não compilaria — e isso é intencional.
>
> **`CancellationToken ct = default`:**
> Parâmetro opcional com default. Permite que chamadores passem um token de cancelamento para abortar queries longas. Em ASP.NET Core, o framework injeta o token do request — se o cliente desconectar, a query para. Sempre propague `CancellationToken` em operações async.
>
> **`IReadOnlyList<Order>` (não `List<Order>`):**
> O retorno é readonly — o chamador não pode `.Add()` na coleção retornada. Sinaliza intenção: "aqui está o resultado, não modifique-o".
>
> **Por que a interface está no Domain?**
> A interface define **o que** o domínio precisa. A implementação concreta (EF Core, Dapper, etc.) fica na Infrastructure. Isso é **Dependency Inversion Principle (DIP)**: o módulo de alto nível (Domain) define a abstração; o módulo de baixo nível (Infrastructure) implementa. Na Fase 03, criaremos `OrderRepository : IOrderRepository` na camada de Infrastructure.
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 6 — "Repositórios":**
> *"Para cada tipo de objeto que precisa de acesso global, crie um objeto que possa fornecer a ilusão de uma coleção em memória de todos os objetos daquele tipo. Configure o acesso através de uma interface global bem conhecida. Forneça métodos para adicionar e remover objetos. [...] Forneça Repositórios apenas para Raízes de Aggregate."*

---

## 6. Testes

> 🧠 **Analogia — O Crash Test do Carro:** Testes de domínio são como **crash tests** — você coloca o carro (aggregate) em situações extremas (adicionar item em pedido cancelado, criar Money com valor negativo) e verifica se as proteções funcionam. Um carro sem crash test pode parecer perfeito, mas ninguém confia nele. Domínio sem testes? Mesma coisa. A beleza dos testes de domínio é que são **os mais rápidos e baratos** de todo o sistema — sem banco, sem HTTP, sem Docker. Rodam em milissegundos.

> 📖 **Kent Beck — "TDD: Desenvolvimento Guiado por Testes" (2002, Bookman):**
> *"Escreva um teste que define uma função ou melhorias de uma função. [...] Testes são o andaime, não o edifício. Mas tente construir sem andaime."*
>
> Os testes desta fase são **puro domínio** — sem mocks, sem setup complexo, sem infraestrutura. Se seu domínio precisa de mock para testar, o domínio tem dependências externas e algo está errado arquiteturalmente.

### 6.1 Filosofia de Testes do Domínio

| Princípio | Aplicação |
|-----------|-----------|
| **Teste o comportamento, não a implementação** | Testar `order.AddItem()`, não o setter de uma propriedade |
| **Um conceito por teste** | Cada teste valida uma coisa |
| **Naming:** `Method_State_Expected` | `AddItem_WhenOrderIsPending_AddsItemAndRecalculatesTotal` |
| **Sem mocks** | Domínio puro não precisa de mocks |
| **Arrange-Act-Assert** | Estrutura clara e consistente |

> 📖 **Gerard Meszaros — "xUnit Test Patterns" (2007, sem edição em pt-br):**
> *"Testes devem ser escritos de forma que nos ajudem a entender o que o código testado faz. [...] O nome do teste deve descrever o cenário e o comportamento esperado."*
>
> A nomenclatura `Method_State_Expected` é uma das convenções mais usadas (Roy Osherove, "The Art of Unit Testing"). Outra opção popular é `Given_When_Then` (BDD style). Escolha uma e mantenha consistência no projeto.

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

> 🔍 **Nota de Engenharia — Estratégia de testes do Money:**
>
> **Cobertura por categorias:**
> | Categoria | Testes | O que valida |
> |-----------|--------|--------------|
> | Criação (happy path) | `FromDecimal_ValidAmount_CreatesMoney` | Factory funciona com input válido |
> | Criação (edge case) | `FromDecimal_NegativeAmount_ThrowsArgumentException` | Guard clause funciona |
> | Arredondamento | `FromDecimal_RoundsToTwoDecimals` | `Math.Round` com 2 casas |
> | Operação (happy) | `Add_SameCurrency`, `Multiply_ByQuantity` | Aritmética correta |
> | Operação (guard) | `Add_DifferentCurrency` | Guard de moeda funciona |
> | Igualdade | `Equality_SameValues`, `Equality_DifferentValues` | ValueObject equality |
> | Factory auxiliar | `Zero_ReturnsZeroAmount` | Money.Zero() funciona |
>
> **Padrão Arrange-Act-Assert visível:**
> ```csharp
> // Arrange — prepara o cenário
> var a = Money.FromDecimal(100m);
> var b = Money.FromDecimal(50m);
>
> // Act — executa a ação sendo testada
> var result = a.Add(b);
>
> // Assert — verifica o resultado
> result.Amount.Should().Be(150m);
> ```
> Linhas em branco separam as 3 fases — convenção visual que facilita leitura rápida.
>
> **`var act = () => ...` + `act.Should().Throw<T>()`:**
> O `act` é um `Action` (delegate) que **não é executado** imediatamente. O `Should().Throw<T>()` do FluentAssertions **executa** o delegate e captura a exceção. Se não lançar a exceção esperada, o teste falha. Isso é mais expressivo que `Assert.Throws<T>()` do xUnit.
>
> **Por que testar igualdade explicitamente?**
> Porque `ValueObject.Equals()` e `operator ==` são código nosso — precisam de teste. Se amanhã alguém alterar `GetEqualityComponents()` e esquecer um campo, o teste de igualdade pega.

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

> 🔍 **Nota de Engenharia — `[Theory]` + `[InlineData]` para State Machine:**
>
> **`[Theory]` vs `[Fact]`:**
> - `[Fact]` = um cenário fixo (sem parâmetros).
> - `[Theory]` = múltiplos cenários parametrizados. Cada `[InlineData]` gera um teste separado no test runner.
>
> **10 combinações em um único método:**
> Em vez de escrever 10 métodos separados para cada transição, `[Theory]` com `[InlineData]` cobre todas as transições da state machine declarativamente. O xUnit executa 10 testes independentes — se um falha, os outros continuam.
>
> **Cobertura da state machine:**
> Os 10 `InlineData` cobrem:
> - 5 transições **válidas** (expected = true): Pending→Confirmed, Pending→Cancelled, etc.
> - 5 transições **inválidas** (expected = false): Pending→Shipped, Shipped→Cancelled, etc.
>
> **Idealmente**: cubra TODAS as combinações possíveis (5 estados x 5 estados = 25). Os 10 aqui cobrem as mais importantes. Para cobertura exaustiva, adicione as 15 restantes.
>
> **`WithMessage("*Invalid status transition*")`:**
> O `*` é wildcard — verifica que a mensagem **contém** "Invalid status transition", sem exigir match exato. Útil porque a mensagem inclui valores dinâmicos (from/to) que variam por caso.

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

> 🔍 **Nota de Engenharia — Dissecando os testes do Aggregate:**
>
> **Helpers `CreateTestAddress()` e `CreatePendingOrder()`:**
> - Métodos `static` para reduzir boilerplate.
> - `CreatePendingOrder()` chama `ClearDomainEvents()` — importante! Sem isso, o evento `OrderCreated` da fábrica "contamina" os testes que verificam eventos específicos de outras ações.
> - Esse padrão é chamado **Test Data Builder** (simplificado). Em domínios maiores, use a variante completa com fluent API: `new OrderBuilder().WithCustomer(id).WithItem(...).Build()`.
>
> **Teste de ciclo de vida completo `FullLifecycle_PendingToDelivered_Works`:**
> Esse teste é um **integration test de domínio** — testa o fluxo completo (Pending → Confirmed → Shipped → Delivered) em sequência. É útil como smoke test para garantir que a state machine funciona ponta a ponta. Os outros testes são unitários (testam uma ação isolada).
>
> **Testes de Domain Events (ex: `AddItem_RaisesOrderItemAddedDomainEvent`):**
> ```csharp
> order.DomainEvents.Should().ContainSingle()           // exatamente 1 evento
>     .Which.Should().BeOfType<OrderItemAddedDomainEvent>(); // do tipo correto
> ```
> - `ContainSingle()` é assertion forte — se houver 0 ou 2+ eventos, falha.
> - `.Which` extrai o elemento para assertion encadeada.
> - Isso valida que o aggregate **emite** os eventos corretamente. Quem **consome** os eventos será testado separadamente (na camada Application).
>
> **Testes negativos (guards):**
> Cada comportamento tem pelo menos um teste de "caminho triste" — o que acontece quando a pré-condição é violada. `AddItem_WhenConfirmed_Throws`, `Confirm_WithoutItems_Throws`, `Cancel_DeliveredOrder_Throws`. Esses testes são **tão importantes** quanto os happy paths — garantem que as invariantes realmente protegem o aggregate.
>
> 📖 **Michael Feathers — "Trabalhando Efetivamente com Código Legado" (2004, Bookman):**
> *"Código sem testes é código ruim. Não importa quão bem escrito ele seja; não importa quão bonito ou orientado a objetos ou bem encapsulado ele esteja. Com testes, podemos mudar o comportamento do nosso código rápida e verificavelmente. Sem eles, realmente não sabemos se nosso código está melhorando ou piorando."*

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

> 🔍 **Nota de Engenharia — Testes de Address:**
>
> **Teste de normalização (implícito):**
> `address.ZipCode.Should().Be("01234567")` — o input foi `"01234-567"` com hífen, e o resultado esperado é sem. Isso verifica que o factory method normaliza o CEP. Se a normalização quebrar, esse teste pega.
>
> **Testes de igualdade — por que dois endereços iguais importam?**
> Na prática, igualdade de VOs é usada em comparações de domínio: "o endereço de entrega mudou?" → `order.ShippingAddress != newAddress`. Se a equality estiver bugada, o domínio pode considerar endereços iguais como diferentes (ou vice-versa), gerando atualizações desnecessárias ou perdendo atualizações reais.

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

> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 6 — sobre limites de Aggregates:**
> *"Tentar manter uma regra de consistência que atravessa Aggregates não é facilmente implementável. [...] Se uma regra é complexa demais para aplicar dentro de um Aggregate, isso sugere que o limite foi traçado no lugar errado."*
>
> Se você precisa de uma regra atômica entre dois aggregates, reavalie se são realmente dois aggregates ou se deveriam ser um só. Essa é a tensão central do aggregate design.

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
| `AuditableEntity` | `Order` herda via `AggregateRoot` (tem `CreatedAt`, `UpdatedAt`) |
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
| Aggregate Root | `Order.cs` (com AddItem, Confirm, Cancel, Ship, Deliver) |
| Entity | `OrderItem.cs` (entity dentro do aggregate) |
| Value Objects | `Money.cs`, `Address.cs`, `OrderNumber.cs`, `OrderStatus.cs` |
| Base Classes | `Entity.cs`, `AuditableEntity.cs`, `AggregateRoot.cs`, `ValueObject.cs` (SharedKernel) |
| Domain Events | `OrderCreatedDomainEvent.cs`, `OrderItemAddedDomainEvent.cs`, `OrderConfirmedDomainEvent.cs`, `OrderCancelledDomainEvent.cs` |
| Domain Exception | `OrderDomainException.cs` |
| Interface | `IOrderRepository.cs` |
| Testes | `OrderTests.cs`, `MoneyTests.cs`, `AddressTests.cs`, `OrderStatusTests.cs` |

---

## 📋 Resumo de Decisões Arquiteturais (ADR Index)

| ADR | Decisão | Trade-off Principal |
|-----|---------|---------------------|
| ADR-001 | Rich Domain Model sobre Anemic | Mais complexo, mas invariantes centralizadas |
| ADR-002 | Order como aggregate root, OrderItem interno | Transação atômica, mas dados de produto "stale" |
| ADR-003 | Domain Events in-process via coleção na Entity | Puro e testável, mas não cruza bounded contexts |
| ADR-004 | ValueObject base class sobre C# record | Controle fino de equality, mas mais boilerplate |

> 📐 **Para o arquiteto em formação:** Cada ADR responde a pergunta *"por que fizemos X e não Y?"*. Em code reviews, entrevistas e post-mortems, essa é a pergunta mais importante. Acostume-se a registrar decisões — seu eu futuro (e seu time) agradecerá.

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

> 🔍 **Nota de Engenharia — Por que separar transações:**
> - **Lock contention**: Se Order e Inventory estão na mesma transação, o banco mantém locks nas duas tabelas simultaneamente. Sob carga, isso causa deadlocks ou timeouts.
> - **Latência**: A transação dura o tempo da operação mais lenta. Se Inventory estiver em outro banco/serviço, a latência dobra.
> - **Acoplamento**: Se Inventory falha, Order também falha — não pode confirmar um pedido porque o estoque deu timeout.
> - **Solução com eventual consistency**: Order confirma (commit). Evento vai para handler que decrementa Inventory (outro commit). Se Inventory falha, o handler retenta (resilience). O pedido **já está confirmado** — o estoque será ajustado em milissegundos.
>
> 📖 **Pat Helland — "Life beyond Distributed Transactions" (2007, artigo técnico, sem edição em pt-br):**
> *"Em geral, desenvolvedores de aplicações simplesmente não podem depender de transações distribuídas para proteger a integridade de suas atividades."*
>
> Essa é a realidade de microservices: transações distribuídas (2PC) são caras e frágeis. Eventual consistency via eventos é o padrão da indústria.

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

> 🔍 **Nota de Engenharia — Concorrência Otimista:**
> - **`Version`** é um **concurrency token** — incrementa a cada mudança. EF Core inclui `WHERE Version = @originalVersion` no UPDATE SQL.
> - Se outro request alterou entre leitura e escrita, o `WHERE` não encontra a row → EF lança `DbUpdateConcurrencyException`.
> - **Alternativa: `RowVersion` com `byte[]`** — SQL Server tem `ROWVERSION` nativo que muda automaticamente a cada update. Não precisa incrementar manualmente.
> - **Trade-off**: Concorrência otimista assume que conflitos são **raros**. Em cenários de alta contenção (leilão, estoque de ingresso), considere pessimistic locking (`SELECT ... FOR UPDATE`).

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

> 🔍 **Nota de Engenharia — ACL na prática:**
> O `LegacyCrmAdapter` é um **Adapter** (GoF) que traduz o modelo externo (`raw.cust_no`, `raw.fname`) para o modelo do nosso domínio (`CustomerId`, `CustomerName`). O domínio **nunca vê** o modelo legado — apenas o modelo limpo que o adapter retorna.
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 14 — "Camada Anticorrupção":**
> *"Crie uma camada isolante para fornecer aos clientes funcionalidade nos termos de seu próprio modelo de domínio. A camada se comunica com o outro sistema através de sua interface existente, exigindo pouca ou nenhuma modificação no outro sistema."*

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

> 🔍 **Nota de Engenharia — Specification como Expression Tree:**
> A `Specification<T>` encapsula um `Expression<Func<T, bool>>` — que o EF Core traduz para SQL `WHERE`. Não é `Func<T, bool>` (que executaria em memória). A Expression Tree é uma **árvore sintática abstrata (AST)** — a mesma estrutura de dados que compiladores usam. EF Core percorre a árvore e gera SQL. Isso combina a expressividade de LINQ com a performance de queries nativas.
>
> 📖 **Eric Evans — DDD (2003, Alta Books), Cap. 9 — "Especificações":**
> *"Crie Objetos de Valor explícitos semelhantes a predicados para propósitos especializados. Uma Especificação é um predicado que determina se um objeto satisfaz ou não algum critério."*

### A6. Event Sourcing — Preview

Em vez de salvar o **estado**, salva-se a sequência de **eventos**. Estado = `reduce(eventos)`. Aprofundamento completo na [Fase 13](./fase-13-grpc-kafka-eventsourcing.md#6-event-sourcing--conceito).

### 💼 Perguntas Sênior

**"Como você modelaria 'cada item do pedido pode ter desconto independente'?"** — `OrderItem` como entidade dentro do aggregate `Order`, com seu próprio `Discount` value object. `Order.AddItem(productId, qty, discount)` valida invariantes (desconto máximo, política de combinação). Nunca expor `Items` mutável — encapsular.

**"Como dois aggregates ficam consistentes sem 2PC?"** — Eventual consistency via Domain Event publicado pós-commit. Aggregate A salva, evento sai (Outbox), Aggregate B em outro processo recebe e atualiza. Aceita janela de inconsistência (ms a s) em troca de escalabilidade e resiliência.

---

## 📚 Referências Bibliográficas

| Obra | Autor(es) | Ano | Editora pt-br | Relevância para esta fase |
|------|-----------|-----|---------------|---------------------------|
| **Domain-Driven Design: Atacando as Complexidades no Coração do Software** | Eric Evans | 2003 | Alta Books | Livro fundacional — Aggregates, Entities, Value Objects, Domain Events, Repositories |
| **Implementando Domain-Driven Design** | Vaughn Vernon | 2013 | Alta Books | Aplicação prática do DDD — aggregate design rules, bounded contexts |
| **Padrões de Arquitetura de Aplicações Corporativas** | Martin Fowler | 2002 | Bookman | Money pattern, Domain Model vs Transaction Script, Anemic Domain Model |
| **Arquitetura Limpa: O Guia do Artesão para Estrutura e Design de Software** | Robert C. Martin | 2017 | Alta Books | Dependency Rule, separação de camadas, independência de frameworks |
| **Código Limpo: Habilidades Práticas do Agile Software** | Robert C. Martin | 2008 | Alta Books | Naming, error handling, funções pequenas e focadas |
| **Padrões de Projeto: Soluções Reutilizáveis de Software Orientado a Objetos** | Gamma, Helm, Johnson, Vlissides (GoF) | 1994 | Bookman | State, Template Method, Factory Method, Adapter |
| **TDD: Desenvolvimento Guiado por Testes** | Kent Beck | 2002 | Bookman | Filosofia de testes, Red-Green-Refactor |
| **xUnit Test Patterns** | Gerard Meszaros | 2007 | Sem edição pt-br | Padrões de teste, nomenclatura, test doubles |
| **Trabalhando Efetivamente com Código Legado** | Michael Feathers | 2004 | Bookman | Código sem testes como legacy, seams para testabilidade |
| **Java Efetivo** | Joshua Bloch | 2001 | Alta Books | Static Factory Methods (aplicável a C#), imutabilidade |
| **Life beyond Distributed Transactions** | Pat Helland | 2007 | Sem edição pt-br (artigo) | Por que eventual consistency é necessária em sistemas distribuídos |

---

> **Próximo passo:** Avance para [fase-03-cqrs-application.md](./fase-03-cqrs-application.md) para implementar CQRS com MediatR.
>
> 🚀 **Trilha Sênior relacionada:** [fase-13-grpc-kafka-eventsourcing.md](./fase-13-grpc-kafka-eventsourcing.md) — Event Sourcing como modelo alternativo.

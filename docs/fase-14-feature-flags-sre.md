# Fase 14 — Feature Flags, SLO/SLI e SRE

> **Trilha:** Sênior | **Pré-requisitos:** Fase 06 (Observabilidade), Fase 08 (CI/CD)
> **Objetivo:** Adotar **práticas SRE** — desacoplar deploy de release com **Feature Flags**, definir **SLO/SLI/Error Budget**, instrumentar **RED/USE methods**, validar contratos com **Pact** e construir cultura de **Game Days**.

### 🎯 O que você vai aprender

- **Feature Flags** com `Microsoft.FeatureManagement` + Azure App Configuration
- Estratégias: kill switch, gradual rollout, A/B test, ops toggle
- **SLI** (Indicator), **SLO** (Objective), **SLA** (Agreement) — diferença real
- **Error Budget** — quanto erro é "OK" antes de pausar features
- **RED Method** (serviços) e **USE Method** (recursos)
- **Contract Testing** com Pact .NET — evitar quebras entre serviços
- Como conduzir um **Game Day**

---

## Sumário

1. [Por Que SRE Importa para Sênior](#1-por-que-sre-importa-para-sênior)
2. [Feature Flags — Desacoplar Deploy de Release](#2-feature-flags--desacoplar-deploy-de-release)
3. [Implementação no .NET 10](#3-implementação-no-net-10)
4. [SLI, SLO, SLA — A Pirâmide](#4-sli-slo-sla--a-pirâmide)
5. [Error Budget na Prática](#5-error-budget-na-prática)
6. [RED Method](#6-red-method)
7. [USE Method](#7-use-method)
8. [Contract Testing com Pact](#8-contract-testing-com-pact)
9. [Game Days e Postmortems](#9-game-days-e-postmortems)
10. [💼 Perguntas de Entrevista](#10--perguntas-de-entrevista)

---

## 1. Por Que SRE Importa para Sênior

> *"Hope is not a strategy."* — Google SRE Book

Vagas Sênior 2026 cobram cada vez mais práticas de **engenharia de confiabilidade**. Não basta entregar feature — precisa garantir que **não quebre o sistema** e, se quebrar, que **se recupere rápido**.

| Sintoma de imaturidade | Prática SRE |
|---|---|
| "Não posso deployar sexta" | Feature flags + observabilidade + rollback automático |
| "100% uptime" como objetivo | SLO realista (99.9%) + error budget |
| Deploy = release | Deploy escuro + flag controla release |
| "Está lento" sem métrica | SLI + dashboards + alertas |
| Incidente sem aprendizado | Postmortem blameless + ações de prevenção |

---

> 🤔 **Pense antes de ler:**
> 1. Qual a diferença entre **deploy** e **release**? Por que seprar os dois é tão poderoso?
> 2. Se um SLO é 99.9%, quanto downtime por mês você pode ter? (Faça a conta.)
> 3. O que é um **postmortem blameless** e por que culpar pessoas é contraproducente?

## 2. Feature Flags — Desacoplar Deploy de Release

**Tese:** deploy é mecânico (sobe código); release é estratégico (libera para usuário). Feature flags separam os dois.

### Tipos de Flags

| Tipo | TTL | Exemplo |
|---|---|---|
| **Release** | Curto (dias-semanas) | "Novo checkout para 10% dos usuários" |
| **Experiment (A/B)** | Médio | "Algoritmo de recomendação A vs B" |
| **Ops (kill switch)** | Indefinido | "Desabilitar emails durante incidente" |
| **Permission** | Indefinido | "Feature beta para enterprise tier" |

### Vantagens

- **Deploy seguro:** push de código sem ativar
- **Rollback instantâneo:** flag off, sem CI/CD
- **Canary:** 1% → 10% → 50% → 100% sem deploy
- **Trunk-based development:** features incompletas merge protegidas por flag
- **Kill switch:** desativar feature problemática em incidente

### Cuidados (Anti-Padrões)

- **Feature flag debt:** flags antigas nunca removidas → código morto
  - Solução: TTL obrigatório + tarefa de cleanup no sprint
- **Lógica complexa em flags:** if-else aninhado vira pesadelo
  - Solução: 1 feature = 1 flag; combinações limitadas
- **Flags em hot path sem cache:** N requests/s → N consultas ao serviço de flags
  - Solução: cache local + refresh periódico (5-30s)

---

## 3. Implementação no .NET 10

### Pacotes
```
dotnet add package Microsoft.FeatureManagement.AspNetCore
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore
dotnet add package Microsoft.Extensions.Configuration.AzureAppConfiguration.FeatureManagement
```

### `Program.cs`

```csharp
builder.Configuration.AddAzureAppConfiguration(o =>
{
    o.Connect(builder.Configuration["AppConfig:ConnectionString"])
     .UseFeatureFlags(opts =>
     {
         opts.CacheExpirationInterval = TimeSpan.FromSeconds(30);  // 🔑 cache local
     });
});

builder.Services.AddFeatureManagement()
    .AddFeatureFilter<PercentageFilter>()         // % rollout
    .AddFeatureFilter<TimeWindowFilter>()         // só em janela de tempo
    .AddFeatureFilter<TargetingFilter>();         // por usuário/grupo

// Targeting context (por user)
builder.Services.AddSingleton<ITargetingContextAccessor, HttpContextTargetingAccessor>();
```

### Uso — Endpoint

```csharp
app.MapPost("/orders/checkout-v2",
    [FeatureGate("NewCheckoutFlow")] (CheckoutCommand cmd) => /* ... */);

// Ou inline
app.MapPost("/orders/checkout", async (
    CheckoutCommand cmd,
    IFeatureManager features) =>
{
    if (await features.IsEnabledAsync("NewCheckoutFlow"))
        return await HandleV2(cmd);
    return await HandleV1(cmd);
});
```

### Configuração no Azure App Config

```json
{
  "id": "NewCheckoutFlow",
  "enabled": true,
  "conditions": {
    "client_filters": [
      {
        "name": "Microsoft.Targeting",
        "parameters": {
          "Audience": {
            "Users": ["beta@acme.com"],
            "Groups": [{ "Name": "BetaUsers", "RolloutPercentage": 25 }],
            "DefaultRolloutPercentage": 5
          }
        }
      }
    ]
  }
}
```

### Targeting Determinístico

Por padrão, % rollout é **estável por usuário** (mesmo userId sempre cai no mesmo bucket). Crítico para experimentos A/B — usuário não pode "alternar" entre variantes ao recarregar.

---

## 4. SLI, SLO, SLA — A Pirâmide

```
        ┌──────────┐
        │   SLA    │  ← contrato com cliente externo (penalidade $)
        ├──────────┤
        │   SLO    │  ← meta interna, mais agressiva que o SLA
        ├──────────┤
        │   SLI    │  ← métrica medida (a verdade)
        └──────────┘
```

### Definições

- **SLI** (Service Level **Indicator**): métrica observável.
  - *Ex: % de requests `GET /orders` com status 2xx e latência < 300ms*

- **SLO** (Service Level **Objective**): alvo do SLI.
  - *Ex: 99.9% das requests atendendo o SLI por mês corrido*

- **SLA** (Service Level **Agreement**): contrato externo.
  - *Ex: 99.5% mensal ou crédito de 10% na fatura*

> **Regra de ouro:** SLO **mais agressivo** que SLA. Você quer **descobrir** problemas antes do cliente reclamar.

### Como Definir um Bom SLI

```
✅ Bom SLI: % de requests GET /orders com status 2xx e latência P95 < 300ms
❌ Ruim:   "uptime" — vago, ignora qualidade
❌ Ruim:   "average response time" — esconde caudas longas
```

**Templates SLI úteis:**

| Categoria | Template |
|---|---|
| Disponibilidade | `success / total` |
| Latência | `p95(latency) < target` |
| Qualidade | `correct_responses / total` |
| Throughput | `requests / second` |
| Frescor de dados | `time(data) - now() < target` |

---

## 5. Error Budget na Prática

```
Error Budget = 1 - SLO
SLO 99.9% mensal → Budget = 0.1% = ~43 minutos de erro/mês
```

### Política de Uso

| Estado do Budget | Ação |
|---|---|
| **> 50% restante** | Deploy livre, foco em features |
| **20-50%** | Cuidado: revisar releases recentes |
| **< 20%** | Congelar features novas; foco em confiabilidade |
| **0% (esgotado)** | Deploy só para corrigir confiabilidade ou bug crítico |

### Burndown Chart

```
Budget mensal: 43 minutos
Dia 1:  ▓░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  2 min usados
Dia 10: ▓▓▓▓▓░░░░░░░░░░░░░░░░░░░░░░░░░  15 min — alerta
Dia 25: ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░░░░  43 min — esgotado, freeze
```

### Vantagem Cultural

Error Budget remove o conflito **dev (entregar) vs ops (estabilidade)**. Vira número objetivo: enquanto há budget, deploy livre; esgotou, todos focam em confiabilidade.

---

## 6. RED Method

Para **serviços** (request-driven):

| Métrica | O que medir |
|---|---|
| **R**ate | Requests por segundo |
| **E**rrors | % de requests com erro |
| **D**uration | Distribuição de latência (P50, P95, P99) |

### Instrumentação .NET 10

```csharp
public sealed class RedMetrics
{
    private readonly Counter<long> _requestCount;
    private readonly Counter<long> _errorCount;
    private readonly Histogram<double> _duration;

    public RedMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("OrderFlow.Orders");
        _requestCount = meter.CreateCounter<long>("orders.requests.total");
        _errorCount = meter.CreateCounter<long>("orders.requests.errors");
        _duration = meter.CreateHistogram<double>("orders.requests.duration", "ms");
    }

    public IDisposable Track(string endpoint)
    {
        var sw = ValueStopwatch.StartNew();
        _requestCount.Add(1, new TagList { { "endpoint", endpoint } });
        return new TrackingScope(this, endpoint, sw);
    }
    // ... no Dispose: registra duration; em erro: incrementa errorCount
}
```

### Dashboard Grafana

3 painéis por serviço:
1. **Rate:** `sum(rate(orders_requests_total[5m])) by (endpoint)`
2. **Errors:** `sum(rate(orders_requests_errors[5m])) / sum(rate(orders_requests_total[5m]))`
3. **Duration P95:** `histogram_quantile(0.95, sum(rate(orders_requests_duration_bucket[5m])) by (le))`

---

## 7. USE Method

Para **recursos** (CPU, memória, disco, rede, conexões):

| Métrica | O que medir |
|---|---|
| **U**tilization | % do tempo ocupado |
| **S**aturation | Quanto trabalho está em fila |
| **E**rrors | Contagem de erros do recurso |

### Exemplos

| Recurso | Utilization | Saturation | Errors |
|---|---|---|---|
| CPU | % busy | run queue length | machine check exceptions |
| Memory | % used | swap I/O | OOM kills |
| Disk | % busy | I/O queue | I/O errors |
| Network | bandwidth used | TCP retransmits | drops |
| **Connection Pool** | `active / max` | `pending requests` | `acquisition timeouts` |
| **Thread Pool** | `busy / max` | `queue length` | starvation |

### Anti-Padrão Clássico

App lento, dashboard mostra CPU=30%. Conclusão errada: "tem capacidade". CPU baixa **com saturação alta** (run queue cheia) = contenção, não folga. Sempre olhar U + S + E **juntos**.

---

## 8. Contract Testing com Pact

Em microserviços, mudança de contrato em um serviço **quebra outros** silenciosamente. Testes E2E são lentos e frágeis. **Contract Testing** valida o contrato **entre dois serviços** sem subir tudo.

### Fluxo Pact

```
Consumer (Orders) define expectativas
   ↓
Roda teste consumer → gera contrato (pact.json)
   ↓
Sobe para Pact Broker
   ↓
Provider (Catalog) baixa pact e valida sua API contra ele
   ↓
✅ Compatível → pode deployar
❌ Quebra → CI falha antes de produção
```

### Exemplo .NET — Consumer Side

```csharp
[Fact]
public async Task GetProduct_WhenExists_Returns200WithBody()
{
    var pact = Pact.V4("orders-api", "catalog-api", new PactConfig());
    var pactBuilder = pact.WithHttpInteractions();

    pactBuilder
        .UponReceiving("a request for an existing product")
            .Given("product abc-123 exists")
            .WithRequest(HttpMethod.Get, "/api/products/abc-123")
            .WithHeader("Authorization", Match.Type("Bearer xyz"))
        .WillRespond()
            .WithStatus(200)
            .WithJsonBody(new
            {
                id = Match.Type("abc-123"),
                name = Match.Type("Sample Product"),
                price = Match.Decimal(99.90m),
                stock = Match.Integer(5)
            });

    await pactBuilder.VerifyAsync(async ctx =>
    {
        var client = new CatalogClient(new HttpClient { BaseAddress = ctx.MockServerUri });
        var product = await client.GetByIdAsync(Guid.Parse("abc-123"), CancellationToken.None);
        product.Should().NotBeNull();
    });
}
```

Provider valida o pact gerado contra a API real.

> **Vantagem:** quando Catalog renomeia `price` para `unitPrice`, pact provider falha **antes** do deploy. Sem precisar subir Orders.

---

## 9. Game Days e Postmortems

### Game Day

Exercício programado onde injetam-se **falhas controladas** em produção (ou staging realista) e o time responde como se fosse incidente real.

**Ciclo:**
1. Definir cenário ("Redis cai por 10 min")
2. Anunciar (não surpresa — é treino)
3. Injetar falha (Simmy, kubectl delete, blocking firewall)
4. Time responde com runbook
5. Postmortem coletivo: o que funcionou, o que não

**Frequência:** mensal ou trimestral.

### Blameless Postmortem

Após **incidente real**, documento:

```markdown
## Incidente #2026-04-15-001

### Resumo
30 min de indisponibilidade do Orders API entre 14:30-15:00 UTC.

### Impacto
- 12k requests falharam (HTTP 5xx)
- 230 pedidos não criados
- Error budget consumido: 35 min de 43 min mensais

### Timeline
- 14:30 — Deploy do Orders v2.1.4
- 14:32 — Latência P95 sobe para 5s
- 14:35 — Alertas disparam
- 14:42 — Engenheiro on-call identifica
- 14:48 — Rollback iniciado
- 15:00 — Serviço estabilizado

### Causa raiz
Migração EF Core sem índice na nova coluna OrderStatus, fazendo full scan em queries de listagem.

### O que funcionou
- Alertas dispararam em 5min
- Rollback em 1 comando

### O que falhou
- PR review não pegou ausência do índice
- Smoke test pós-deploy não validou latência

### Ações
- [ ] (Owner: Maria) Adicionar lint que bloqueia migration sem índice em FK
- [ ] (Owner: João) Smoke test pós-deploy validando P95
- [ ] (Owner: Squad) Game Day com cenário "DB lento" no próximo mês

### Sem culpados
Engenheiro X seguiu o processo existente. O processo precisa evoluir.
```

**Princípios:**
- Foco em **sistema/processo**, não pessoas
- Ações **acionáveis** com owner + prazo
- Compartilhar amplamente — todo time aprende

---

## ⚠️ Erros Comuns em Feature Flags e SRE

| # | Erro | Consequência | Solução |
|---|---|---|---|
| 1 | **Feature flag permanente** | Código cheio de `if (featureEnabled)` — complexidade acidental | Flags de release devem ter TTL. Remova após rollout completo |
| 2 | **Flag sem fallback** | Serviço de feature flags cai → exceção em runtime | Sempre defina valor default: `if (await featureManager.IsEnabledAsync("NewCheckout") ?? false)` |
| 3 | **SLO sem SLI** | Meta sem métrica = meta impossível de verificar | Defina o SLI primeiro (ex: latência p99), depois o SLO (ex: < 200ms) |
| 4 | **Alerta em toda métrica** | Alert fatigue → equipe ignora alertas reais | Alerte apenas em **SLO burn rate**. Dashboards para o resto |
| 5 | **Postmortem blameful** | Pessoas escondem erros → incidentes se repetem | Foque em **sistema**, não em pessoas. "O que permitiu que isso acontecesse?" |
| 6 | **Canary release sem rollback automático** | Canary com erros continua recebendo tráfego | Configure rollback automático baseado em error rate threshold |

---

## 🔧 Troubleshooting — Fase 14

| Sintoma | Causa Provável | Solução |
|---------|---------------|---------|
| Feature flag sempre retorna false | Filtro de targeting não configurado ou user context vazio | Verifique `ITargetingContextAccessor` retorna `UserId` correto |
| Grafana dashboard "No data" | Prometheus não scraping a aplicação | Verifique `prometheus.yml` targets e firewall |
| SLO burn rate alert disparando sem causa | Threshold muito sensível | Ajuste janela de avaliação (use multi-window: 1h + 6h) |
| Postmortem sem ações úteis | Template ruim ou cultura de blame | Use template estruturado: Timeline → Root Cause → Action Items com owner |

---

## 10. 💼 Perguntas de Entrevista

**1. "Diferencie SLI, SLO, SLA."**
— **SLI** = métrica observável (% requests bem-sucedidas). **SLO** = meta interna sobre o SLI (99.9% mensal). **SLA** = contrato externo (99.5% ou crédito). SLO sempre mais agressivo que SLA — você descobre antes do cliente.

**2. "O que é Error Budget?"**
— `1 - SLO`. SLO 99.9% → budget de 0.1% = 43min/mês. Resolve o conflito dev vs ops: enquanto há budget, deploy livre; esgotou, todos focam em confiabilidade.

**3. "Por que separar deploy de release?"**
— Deploy é mecânico (código sobe); release é decisão de produto (libera). Feature flags separam. Vantagens: rollback instantâneo (sem CI/CD), canary controlado, kill switch em incidente, trunk-based dev sem pull requests longos.

**4. "Diferencie RED e USE methods."**
— **RED** mede **serviços** (Rate, Errors, Duration) — visão do consumidor. **USE** mede **recursos** (Utilization, Saturation, Errors) — visão da infra. RED diz "está lento"; USE diz "porque está lento" (CPU saturada? pool de conexões cheio?).

**5. "Como evitar feature flag debt?"**
— (1) TTL obrigatório no metadata da flag. (2) Tarefa no sprint para limpar flags antigas. (3) Linter detectando flags > N meses. (4) Limite de flags ativas por serviço (~10-20). Flag sem dono = remove.

**6. "Por que contract testing em vez de E2E?"**
— E2E exige subir tudo, é lento, frágil (falha por motivo errado), difícil de debugar. Contract testing valida **interface entre 2 serviços** isoladamente. Quebra de contrato é detectada **antes do deploy**, não em produção.

**7. "Como conduzir um postmortem blameless?"**
— Foco em **sistema/processo**, não pessoas. Documentar: timeline, impacto, causa raiz, o que funcionou, o que falhou, **ações acionáveis com owner**. Compartilhar amplamente. Pessoas seguiram o processo existente — se algo deu errado, o processo precisa evoluir.

---

## Checkpoint

✅ Microsoft.FeatureManagement integrado com Azure App Config
✅ Pelo menos 1 feature em rollout gradual (5% → 25% → 100%)
✅ SLO documentado para cada serviço (disponibilidade + latência)
✅ Dashboard Grafana com RED por serviço e USE por recurso
✅ Pact contract test entre Orders (consumer) e Catalog (provider)
✅ Runbook documentado para top 3 cenários de incidente
✅ Primeiro Game Day executado e postmortem registrado

➡️ **Próxima fase:** [`fase-15-ai-integration.md`](./fase-15-ai-integration.md) — última fase!

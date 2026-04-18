# Fase 15 — Integração com IA: Semantic Kernel & AI Gateway

> **Trilha:** Sênior+ | **Pré-requisitos:** Fases 01-14
> **Objetivo:** Integrar capacidades de IA generativa ao OrderFlow de forma **idiomática .NET** — usando **Semantic Kernel** (Microsoft) — e construir um **AI Gateway pattern** para controlar custo, latência, fallback e segurança das chamadas a LLMs.

### 🎯 O que você vai aprender

- Como **IA Generativa** se encaixa em backends enterprise (não é só chatbot)
- **Semantic Kernel** — abstração over LLMs (OpenAI, Azure OpenAI, Mistral, etc.)
- **Function Calling** — LLM chama suas funções C# como ferramentas
- **RAG** (Retrieval Augmented Generation) — LLM responde sobre seus dados
- **AI Gateway pattern** — proxy entre app e LLM com cache, rate limit, fallback, auditoria
- Custo, latência, alucinação — riscos reais e mitigações
- Quando NÃO usar IA — anti-padrões caros

---

## Sumário

1. [IA Generativa em Backend Enterprise](#1-ia-generativa-em-backend-enterprise)
2. [Por Que Semantic Kernel](#2-por-que-semantic-kernel)
3. [Setup no OrderFlow](#3-setup-no-orderflow)
4. [Function Calling — LLM como Orquestrador](#4-function-calling--llm-como-orquestrador)
5. [RAG sobre o Catálogo](#5-rag-sobre-o-catálogo)
6. [AI Gateway Pattern](#6-ai-gateway-pattern)
7. [Custo, Latência e Alucinação](#7-custo-latência-e-alucinação)
8. [Quando NÃO Usar IA](#8-quando-não-usar-ia)
9. [💼 Perguntas de Entrevista](#9--perguntas-de-entrevista)

---

## 1. IA Generativa em Backend Enterprise

> **🤔 Pergunta Socrática:** *"Onde IA agrega valor real no OrderFlow — sem virar 'usar GPT por usar'?"*

Casos legítimos:

| Cenário | Valor |
|---|---|
| **Sumarização de pedidos** para suporte ao cliente | Atendente vê resumo em vez de ler 30 itens |
| **Classificação de feedback** | Categorizar tickets automaticamente (bug, feature request, dúvida) |
| **Chat sobre catálogo** (RAG) | Cliente pergunta "tem produto vegano até R$ 50?" |
| **Geração de descrições** de produto | Marketing produz mais rápido |
| **Detecção de anomalias** | "Pedido de R$ 50k de cliente que sempre comprou R$ 100" |
| **Resumo de logs** em postmortem | Engenheiro recebe síntese em vez de 10MB de log |

**Anti-uso (caro e arriscado):**
- Cálculos determinísticos (use código)
- Validações com regras claras (use FluentValidation)
- Workflows transacionais críticos (LLM é probabilístico)

---

## 2. Por Que Semantic Kernel

| Opção | Pros | Contras |
|---|---|---|
| **OpenAI SDK direto** | Simples para protótipo | Acoplado, sem abstração |
| **Semantic Kernel** | Abstração over LLMs, plugins, planners, **first-party Microsoft** | Curva de aprendizado |
| **LangChain.NET** | Ecossistema rico (vinda de Python) | .NET é cidadão de segunda classe |
| **Microsoft.Extensions.AI** | Abstração mais nova, lightweight | Menos features que SK |

**Recomendação 2026:** **Semantic Kernel** para apps complexos com plugins/RAG; **Microsoft.Extensions.AI** para abstração simples sobre chat.

---

## 3. Setup no OrderFlow

### Pacotes
```
dotnet add package Microsoft.SemanticKernel
dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI
dotnet add package Microsoft.SemanticKernel.Connectors.AzureAISearch     # RAG
```

### `Program.cs`

```csharp
builder.Services.AddKernel()
    .AddAzureOpenAIChatCompletion(
        deploymentName: "gpt-4o-mini",
        endpoint: builder.Configuration["AzureOpenAI:Endpoint"]!,
        apiKey: builder.Configuration["AzureOpenAI:ApiKey"]!)
    .AddAzureOpenAITextEmbeddingGeneration(
        deploymentName: "text-embedding-3-small",
        endpoint: builder.Configuration["AzureOpenAI:Endpoint"]!,
        apiKey: builder.Configuration["AzureOpenAI:ApiKey"]!);

// Plugins (funções expostas ao LLM)
builder.Services.AddSingleton<OrderQueryPlugin>();
builder.Services.AddSingleton<CatalogPlugin>();
```

### Uso Simples — Sumarização

```csharp
public sealed class OrderSummaryService(Kernel kernel)
{
    public async Task<string> SummarizeAsync(Order order, CancellationToken ct)
    {
        var prompt = """
            Summarize this order in 2-3 sentences for a customer support agent.
            Include: total value, number of items, and any unusual patterns.

            Order:
            {{$order}}
            """;

        var result = await kernel.InvokePromptAsync(prompt,
            new KernelArguments { ["order"] = JsonSerializer.Serialize(order) },
            cancellationToken: ct);

        return result.ToString();
    }
}
```

---

## 4. Function Calling — LLM como Orquestrador

LLM **decide** qual função chamar baseado na pergunta. Você expõe métodos C#:

```csharp
public sealed class OrderQueryPlugin(IOrderRepository repo)
{
    [KernelFunction, Description("Get total spending of a customer in last N days.")]
    public async Task<decimal> GetCustomerSpendingAsync(
        [Description("Customer ID (GUID)")] Guid customerId,
        [Description("Period in days, max 365")] int days,
        CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var orders = await repo.GetByCustomerSinceAsync(customerId, since, ct);
        return orders.Sum(o => o.Total.Amount);
    }

    [KernelFunction, Description("Get count of cancelled orders in a date range.")]
    public async Task<int> GetCancelledCountAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        => await repo.CountByStatusAsync(OrderStatus.Cancelled, from, to, ct);
}

// Registrar no kernel
kernel.Plugins.AddFromObject(scope.ServiceProvider.GetRequiredService<OrderQueryPlugin>());

// Uso — LLM escolhe quais funções chamar
var settings = new OpenAIPromptExecutionSettings
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

var answer = await kernel.InvokePromptAsync(
    "How much did customer abc-123 spend in the last 30 days, and how many of their orders were cancelled this year?",
    new KernelArguments(settings));
```

LLM analisa a pergunta, **chama as 2 funções automaticamente**, junta os resultados em uma resposta natural.

> **Cuidado de produção:**
> - **Nunca** exponha funções que escrevem (transações financeiras, deletes) sem confirmação humana
> - Funções devem ser **idempotentes** quando possível
> - Sempre validar parâmetros — LLM pode passar valores absurdos

---

## 5. RAG sobre o Catálogo

**Problema:** LLM não conhece o catálogo do OrderFlow. Treinou em 2024, dados mudam diariamente.

**Solução:** RAG — embedda os produtos, busca os mais similares à pergunta, passa como contexto.

### Pipeline RAG

```
1. INGESTION (offline)
   Produtos → embeddings → vector DB (Azure AI Search / Qdrant)

2. QUERY (runtime)
   Pergunta do usuário → embedding → busca top-K similares → contexto + pergunta → LLM
```

### Implementação

```csharp
public sealed class CatalogRagService(
    Kernel kernel,
    ITextEmbeddingGenerationService embeddings,
    AzureAISearchClient searchClient)
{
    public async Task IngestAsync(Product product, CancellationToken ct)
    {
        var text = $"{product.Name}. {product.Description}. Categories: {string.Join(", ", product.Categories)}.";
        var embedding = await embeddings.GenerateEmbeddingAsync(text, cancellationToken: ct);

        await searchClient.UpsertAsync(new ProductDocument
        {
            Id = product.Id.ToString(),
            Text = text,
            Embedding = embedding.ToArray(),
            Price = product.Price.Amount,
            Tags = product.Tags
        }, ct);
    }

    public async Task<string> AskAsync(string userQuestion, CancellationToken ct)
    {
        // 1. Embed da pergunta
        var queryEmbedding = await embeddings.GenerateEmbeddingAsync(userQuestion, cancellationToken: ct);

        // 2. Busca top-5 similares
        var topK = await searchClient.SearchAsync(queryEmbedding.ToArray(), k: 5, ct);

        // 3. Monta prompt com contexto
        var context = string.Join("\n\n", topK.Select(p =>
            $"- {p.Text} (R$ {p.Price})"));

        var prompt = $$"""
            You are a helpful catalog assistant. Answer the question using ONLY the products below.
            If none match, say so honestly. Never invent products or prices.

            Available products:
            {{context}}

            Question: {{userQuestion}}
            """;

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        return result.ToString();
    }
}
```

### Boas Práticas RAG

- **Re-embed** quando produto muda (hook em `ProductUpdated` event)
- **Chunking** para textos longos (300-500 tokens por chunk)
- **Hybrid search:** vector + keyword (BM25) combinados — Azure AI Search faz nativo
- **Citation:** mostrar IDs dos produtos usados para responder (auditoria)
- **Guardrails:** "responda APENAS com base no contexto" — reduz alucinação

---

## 6. AI Gateway Pattern

Em vez de cada serviço chamar OpenAI direto, **um gateway interno** centraliza:

```
┌─────────┐    ┌─────────────┐    ┌──────────────┐
│ Orders  │───▶│             │───▶│ Azure OpenAI │
├─────────┤    │             │───▶│   OpenAI     │
│ Catalog │───▶│ AI Gateway  │───▶│   Mistral    │
├─────────┤    │             │
│ Support │───▶│             │
└─────────┘    └─────────────┘
                     │
                     ▼
              ┌─────────────┐
              │   Redis     │ (cache)
              ├─────────────┤
              │   Audit DB  │ (compliance)
              └─────────────┘
```

### Responsabilidades do Gateway

| Capability | Por quê |
|---|---|
| **Caching** | Mesma pergunta → mesma resposta. Economiza tokens. |
| **Rate limiting** | LLMs têm cotas; sem gateway, um serviço estoura para todos |
| **Fallback** | Azure OpenAI fora? Failover para OpenAI direto |
| **Cost tracking** | Custo por serviço/feature/usuário (showback) |
| **Auditoria** | Log de todas as queries (requisito regulatório) |
| **PII redaction** | Sanitizar antes de enviar ao LLM |
| **Prompt injection defense** | Filtros antes de enviar |
| **A/B de modelos** | Comparar gpt-4o vs gpt-4o-mini sem mudar código nos serviços |

### Esqueleto

```csharp
public sealed class AiGatewayService(
    IDistributedCache cache,
    IEnumerable<IChatCompletionService> providers,    // múltiplos providers
    AuditLogger audit,
    PolicyRegistry policies)
{
    public async Task<string> CompleteAsync(
        ChatRequest request, CancellationToken ct)
    {
        // 1. Cache lookup (hash determinístico do prompt)
        var cacheKey = HashRequest(request);
        if (await cache.GetAsync<string>(cacheKey) is { } cached)
            return cached;

        // 2. PII redaction
        request.Messages = _piiRedactor.Redact(request.Messages);

        // 3. Polly pipeline: retry + circuit breaker + fallback
        var response = await policies
            .Get<ResiliencePipeline<string>>("ai-completion")
            .ExecuteAsync(async token =>
            {
                var primary = providers.First();
                return await primary.GetChatMessageContentAsync(
                    request.Messages, request.Settings, cancellationToken: token);
            }, ct);

        // 4. Audit
        await audit.LogAsync(request, response, ct);

        // 5. Cache (TTL curto, ex: 5 min)
        await cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

        return response;
    }
}
```

> **Padrão de mercado:** **Azure API Management** com policies de OpenAI faz muito disso pronto (rate limit por subscription, semantic caching, fallback entre regiões).

---

## 7. Custo, Latência e Alucinação

### Custo

| Modelo | Input ($/1M tok) | Output ($/1M tok) | Latência típica |
|---|---|---|---|
| GPT-4o | ~$2.50 | ~$10 | 1-3s |
| GPT-4o-mini | ~$0.15 | ~$0.60 | 0.5-1.5s |
| Embeddings (small) | ~$0.02 | — | <100ms |

**Estratégias de redução:**
- **Cache** agressivo (Redis, semantic cache)
- Modelo **menor** quando aceitável (gpt-4o-mini para sumarização simples)
- **Few-shot** em vez de fine-tuning (mais barato)
- **Truncar contexto** — não mande logs gigantes inteiros
- **Batch** quando possível (embeddings em lote)

### Latência

LLM = SLA pior que API tradicional. Para hot path de usuário:
- **Streaming** — comece a exibir tokens conforme chegam
- **Async** — fire-and-forget, notifica via WebSocket quando pronto
- **Skeleton UI** — mostra placeholder enquanto LLM processa

### Alucinação

LLM **inventa fatos** com confiança. Mitigações:

- **Grounding via RAG** — passe fonte como contexto
- **System prompt restritivo:** *"responda APENAS com base no contexto; se não souber, diga 'não tenho essa informação'"*
- **Validação pós-LLM** — extrair claims e verificar contra fonte
- **Citation obrigatória** — peça ao LLM citar IDs/links da fonte
- **Temperatura baixa** (0.0-0.3) para tarefas factuais; alta (0.7-1.0) só para criatividade

---

## 8. Quando NÃO Usar IA

| Caso | Use isto, não LLM |
|---|---|
| Cálculo de total do pedido | Código — LLM erra aritmética |
| Validação de CPF | Algoritmo determinístico |
| Roteamento de mensagens (regras claras) | If/else, MediatR behavior |
| Análise estatística | Código + biblioteca |
| Pesquisa exata em DB | SQL ou Elasticsearch |
| Workflow transacional crítico | Saga + state machine |

**Heurística:**
> *"Se você consegue escrever as regras explicitamente, não use LLM. LLM brilha em **ambiguidade** e **linguagem natural** — paga caro por isso."*

---

## 9. 💼 Perguntas de Entrevista

**1. "Onde IA generativa agrega valor real em backend enterprise?"**
— Em problemas com **ambiguidade** ou **linguagem natural**: sumarização, classificação de feedback, chat sobre dados (RAG), geração de conteúdo, detecção de anomalias. **Não** em cálculos, validações determinísticas ou workflows transacionais — LLM é probabilístico e caro.

**2. "O que é RAG e quando usar?"**
— Retrieval Augmented Generation: embedda dados, busca os mais similares à pergunta, passa como contexto para o LLM responder. Use quando: (1) LLM precisa conhecer **seus dados** atualizados, (2) responder com **citação** auditável, (3) reduzir alucinação. Componentes: embedding model, vector DB, retriever, prompt template.

**3. "O que é Function Calling?"**
— LLM decide **qual função chamar** baseado na pergunta. Você expõe métodos com descrições; LLM usa-os como ferramentas. Útil para "agentic workflows" — IA orquestra chamadas a APIs reais. Cuidado: nunca exponha operações destrutivas sem confirmação humana.

**4. "O que é AI Gateway e por que importa?"**
— Proxy entre apps e LLMs com: caching (mesma pergunta → mesma resposta), rate limiting (cotas), fallback (multi-provider), audit (compliance), cost tracking, PII redaction. Sem gateway, cada serviço chama LLM direto — sem visibilidade, sem controle, com 1 serviço estourando cota para todos.

**5. "Como mitigar alucinação?"**
— (1) **Grounding via RAG** — passe fonte como contexto. (2) **System prompt restritivo** — "responda só com base no contexto; senão diga não sei". (3) **Citação obrigatória** — IDs/links da fonte na resposta. (4) **Temperatura baixa** (0.0-0.3) para factual. (5) **Validação pós-LLM** — extrair claims, verificar contra fonte.

**6. "Como controlar custo de LLM em produção?"**
— Cache agressivo (semantic cache), modelo menor quando aceitável (gpt-4o-mini), truncar contexto (não mande logs inteiros), batch (embeddings em lote), few-shot em vez de fine-tuning, monitoring por feature/usuário (showback).

**7. "Quando você convenceria seu time a NÃO usar IA?"**
— Quando o problema tem **regras determinísticas claras** (validação, cálculo, roteamento), quando exige **precisão financeira** (LLM erra aritmética), ou quando o **custo + latência** não compensam o benefício marginal. Heurística: "se você consegue escrever as regras, não use LLM".

---

## Checkpoint

✅ Semantic Kernel rodando com Azure OpenAI ou OpenAI
✅ 1 plugin com Function Calling expondo queries do Orders
✅ RAG sobre o catálogo (ingestion + query) com Azure AI Search ou Qdrant
✅ AI Gateway interno centralizando chamadas (cache + audit + Polly)
✅ Custos monitorados por feature em dashboard
✅ ADR documentando "quando usar IA neste sistema"

---

## 🎓 Conclusão das 15 Fases

Você completou a trilha **Pleno → Sênior** alinhada às exigências do mercado .NET 2026. Próximos passos:

1. **Revisar a [matriz de competências](./00-visao-geral.md#11-matriz-de-competências-pleno-vs-sênior)** e marcar tudo
2. **Publicar o repositório** no GitHub com README rico
3. **Escrever artigos** no LinkedIn/dev.to sobre uma fase por semana
4. **Apresentar em meetup** local — uma fase escolhida
5. **Aplicar para vagas Sênior** — você tem o portfólio agora

➡️ Para visualização interativa, abra [`orderflow-guide.html`](./orderflow-guide.html).

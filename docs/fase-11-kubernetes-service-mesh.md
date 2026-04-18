# Fase 11 — Kubernetes & Service Mesh

> **Trilha:** Sênior | **Pré-requisitos:** Fases 01-08
> **Objetivo:** Migrar o OrderFlow de Docker Compose / Azure Container Apps para **Kubernetes** com manifests declarativos, **HPA** (Horizontal Pod Autoscaler), **Helm charts** e visão de **Service Mesh** (Linkerd / Dapr).

### 🎯 O que você vai aprender

- Os primitivos K8s: **Pod, Deployment, Service, Ingress, ConfigMap, Secret**
- **Probes** corretos: liveness, readiness, startup (e por que confundir derruba o sistema)
- **HPA** com métricas customizadas (RPS, fila Rabbit)
- **Helm** — templating, values, releases, charts compartilhados
- **Service Mesh** — quando vale, opções (Linkerd, Istio, Dapr)
- Padrões: Sidecar, Init Container, Job, CronJob

---

## Sumário

1. [Por Que Kubernetes (e Quando Não)](#1-por-que-kubernetes-e-quando-não)
2. [Conceitos Fundamentais](#2-conceitos-fundamentais)
3. [Manifests do Orders API](#3-manifests-do-orders-api)
4. [Probes — Liveness, Readiness, Startup](#4-probes--liveness-readiness-startup)
5. [ConfigMap e Secret](#5-configmap-e-secret)
6. [Horizontal Pod Autoscaler](#6-horizontal-pod-autoscaler)
7. [Helm — Empacotando o OrderFlow](#7-helm--empacotando-o-orderflow)
8. [Service Mesh](#8-service-mesh)
9. [Anti-Padrões Comuns](#9-anti-padrões-comuns)
10. [💼 Perguntas de Entrevista](#10--perguntas-de-entrevista)

---

## 1. Por Que Kubernetes (e Quando Não)

> **🤔 Pergunta Socrática:** *"Se ACA já oferece auto-scaling e revisões, por que K8s?"*

| Cenário | Use ACA / App Service | Use Kubernetes |
|---|---|---|
| < 10 microserviços, time pequeno | ✅ | ❌ overkill |
| Multi-cloud / on-premises | ❌ lock-in | ✅ |
| Workloads complexos (StatefulSet, DaemonSet, GPUs) | ❌ | ✅ |
| Time tem capacidade SRE/Platform | ✅ ou ✅ | ✅ |
| Time pequeno sem SRE | ✅ | ❌ vai sofrer |

**Regra honesta:** K8s é o **padrão de mercado** para sistemas de médio/grande porte. Senioridade exige saber operar, mesmo que escolha não usar.

---

## 2. Conceitos Fundamentais

| Recurso | O Que É |
|---|---|
| **Pod** | Menor unidade — 1 ou + containers compartilhando rede/storage |
| **ReplicaSet** | Garante N pods rodando |
| **Deployment** | Gerencia ReplicaSets (rolling updates, rollback) |
| **Service** | IP/DNS estável para um conjunto de pods (ClusterIP, NodePort, LoadBalancer) |
| **Ingress** | Roteamento HTTP L7 (substitui múltiplos LoadBalancers) |
| **ConfigMap** | Configuração não-sensível |
| **Secret** | Configuração sensível (base64; idealmente use Sealed Secrets ou External Secrets) |
| **Namespace** | Isolamento lógico (dev, staging, prod ou por time) |
| **PersistentVolumeClaim** | Storage persistente para StatefulSet |

---

## 3. Manifests do Orders API

`deploy/k8s/orders/deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: orders-api
  namespace: orderflow
  labels:
    app: orders-api
    version: v1
spec:
  replicas: 3
  selector:
    matchLabels:
      app: orders-api
  template:
    metadata:
      labels:
        app: orders-api
        version: v1
    spec:
      securityContext:
        runAsNonRoot: true              # 🔒 segurança
        runAsUser: 1000
        fsGroup: 1000
      containers:
        - name: orders-api
          image: ghcr.io/orderflow/orders-api:1.4.2
          imagePullPolicy: IfNotPresent
          ports:
            - containerPort: 8080
              name: http
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: Production
            - name: ASPNETCORE_URLS
              value: http://+:8080
          envFrom:
            - configMapRef:
                name: orders-config
            - secretRef:
                name: orders-secrets
          resources:
            requests:                    # 🔑 garantia mínima — usado pelo scheduler
              cpu: 200m
              memory: 256Mi
            limits:                      # teto — pod morto se ultrapassar memória
              cpu: 1000m
              memory: 512Mi
          livenessProbe:
            httpGet:
              path: /health/live
              port: http
            initialDelaySeconds: 10
            periodSeconds: 10
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /health/ready
              port: http
            initialDelaySeconds: 5
            periodSeconds: 5
            failureThreshold: 3
          startupProbe:
            httpGet:
              path: /health/startup
              port: http
            failureThreshold: 30
            periodSeconds: 2
---
apiVersion: v1
kind: Service
metadata:
  name: orders-api
  namespace: orderflow
spec:
  type: ClusterIP
  selector:
    app: orders-api
  ports:
    - port: 80
      targetPort: 8080
      name: http
```

### Resources — Requests vs Limits

- **Requests:** mínimo garantido. K8s usa para **scheduling** (escolher node).
- **Limits:** teto. CPU acima é throttled; **memória acima = OOMKill**.

**Regra:** sempre setar ambos. **Nunca** deixar memory limit ausente — um pod pode comer toda a RAM do node.

---

## 4. Probes — Liveness, Readiness, Startup

| Probe | Pergunta que responde | Falha → |
|---|---|---|
| **Liveness** | "Você está vivo?" | Reinicia o pod |
| **Readiness** | "Está pronto para tráfego?" | Tira do Service (sem reinício) |
| **Startup** | "Já terminou de subir?" | Liveness/readiness são desabilitadas até startup OK |

**Erro clássico:** usar a mesma `/health` para liveness e readiness. Se a dependência (Redis) cai, readiness deveria falhar (tira do tráfego); mas como usa o mesmo endpoint, liveness também falha → pod **reinicia em loop**.

### Configuração ASP.NET Core 10

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddSqlServer(connStr, tags: ["ready"])
    .AddRedis(redisConn, tags: ["ready"])
    .AddRabbitMQ(rabbitConn, tags: ["ready"]);

app.MapHealthChecks("/health/live", new()
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/startup", new()
{
    Predicate = _ => true  // mais permissivo até subir
});
```

---

## 5. ConfigMap e Secret

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: orders-config
  namespace: orderflow
data:
  Logging__LogLevel__Default: Information
  ConnectionStrings__Redis: redis-master.orderflow.svc.cluster.local:6379
  Services__Catalog__Url: http://catalog-api.orderflow.svc.cluster.local
---
apiVersion: v1
kind: Secret
metadata:
  name: orders-secrets
  namespace: orderflow
type: Opaque
stringData:    # stringData = K8s codifica para base64; data exige você codificar
  ConnectionStrings__OrdersDb: "Server=...;User Id=...;Password=..."
  Authentication__Jwt__Key: "long-secret-key"
```

### ⚠️ Secrets em Git são ruins

Secret K8s é apenas **base64**, não criptografia. Em produção use:
- **Azure Key Vault + CSI Driver** ou **External Secrets Operator**
- **Sealed Secrets** (Bitnami) para GitOps
- **HashiCorp Vault**

---

## 6. Horizontal Pod Autoscaler

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: orders-api
  namespace: orderflow
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: orders-api
  minReplicas: 3
  maxReplicas: 20
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
    - type: Pods                       # métrica custom (KEDA)
      pods:
        metric:
          name: rabbitmq_queue_depth
        target:
          type: AverageValue
          averageValue: "100"
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300  # evita flapping — 5min antes de reduzir
    scaleUp:
      stabilizationWindowSeconds: 30
```

> **KEDA** (Kubernetes Event-Driven Autoscaling) — escala por **profundidade de fila Rabbit/Kafka**, não só CPU. Padrão para workers.

---

## 7. Helm — Empacotando o OrderFlow

Em vez de manter manifests duplicados por ambiente (dev, staging, prod), Helm faz **templating**.

### Estrutura

```
deploy/helm/orderflow/
├── Chart.yaml
├── values.yaml                   # defaults
├── values-prod.yaml              # overrides
└── templates/
    ├── _helpers.tpl
    ├── deployment.yaml
    ├── service.yaml
    ├── hpa.yaml
    ├── configmap.yaml
    └── ingress.yaml
```

### `Chart.yaml`

```yaml
apiVersion: v2
name: orderflow
version: 1.4.2
appVersion: "1.4.2"
description: OrderFlow microservices
dependencies:
  - name: redis
    version: 19.x
    repository: https://charts.bitnami.com/bitnami
  - name: rabbitmq
    version: 14.x
    repository: https://charts.bitnami.com/bitnami
```

### `templates/deployment.yaml` (trecho)

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "orderflow.fullname" . }}-orders
spec:
  replicas: {{ .Values.orders.replicas }}
  template:
    spec:
      containers:
        - name: orders
          image: "{{ .Values.image.registry }}/orders:{{ .Values.image.tag }}"
          resources:
            {{- toYaml .Values.orders.resources | nindent 12 }}
```

### Comandos

```bash
helm install orderflow ./deploy/helm/orderflow -f values-prod.yaml -n orderflow
helm upgrade orderflow ./deploy/helm/orderflow -f values-prod.yaml -n orderflow
helm rollback orderflow 3 -n orderflow      # volta para a revisão 3
helm history orderflow -n orderflow
```

---

## 8. Service Mesh

Service Mesh injeta um **sidecar proxy** (geralmente Envoy) em cada pod. Esse proxy intercepta todo o tráfego — fornece **mTLS, retry, timeout, circuit breaker, observabilidade** sem código.

### Opções

| Opção | Quando usar |
|---|---|
| **Linkerd** | Simplicidade — mTLS automático, métricas RED, baixo overhead |
| **Istio** | Features extensas — políticas finas, multi-cluster, mas complexo |
| **Dapr** | Não é mesh tradicional — sidecar com **building blocks** (state, pub/sub, secrets) |
| **Cilium (eBPF)** | Network policies, futuro do mesh sem sidecar |

### Quando vale (e quando não)

✅ **Vale** quando: 20+ serviços, multi-time, requer mTLS organizacional, troubleshoot distribuído é dor recorrente.
❌ **NÃO vale** quando: < 10 serviços, time aprende K8s ainda, polly + OTel já cobrem.

> **Sênior:** mesh **não substitui** Polly. Mesh oferece resiliência **de rede**; Polly oferece resiliência **de domínio** (idempotency, business retry policies).

### Dapr (Alternativa Pragmática)

Em vez de aprender Envoy/Istio, Dapr é uma camada de **building blocks**:

```csharp
// Em vez de injetar IConnectionMultiplexer (Redis):
var state = await _daprClient.GetStateAsync<Order>("statestore", orderId);

// Pub/sub abstraído (broker é detalhe de deploy)
await _daprClient.PublishEventAsync("messagebus", "orders.created", evt);
```

Trade-off: introduz uma SDK; ganho: portabilidade entre brokers/stores.

---

## 9. Anti-Padrões Comuns

### ❌ Liveness probe = readiness probe
Causa pods reiniciando em loop quando dependência cai.
✅ Liveness = "processo respondendo"; Readiness = "deps OK".

### ❌ Sem `resources.limits.memory`
Um pod pode comer toda RAM e derrubar o node.
✅ Sempre defina memory limit.

### ❌ `latest` em image tag
Rollback impossível, deploy não reproduzível.
✅ Sempre tag versionada (`v1.4.2` ou SHA do commit).

### ❌ Secrets em ConfigMap (ou em Git)
✅ Secret K8s + Sealed Secrets / External Secrets / Key Vault CSI.

### ❌ HPA só por CPU
Workers Rabbit não estressam CPU — só fila cresce.
✅ KEDA com métrica de fila.

### ❌ Sem `topologySpreadConstraints`
3 réplicas no mesmo node = node cai = serviço cai.
✅ `topologySpreadConstraints` ou `podAntiAffinity` espalha entre nodes/zones.

---

## 10. 💼 Perguntas de Entrevista

**1. "Diferença entre Liveness e Readiness probe?"**
— Liveness: "ainda vivo?". Falha → **reinicia**. Readiness: "pronto pra tráfego?". Falha → **tira do Service**, sem reinício. Confundir é causa #1 de pods em CrashLoopBackOff quando dependência falha.

**2. "Como escalonar pods baseado em fila Rabbit, não só CPU?"**
— Usar **KEDA** (Kubernetes Event-Driven Autoscaling) — adiciona ScaledObject que polla a fila e expõe métrica para o HPA escalar com base nela.

**3. "Quando usar Service Mesh?"**
— Quando: muitos serviços (>20), exige mTLS organizacional, observabilidade distribuída precisa ser uniforme entre stacks. **Não vale** para começar — Polly + OTel + Ingress + NetworkPolicies cobrem 80% sem o overhead.

**4. "Como fazer deploy blue/green no K8s?"**
— (1) Manter dois Deployments (`orders-blue`, `orders-green`). (2) Service tem `selector` com label `version: blue`. (3) Subir `green`, validar. (4) Trocar selector para `version: green`. Rollback = trocar de volta. Alternativa moderna: **Argo Rollouts** ou **Flagger** automatiza com canary + análise de métricas.

**5. "Por que `requests` < `limits` para memória pode ser perigoso?"**
— Se requests=256Mi e limit=2Gi, K8s schedula assumindo 256Mi por pod → pode lotar o node. Quando todos os pods crescerem para perto do limit, node fica overcommitted, kernel OOM-mata pods aleatoriamente. Boa prática: **memory request = limit** (QoS Guaranteed).

**6. "ConfigMap mudou — pod precisa reiniciar?"**
— Por padrão **sim** se montado como env var (lido só no startup). Como volume, atualiza no FS mas app precisa reler. **Solução:** `Reloader` (Stakater) anota deployments com hash do ConfigMap; mudança força rolling restart.

**7. "Diferencie Helm e Kustomize."**
— **Helm:** templating com Go templates + valores hierárquicos + releases versionadas. Mais poderoso, curva maior. **Kustomize:** overlays declarativos sobre manifests base — sem templating, mais simples, built-in no `kubectl`. Use Helm para charts complexos com dependências; Kustomize para variações simples (dev/staging/prod).

---

## Checkpoint

✅ Manifests YAML para Orders API (Deployment + Service + HPA)
✅ Probes corretamente separados (live/ready/startup)
✅ Secrets via Sealed Secrets ou External Secrets
✅ HPA com KEDA escalando worker por fila Rabbit
✅ Helm chart unificado para os 4 microserviços
✅ ADR documentando "K8s ou ACA" para o projeto

➡️ **Próxima fase:** [`fase-12-oauth2-identityserver.md`](./fase-12-oauth2-identityserver.md)

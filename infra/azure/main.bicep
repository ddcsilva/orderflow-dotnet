// =====================================================================================
// OrderFlow — Azure Infrastructure (Bicep)
// =====================================================================================
// Provisiona Container Apps Environment, Log Analytics, Azure SQL, Redis, Service Bus
// e os 5 Container Apps (gateway, identity, catalog, orders, notification-worker).
//
// Deploy:
//   az deployment group create \
//     --resource-group <rg> \
//     --template-file infra/azure/main.bicep \
//     --parameters environment=staging imagePrefix=ghcr.io/<owner>/orderflow \
//                  sqlAdminPassword='<senha>'
// =====================================================================================

targetScope = 'resourceGroup'

@description('Nome do projeto (prefixo de recursos).')
param projectName string = 'orderflow'

@description('Ambiente de destino.')
@allowed(['staging', 'prod'])
param environment string = 'staging'

@description('Localização dos recursos.')
param location string = resourceGroup().location

@description('Senha do administrador do Azure SQL (mínimo 12 chars, complexa).')
@secure()
@minLength(12)
param sqlAdminPassword string

@description('Prefixo do container registry (ex: ghcr.io/seu-usuario/orderflow).')
param imagePrefix string

@description('Tag das imagens (ex: latest, sha do commit).')
param imageTag string = 'latest'

// ===== Variables =====
var suffix = '${projectName}-${environment}'
var uniq = uniqueString(resourceGroup().id, suffix)
var isProd = environment == 'prod'
var aspnetEnvironment = isProd ? 'Production' : 'Staging'

var logAnalyticsName = 'log-${suffix}'
var containerEnvName = 'cae-${suffix}'
var sqlServerName = 'sql-${suffix}-${uniq}'
var redisName = 'redis-${suffix}-${uniq}'
var serviceBusName = 'sb-${suffix}-${uniq}'

// ===== Log Analytics Workspace =====
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ===== Container Apps Environment =====
resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ===== Azure SQL =====
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'orderflowadmin'
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }

  resource fwAllowAzure 'firewallRules@2023-08-01-preview' = {
    name: 'AllowAzureServices'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }

  resource ordersDb 'databases@2023-08-01-preview' = {
    name: 'OrderFlow_Orders'
    location: location
    sku: {
      name: isProd ? 'S1' : 'Basic'
      tier: isProd ? 'Standard' : 'Basic'
    }
  }

  resource catalogDb 'databases@2023-08-01-preview' = {
    name: 'OrderFlow_Catalog'
    location: location
    sku: {
      name: isProd ? 'S1' : 'Basic'
      tier: isProd ? 'Standard' : 'Basic'
    }
  }

  resource identityDb 'databases@2023-08-01-preview' = {
    name: 'OrderFlow_Identity'
    location: location
    sku: {
      name: 'Basic'
      tier: 'Basic'
    }
  }
}

// ===== Azure Cache for Redis =====
resource redis 'Microsoft.Cache/redis@2023-08-01' = {
  name: redisName
  location: location
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

// ===== Azure Service Bus =====
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }

  resource rootRule 'AuthorizationRules@2022-10-01-preview' existing = {
    name: 'RootManageSharedAccessKey'
  }

  resource orderCreatedQueue 'queues@2022-10-01-preview' = {
    name: 'order-created'
    properties: {
      maxDeliveryCount: 10
      defaultMessageTimeToLive: 'P14D'
    }
  }
}

// ===== Helper closures (variables) =====
var ordersConnString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=OrderFlow_Orders;User ID=orderflowadmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
var catalogConnString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=OrderFlow_Catalog;User ID=orderflowadmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
var identityConnString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=OrderFlow_Identity;User ID=orderflowadmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
var redisConnString = '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
var serviceBusConnString = serviceBus::rootRule.listKeys().primaryConnectionString

// ===== Container Apps =====

// --- Identity API (internal) ---
resource identityApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${projectName}-identity-${environment}'
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      secrets: [
        { name: 'identity-conn', value: identityConnString }
      ]
    }
    template: {
      containers: [
        {
          name: 'identity-api'
          image: '${imagePrefix}-identity-api:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspnetEnvironment }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'ConnectionStrings__IdentityDb', secretRef: 'identity-conn' }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: 8080 }
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        minReplicas: isProd ? 1 : 0
        maxReplicas: isProd ? 5 : 2
      }
    }
  }
}

// --- Catalog API (internal) ---
resource catalogApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${projectName}-catalog-${environment}'
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: { external: false, targetPort: 8080, transport: 'http' }
      secrets: [
        { name: 'catalog-conn', value: catalogConnString }
        { name: 'redis-conn', value: redisConnString }
      ]
    }
    template: {
      containers: [
        {
          name: 'catalog-api'
          image: '${imagePrefix}-catalog-api:${imageTag}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspnetEnvironment }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'ConnectionStrings__CatalogDb', secretRef: 'catalog-conn' }
            { name: 'ConnectionStrings__Redis', secretRef: 'redis-conn' }
          ]
          probes: [
            { type: 'Liveness', httpGet: { path: '/health/live', port: 8080 }, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/health', port: 8080 }, periodSeconds: 15 }
          ]
        }
      ]
      scale: {
        minReplicas: isProd ? 1 : 0
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: { metadata: { concurrentRequests: '50' } }
          }
        ]
      }
    }
  }
}

// --- Orders API (internal) ---
resource ordersApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${projectName}-orders-${environment}'
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: { external: false, targetPort: 8080, transport: 'http' }
      secrets: [
        { name: 'orders-conn', value: ordersConnString }
        { name: 'redis-conn', value: redisConnString }
        { name: 'sb-conn', value: serviceBusConnString }
      ]
    }
    template: {
      containers: [
        {
          name: 'orders-api'
          image: '${imagePrefix}-orders-api:${imageTag}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspnetEnvironment }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'ConnectionStrings__OrdersDb', secretRef: 'orders-conn' }
            { name: 'ConnectionStrings__Redis', secretRef: 'redis-conn' }
            { name: 'MessageBus__ConnectionString', secretRef: 'sb-conn' }
          ]
          probes: [
            { type: 'Liveness', httpGet: { path: '/health/live', port: 8080 }, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/health', port: 8080 }, periodSeconds: 15 }
          ]
        }
      ]
      scale: {
        minReplicas: isProd ? 1 : 0
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: { metadata: { concurrentRequests: '50' } }
          }
        ]
      }
    }
  }
}

// --- Notification Worker (no ingress) ---
resource workerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${projectName}-worker-${environment}'
  location: location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      secrets: [
        { name: 'sb-conn', value: serviceBusConnString }
      ]
    }
    template: {
      containers: [
        {
          name: 'notification-worker'
          image: '${imagePrefix}-notification-worker:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'DOTNET_ENVIRONMENT', value: aspnetEnvironment }
            { name: 'MessageBus__ConnectionString', secretRef: 'sb-conn' }
          ]
        }
      ]
      scale: {
        minReplicas: isProd ? 1 : 0
        maxReplicas: 5
        rules: [
          {
            name: 'queue-scaling'
            custom: {
              type: 'azure-servicebus'
              metadata: {
                queueName: 'order-created'
                messageCount: '10'
              }
              auth: [
                {
                  secretRef: 'sb-conn'
                  triggerParameter: 'connection'
                }
              ]
            }
          }
        ]
      }
    }
  }
}

// --- API Gateway (external ingress) ---
resource gatewayApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${projectName}-gateway-${environment}'
  location: location
  dependsOn: [ identityApp, catalogApp, ordersApp ]
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'gateway'
          image: '${imagePrefix}-gateway:${imageTag}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspnetEnvironment }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            // Service discovery: container apps internos resolvem via DNS interno do environment
            { name: 'ReverseProxy__Clusters__identity-cluster__Destinations__identity-1__Address', value: 'http://${identityApp.properties.configuration.ingress.fqdn}/' }
            { name: 'ReverseProxy__Clusters__catalog-cluster__Destinations__catalog-1__Address', value: 'http://${catalogApp.properties.configuration.ingress.fqdn}/' }
            { name: 'ReverseProxy__Clusters__orders-cluster__Destinations__orders-1__Address', value: 'http://${ordersApp.properties.configuration.ingress.fqdn}/' }
          ]
          probes: [
            { type: 'Liveness', httpGet: { path: '/health/live', port: 8080 }, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/health', port: 8080 }, periodSeconds: 15 }
          ]
        }
      ]
      scale: {
        minReplicas: isProd ? 2 : 1
        maxReplicas: isProd ? 10 : 3
        rules: [
          {
            name: 'http-scaling'
            http: { metadata: { concurrentRequests: '100' } }
          }
        ]
      }
    }
  }
}

// ===== Outputs =====
output gatewayFqdn string = gatewayApp.properties.configuration.ingress.fqdn
output gatewayUrl string = 'https://${gatewayApp.properties.configuration.ingress.fqdn}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output redisHostName string = redis.properties.hostName
output serviceBusEndpoint string = serviceBus.properties.serviceBusEndpoint
output containerEnvironmentId string = containerEnv.id
output logAnalyticsId string = logAnalytics.id

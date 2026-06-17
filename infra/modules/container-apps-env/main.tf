# Container Apps Environment Module
# Provisions the Container Apps Environment, API Service, and Background Worker
# with health probes, scaling rules, and managed identity assignment.

# -----------------------------------------------------------------------------
# Container Apps Environment
# -----------------------------------------------------------------------------
resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${var.resource_prefix}-${var.environment_name}"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = var.log_analytics_workspace_id
  infrastructure_subnet_id   = null

  tags = var.tags
}

# -----------------------------------------------------------------------------
# API Service Container App
# -----------------------------------------------------------------------------
resource "azurerm_container_app" "api" {
  name                         = "ca-${var.resource_prefix}-api-${var.environment_name}"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Multiple"

  tags = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.managed_identity_id]
  }

  registry {
    server   = var.container_registry_login_url
    identity = var.managed_identity_id
  }

  template {
    min_replicas = var.min_api_replicas
    max_replicas = var.max_api_replicas

    container {
      name   = "api"
      image  = "${var.container_registry_login_url}/api:latest"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "app-insights-connection-string"
      }

      env {
        name  = "ServiceBus__Namespace"
        value = var.service_bus_namespace
      }

      # Liveness probe: /health/live, 5s timeout, 10s interval, 3 failure threshold
      liveness_probe {
        transport               = "HTTP"
        path                    = "/health/live"
        port                    = 8080
        timeout                 = 5
        interval_seconds        = 10
        failure_count_threshold = 3
      }

      # Readiness probe: /health/ready, 10s interval, 2 failure threshold
      readiness_probe {
        transport               = "HTTP"
        path                    = "/health/ready"
        port                    = 8080
        interval_seconds        = 10
        failure_count_threshold = 2
      }
    }

    # CPU utilization scaling rule (> 70%)
    custom_scale_rule {
      name             = "cpu-scaling"
      custom_rule_type = "cpu"
      metadata = {
        type  = "Utilization"
        value = "70"
      }
    }

    # Concurrent HTTP requests scaling rule (> 100)
    http_scale_rule {
      name                = "http-scaling"
      concurrent_requests = "100"
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  secret {
    name  = "app-insights-connection-string"
    value = var.app_insights_connection_string
  }
}

# -----------------------------------------------------------------------------
# Background Worker Container App
# -----------------------------------------------------------------------------
resource "azurerm_container_app" "worker" {
  name                         = "ca-${var.resource_prefix}-worker-${var.environment_name}"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  tags = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.managed_identity_id]
  }

  registry {
    server   = var.container_registry_login_url
    identity = var.managed_identity_id
  }

  template {
    min_replicas    = var.min_worker_replicas
    max_replicas    = var.max_worker_replicas
    revision_suffix = "stable"

    container {
      name   = "worker"
      image  = "${var.container_registry_login_url}/worker:latest"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "app-insights-connection-string"
      }

      env {
        name  = "ServiceBus__Namespace"
        value = var.service_bus_namespace
      }
    }

    # KEDA Service Bus queue trigger scaling rule (threshold: 10 messages)
    custom_scale_rule {
      name             = "servicebus-queue-scaling"
      custom_rule_type = "azure-servicebus"
      metadata = {
        queueName    = "work-items"
        namespace    = var.service_bus_namespace
        messageCount = "10"
      }
      authentication {
        secret_name       = "servicebus-connection"
        trigger_parameter = "connection"
      }
    }
  }

  # No ingress for worker - internal processing only

  secret {
    name  = "app-insights-connection-string"
    value = var.app_insights_connection_string
  }

  secret {
    name  = "servicebus-connection"
    value = var.service_bus_namespace
  }
}

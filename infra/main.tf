locals {
  resource_group_name = "rg-${var.resource_prefix}-${var.environment_name}"
  common_tags = {
    Environment = var.environment_name
    ManagedBy   = "Terraform"
    Project     = "azure-platform-service"
  }
}

data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "main" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.common_tags
}

module "managed_identity" {
  source = "./modules/managed-identity"

  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  resource_prefix          = var.resource_prefix
  environment_name         = var.environment_name
  tags                     = local.common_tags
  service_bus_namespace_id = module.service_bus.namespace_id
}

module "service_bus" {
  source = "./modules/service-bus"

  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  resource_prefix     = var.resource_prefix
  environment_name    = var.environment_name
  sku                 = var.service_bus_sku
}

module "container_registry" {
  source = "./modules/container-registry"

  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  resource_prefix     = var.resource_prefix
  environment_name    = var.environment_name
  sku                 = var.acr_sku
}

module "app_insights" {
  source = "./modules/app-insights"

  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  resource_prefix     = var.resource_prefix
  environment_name    = var.environment_name
  alert_email         = var.alert_email
}

module "key_vault" {
  source = "./modules/key-vault"

  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  resource_prefix            = var.resource_prefix
  environment_name           = var.environment_name
  managed_identity_object_id = module.managed_identity.principal_id
  pipeline_sp_object_id      = var.pipeline_sp_object_id
}

# Key Vault Secrets User role assignment for managed identity
# Defined here (not in the managed-identity module) to avoid circular dependency
# since the key_vault module depends on managed_identity.principal_id.
resource "azurerm_role_assignment" "managed_identity_key_vault_secrets_user" {
  scope                = module.key_vault.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.managed_identity.principal_id
}

module "container_apps_env" {
  source = "./modules/container-apps-env"

  resource_group_name            = azurerm_resource_group.main.name
  location                       = azurerm_resource_group.main.location
  resource_prefix                = var.resource_prefix
  environment_name               = var.environment_name
  managed_identity_id            = module.managed_identity.id
  log_analytics_workspace_id     = module.app_insights.workspace_id
  max_worker_replicas            = var.max_worker_replicas
  max_api_replicas               = var.max_api_replicas
  min_worker_replicas            = var.min_worker_replicas
  min_api_replicas               = var.min_api_replicas
  container_registry_login_url   = module.container_registry.login_server
  service_bus_namespace          = module.service_bus.namespace_endpoint
  app_insights_connection_string = module.app_insights.connection_string
  tags                           = local.common_tags
}

module "dashboard" {
  source = "./modules/dashboard"

  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  resource_prefix            = var.resource_prefix
  environment_name           = var.environment_name
  app_insights_id            = module.app_insights.app_insights_id
  log_analytics_workspace_id = module.app_insights.workspace_id
  tags                       = local.common_tags
}

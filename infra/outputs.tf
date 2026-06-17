output "acr_login_server" {
  description = "The login server URL of the Azure Container Registry"
  value       = module.container_registry.login_server
}

output "container_apps_fqdn" {
  description = "The FQDN of the Container Apps Environment default domain"
  value       = module.container_apps_env.default_domain
}

output "service_bus_namespace" {
  description = "The fully qualified namespace of the Azure Service Bus"
  value       = module.service_bus.namespace_endpoint
}

output "resource_group_name" {
  description = "The name of the resource group containing all resources"
  value       = azurerm_resource_group.main.name
}

output "managed_identity_client_id" {
  description = "The client ID of the user-assigned managed identity"
  value       = module.managed_identity.client_id
}

output "app_insights_connection_string" {
  description = "The connection string for Application Insights"
  value       = module.app_insights.connection_string
  sensitive   = true
}

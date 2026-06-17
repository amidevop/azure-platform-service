output "default_domain" {
  description = "The default domain of the Container Apps Environment"
  value       = azurerm_container_app_environment.main.default_domain
}

output "environment_id" {
  description = "The resource ID of the Container Apps Environment"
  value       = azurerm_container_app_environment.main.id
}

output "api_app_id" {
  description = "The resource ID of the API container app"
  value       = azurerm_container_app.api.id
}

output "api_app_fqdn" {
  description = "The FQDN of the API container app"
  value       = azurerm_container_app.api.ingress[0].fqdn
}

output "worker_app_id" {
  description = "The resource ID of the Worker container app"
  value       = azurerm_container_app.worker.id
}

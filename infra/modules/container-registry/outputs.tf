output "acr_id" {
  description = "The resource ID of the Container Registry"
  value       = azurerm_container_registry.acr.id
}

output "acr_name" {
  description = "The name of the Container Registry"
  value       = azurerm_container_registry.acr.name
}

output "login_server" {
  description = "The login server URL of the Container Registry"
  value       = azurerm_container_registry.acr.login_server
}

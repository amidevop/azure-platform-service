output "namespace_id" {
  description = "The ID of the Service Bus namespace"
  value       = azurerm_servicebus_namespace.this.id
}

output "namespace_name" {
  description = "The name of the Service Bus namespace"
  value       = azurerm_servicebus_namespace.this.name
}

output "queue_name" {
  description = "The name of the work items queue"
  value       = azurerm_servicebus_queue.work_items.name
}

output "primary_connection_string" {
  description = "The primary connection string for the Service Bus namespace"
  value       = azurerm_servicebus_namespace.this.default_primary_connection_string
  sensitive   = true
}

output "namespace_endpoint" {
  description = "The fully qualified namespace endpoint (e.g., <name>.servicebus.windows.net)"
  value       = "${azurerm_servicebus_namespace.this.name}.servicebus.windows.net"
}

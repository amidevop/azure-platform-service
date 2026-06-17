resource "azurerm_user_assigned_identity" "this" {
  name                = "id-${var.resource_prefix}-${var.environment_name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  tags                = var.tags
}

# Service Bus Data Sender role assignment (conditional)
resource "azurerm_role_assignment" "service_bus_sender" {
  count                = var.service_bus_namespace_id != null ? 1 : 0
  scope                = var.service_bus_namespace_id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azurerm_user_assigned_identity.this.principal_id
}

# Service Bus Data Receiver role assignment (conditional)
resource "azurerm_role_assignment" "service_bus_receiver" {
  count                = var.service_bus_namespace_id != null ? 1 : 0
  scope                = var.service_bus_namespace_id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azurerm_user_assigned_identity.this.principal_id
}

# Key Vault Secrets User role assignment (conditional)
resource "azurerm_role_assignment" "key_vault_secrets_user" {
  count                = var.key_vault_id != null ? 1 : 0
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.this.principal_id
}

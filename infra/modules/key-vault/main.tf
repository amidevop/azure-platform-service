data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "kv" {
  name                = "${var.resource_prefix}-kv-${var.environment_name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  purge_protection_enabled   = true
  soft_delete_retention_days = 7

  tags = {
    environment = var.environment_name
    managed_by  = "terraform"
  }
}

# Access policy for Managed Identity: read-only secret access (get, list)
resource "azurerm_key_vault_access_policy" "managed_identity" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = var.managed_identity_object_id

  secret_permissions = [
    "Get",
    "List",
  ]
}

# Access policy for pipeline service principal: minimum deployment permissions (get only)
resource "azurerm_key_vault_access_policy" "pipeline" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = var.pipeline_sp_object_id

  secret_permissions = [
    "Get",
  ]
}

resource "azurerm_container_registry" "acr" {
  # ACR names must be alphanumeric (no hyphens allowed), 5-50 characters
  name                = "${replace(var.resource_prefix, "-", "")}acr${var.environment_name}"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  admin_enabled       = false # Using managed identity for authentication

  tags = {
    environment = var.environment_name
    managed_by  = "terraform"
  }
}

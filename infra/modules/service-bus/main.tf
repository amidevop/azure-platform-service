resource "azurerm_servicebus_namespace" "this" {
  name                = "${var.resource_prefix}-sbns-${var.environment_name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku

  tags = {
    environment = var.environment_name
    managed_by  = "terraform"
  }
}

resource "azurerm_servicebus_queue" "work_items" {
  name         = "work-items"
  namespace_id = azurerm_servicebus_namespace.this.id

  # Enable dead-lettering when messages expire
  dead_lettering_on_message_expiration = true

  # Max delivery count before message goes to DLQ (initial attempt + 3 retries = 4)
  max_delivery_count = 4

  # Message TTL - messages expire after 14 days if not processed
  default_message_ttl = "P14D"

  # Lock duration for processing - 30 seconds (allows redelivery on worker crash)
  lock_duration = "PT30S"

  # DLQ retains messages for at least 7 days
  # In Azure Service Bus, dead-lettered messages inherit the queue's
  # default_message_ttl unless auto_delete_on_idle is set.
  # Setting auto_delete_on_idle to 7 days ensures DLQ messages are retained.
  auto_delete_on_idle = "P14D"

  # Enable partitioning for Premium SKU to improve throughput
  partitioning_enabled = var.sku == "Premium" ? true : false
}

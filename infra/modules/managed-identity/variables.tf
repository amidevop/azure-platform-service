variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
}

variable "resource_prefix" {
  description = "Naming prefix for resources"
  type        = string
}

variable "environment_name" {
  description = "Environment name (dev or prod)"
  type        = string
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}

variable "service_bus_namespace_id" {
  description = "The resource ID of the Service Bus namespace for role assignment scoping. If null, Service Bus role assignments are skipped."
  type        = string
  default     = null
}

variable "key_vault_id" {
  description = "The resource ID of the Key Vault for role assignment scoping. If null, Key Vault role assignment is skipped."
  type        = string
  default     = null
}

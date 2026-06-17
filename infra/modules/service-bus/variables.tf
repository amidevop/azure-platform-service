variable "resource_group_name" {
  description = "Name of the Azure resource group"
  type        = string
}

variable "location" {
  description = "Azure region for the Service Bus namespace"
  type        = string
}

variable "resource_prefix" {
  description = "Naming prefix for all resources"
  type        = string
}

variable "environment_name" {
  description = "Environment name (dev or prod)"
  type        = string

  validation {
    condition     = contains(["dev", "prod"], var.environment_name)
    error_message = "environment_name must be either 'dev' or 'prod'."
  }
}

variable "sku" {
  description = "Service Bus SKU tier (Basic for dev, Premium for prod)"
  type        = string
  default     = "Basic"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.sku)
    error_message = "sku must be one of: Basic, Standard, Premium."
  }
}

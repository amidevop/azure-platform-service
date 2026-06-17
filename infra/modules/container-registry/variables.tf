variable "resource_group_name" {
  description = "Name of the resource group where the Container Registry will be created"
  type        = string
}

variable "location" {
  description = "Azure region for the Container Registry"
  type        = string
}

variable "resource_prefix" {
  description = "Naming prefix for the registry (hyphens will be stripped for ACR naming compliance)"
  type        = string
}

variable "environment_name" {
  description = "Environment name (e.g., dev, prod) appended to ACR resource name"
  type        = string
}

variable "sku" {
  description = "SKU for the Container Registry (Basic for dev, Standard for prod)"
  type        = string
  default     = "Basic"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.sku)
    error_message = "SKU must be one of: Basic, Standard, Premium."
  }
}

variable "environment_name" {
  description = "The deployment environment name (dev or prod)"
  type        = string

  validation {
    condition     = contains(["dev", "prod"], var.environment_name)
    error_message = "environment_name must be either 'dev' or 'prod'."
  }
}

variable "resource_prefix" {
  description = "Naming prefix for all Azure resources"
  type        = string

  validation {
    condition     = length(var.resource_prefix) >= 2 && length(var.resource_prefix) <= 10
    error_message = "resource_prefix must be between 2 and 10 characters."
  }
}

variable "location" {
  description = "Azure region for resource deployment"
  type        = string
  default     = "eastus2"
}

variable "service_bus_sku" {
  description = "SKU tier for Azure Service Bus namespace (Basic for dev, Premium for prod)"
  type        = string
  default     = "Basic"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.service_bus_sku)
    error_message = "service_bus_sku must be Basic, Standard, or Premium."
  }
}

variable "acr_sku" {
  description = "SKU tier for Azure Container Registry (Basic for dev, Standard for prod)"
  type        = string
  default     = "Basic"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.acr_sku)
    error_message = "acr_sku must be Basic, Standard, or Premium."
  }
}

variable "max_worker_replicas" {
  description = "Maximum number of Background Worker replicas for scaling"
  type        = number
  default     = 3

  validation {
    condition     = var.max_worker_replicas >= 1 && var.max_worker_replicas <= 30
    error_message = "max_worker_replicas must be between 1 and 30."
  }
}

variable "max_api_replicas" {
  description = "Maximum number of API Service replicas for scaling"
  type        = number
  default     = 3

  validation {
    condition     = var.max_api_replicas >= 1 && var.max_api_replicas <= 30
    error_message = "max_api_replicas must be between 1 and 30."
  }
}

variable "min_worker_replicas" {
  description = "Minimum number of Background Worker replicas (0 allows scale-to-zero)"
  type        = number
  default     = 0

  validation {
    condition     = var.min_worker_replicas >= 0 && var.min_worker_replicas <= 10
    error_message = "min_worker_replicas must be between 0 and 10."
  }
}

variable "min_api_replicas" {
  description = "Minimum number of API Service replicas (must be at least 1)"
  type        = number
  default     = 1

  validation {
    condition     = var.min_api_replicas >= 1 && var.min_api_replicas <= 10
    error_message = "min_api_replicas must be between 1 and 10."
  }
}

variable "pipeline_sp_object_id" {
  description = "Object ID of the pipeline service principal for Key Vault deployment secret retrieval"
  type        = string
}

variable "alert_email" {
  description = "Email address to receive monitoring alert notifications"
  type        = string
  default     = "platform-alerts@example.com"
}

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

variable "managed_identity_id" {
  description = "Resource ID of the user-assigned managed identity"
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics workspace"
  type        = string
}

variable "max_worker_replicas" {
  description = "Maximum replicas for the Background Worker"
  type        = number
}

variable "max_api_replicas" {
  description = "Maximum replicas for the API Service"
  type        = number
}

variable "min_worker_replicas" {
  description = "Minimum replicas for the Background Worker"
  type        = number
}

variable "min_api_replicas" {
  description = "Minimum replicas for the API Service"
  type        = number
}

variable "container_registry_login_url" {
  description = "Login server URL of the container registry"
  type        = string
}

variable "service_bus_namespace" {
  description = "Fully qualified Service Bus namespace"
  type        = string
}

variable "app_insights_connection_string" {
  description = "Application Insights connection string"
  type        = string
  sensitive   = true
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}

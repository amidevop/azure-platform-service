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

variable "app_insights_id" {
  description = "Resource ID of the Application Insights instance"
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics workspace"
  type        = string
}

variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default     = {}
}

variable "resource_group_name" {
  description = "Name of the resource group where the Key Vault will be created"
  type        = string
}

variable "location" {
  description = "Azure region for the Key Vault"
  type        = string
}

variable "resource_prefix" {
  description = "Naming prefix for the Key Vault resource"
  type        = string
}

variable "environment_name" {
  description = "Environment name (e.g. dev, prod) used in Key Vault naming"
  type        = string
}

variable "managed_identity_object_id" {
  description = "Object ID of the Managed Identity that requires read-only secret access"
  type        = string
}

variable "pipeline_sp_object_id" {
  description = "Object ID of the pipeline service principal for minimum deployment secret retrieval"
  type        = string
}

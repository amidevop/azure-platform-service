environment_name = "prod"
resource_prefix  = "azplat"
location         = "eastus2"

# Service Bus - Premium tier for prod (sessions, partitioning, VNET)
service_bus_sku = "Premium"

# Container Registry - Standard tier for prod (geo-replication, webhooks)
acr_sku = "Standard"

# Scaling limits - higher for prod environment
max_worker_replicas = 10
max_api_replicas    = 10
min_worker_replicas = 0
min_api_replicas    = 1

# Pipeline service principal Object ID (replace with actual value)
pipeline_sp_object_id = "00000000-0000-0000-0000-000000000000"

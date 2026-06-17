environment_name = "dev"
resource_prefix  = "azplat"
location         = "eastus2"

# Service Bus - Basic tier for dev (cost-effective)
service_bus_sku = "Basic"

# Container Registry - Basic tier for dev
acr_sku = "Basic"

# Scaling limits - lower for dev environment
max_worker_replicas = 3
max_api_replicas    = 3
min_worker_replicas = 0
min_api_replicas    = 1

# Pipeline service principal Object ID (replace with actual value)
pipeline_sp_object_id = "00000000-0000-0000-0000-000000000000"

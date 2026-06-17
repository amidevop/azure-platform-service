###############################################################################
# Application Insights Module
# Provisions Log Analytics Workspace, Application Insights, Action Group, and
# a scheduled query alert for HTTP 5xx error rate exceeding 5% over a 5-minute
# window with a minimum of 20 requests evaluated.
# Requirements: 6.1, 9.3
###############################################################################

resource "azurerm_log_analytics_workspace" "this" {
  name                = "${var.resource_prefix}-law-${var.environment_name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = {
    environment = var.environment_name
  }
}

resource "azurerm_application_insights" "this" {
  name                = "${var.resource_prefix}-ai-${var.environment_name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  workspace_id        = azurerm_log_analytics_workspace.this.id
  application_type    = "web"

  tags = {
    environment = var.environment_name
  }
}

###############################################################################
# Action Group for Alert Notifications
###############################################################################

resource "azurerm_monitor_action_group" "alert_action_group" {
  name                = "${var.resource_prefix}-ag-5xx-${var.environment_name}"
  resource_group_name = var.resource_group_name
  short_name          = "5xxAlert"

  email_receiver {
    name          = "alert-email"
    email_address = var.alert_email
  }

  tags = {
    environment = var.environment_name
  }
}

###############################################################################
# Scheduled Query Alert for 5xx Error Rate
# Triggers when HTTP 5xx error rate exceeds 5% over a 5-minute window
# with a minimum of 20 total requests evaluated.
###############################################################################

resource "azurerm_monitor_scheduled_query_rules_alert_v2" "http_5xx_rate" {
  name                = "${var.resource_prefix}-alert-5xx-${var.environment_name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  description         = "Alert when HTTP 5xx error rate exceeds 5% over a 5-minute window (minimum 20 requests)"
  severity            = 2
  enabled             = true

  scopes                = [azurerm_application_insights.this.id]
  evaluation_frequency  = "PT1M"
  window_duration       = "PT5M"
  target_resource_types = ["Microsoft.Insights/components"]

  criteria {
    query = <<-QUERY
      requests
      | where timestamp >= ago(5m)
      | summarize
          totalRequests = count(),
          failedRequests = countif(toint(resultCode) >= 500 and toint(resultCode) < 600)
      | where totalRequests >= 20
      | extend errorRate = (toreal(failedRequests) / toreal(totalRequests)) * 100
      | where errorRate > 5
      | project errorRate, totalRequests, failedRequests
    QUERY

    time_aggregation_method = "Count"
    operator                = "GreaterThan"
    threshold               = 0

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 1
      number_of_evaluation_periods             = 1
    }
  }

  action {
    action_groups = [azurerm_monitor_action_group.alert_action_group.id]
  }

  tags = {
    environment = var.environment_name
  }
}

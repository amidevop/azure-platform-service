# Dashboard Module - Azure Monitor Workbook
# Provisions an Azure Monitor Workbook for visualizing
# API metrics, worker metrics, and Service Bus queue metrics.

resource "random_uuid" "workbook_id" {}

resource "azurerm_application_insights_workbook" "dashboard" {
  name                = random_uuid.workbook_id.result
  resource_group_name = var.resource_group_name
  location            = var.location
  display_name        = "${var.resource_prefix}-${var.environment_name}-dashboard"
  source_id           = var.app_insights_id

  data_json = jsonencode({
    version = "Notebook/1.0"
    items = [
      # =============================================
      # Group 1: API Service Metrics
      # =============================================
      {
        type = 1
        content = {
          json = "## API Service Metrics"
        }
        name = "api-metrics-header"
      },
      # Panel 1.1: API Request Rate (requests per minute)
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            requests
            | where timestamp > ago(1h)
            | summarize RequestCount = count() by bin(timestamp, 1m)
            | project timestamp, RequestsPerMinute = RequestCount
            | order by timestamp asc
          EOT
          size    = 0
          title   = "API Request Rate (requests/min)"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "api-request-rate"
      },
      # Panel 1.2: API Error Rate (5xx / total %)
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            requests
            | where timestamp > ago(1h)
            | summarize TotalRequests = count(), ErrorRequests = countif(toint(resultCode) >= 500) by bin(timestamp, 1m)
            | extend ErrorRatePercent = iff(TotalRequests == 0, 0.0, (toreal(ErrorRequests) / toreal(TotalRequests)) * 100.0)
            | project timestamp, ErrorRatePercent
            | order by timestamp asc
          EOT
          size    = 0
          title   = "API Error Rate (5xx %)"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "api-error-rate"
      },
      # Panel 1.3: Average Response Time (ms)
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            requests
            | where timestamp > ago(1h)
            | summarize AvgResponseTimeMs = avg(duration) by bin(timestamp, 1m)
            | project timestamp, AvgResponseTimeMs
            | order by timestamp asc
          EOT
          size    = 0
          title   = "Average Response Time (ms)"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "api-avg-response-time"
      },
      # =============================================
      # Group 2: Worker Metrics
      # =============================================
      {
        type = 1
        content = {
          json = "## Worker Metrics"
        }
        name = "worker-metrics-header"
      },
      # Panel 2.1: Worker Processing Rate (items processed per minute)
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            customMetrics
            | where timestamp > ago(1h)
            | where name == "worker.messages.processed"
            | summarize ProcessedCount = sum(valueCount) by bin(timestamp, 1m)
            | project timestamp, ProcessedPerMinute = ProcessedCount
            | order by timestamp asc
          EOT
          size    = 0
          title   = "Worker Processing Rate (items/min)"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "worker-processing-rate"
      },
      # Panel 2.2: Worker Failure Rate (failures / total %)
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            customMetrics
            | where timestamp > ago(1h)
            | where name in ("worker.messages.processed", "worker.messages.failed")
            | summarize
                TotalProcessed = sumif(valueCount, name == "worker.messages.processed"),
                TotalFailed = sumif(valueCount, name == "worker.messages.failed")
                by bin(timestamp, 1m)
            | extend FailureRatePercent = iff(TotalProcessed == 0, 0.0, (toreal(TotalFailed) / toreal(TotalProcessed)) * 100.0)
            | project timestamp, FailureRatePercent
            | order by timestamp asc
          EOT
          size    = 0
          title   = "Worker Failure Rate (%)"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "worker-failure-rate"
      },
      # Panel 2.3: Average Processing Duration (ms)
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            customMetrics
            | where timestamp > ago(1h)
            | where name == "worker.processing.duration"
            | summarize AvgDurationMs = avg(value) by bin(timestamp, 1m)
            | project timestamp, AvgDurationMs
            | order by timestamp asc
          EOT
          size    = 0
          title   = "Average Processing Duration (ms)"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "worker-avg-processing-duration"
      },
      # =============================================
      # Group 3: Queue Metrics
      # =============================================
      {
        type = 1
        content = {
          json = "## Queue Metrics"
        }
        name = "queue-metrics-header"
      },
      # Panel 3.1: Queue Depth (active messages)
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            customMetrics
            | where timestamp > ago(1h)
            | where name == "servicebus.queue.active_messages"
            | summarize QueueDepth = max(value) by bin(timestamp, 1m)
            | project timestamp, QueueDepth
            | order by timestamp asc
          EOT
          size    = 0
          title   = "Queue Depth (Active Messages)"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "queue-depth"
      },
      # Panel 3.2: Dead Letter Queue Count
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            customMetrics
            | where timestamp > ago(1h)
            | where name == "servicebus.queue.deadletter_messages"
            | summarize DLQCount = max(value) by bin(timestamp, 1m)
            | project timestamp, DLQCount
            | order by timestamp asc
          EOT
          size    = 0
          title   = "Dead Letter Queue Count"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "queue-dlq-count"
      },
      # Panel 3.3: Active Message Count
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-EOT
            customMetrics
            | where timestamp > ago(1h)
            | where name == "servicebus.queue.message_count"
            | summarize ActiveMessages = max(value) by bin(timestamp, 1m)
            | project timestamp, ActiveMessages
            | order by timestamp asc
          EOT
          size    = 0
          title   = "Active Message Count"
          timeContext = {
            durationMs = 3600000
          }
          queryType               = 0
          resourceType            = "microsoft.insights/components"
          crossComponentResources = [var.app_insights_id]
          visualization           = "timechart"
          noDataMessage           = "No data available"
        }
        name = "queue-active-message-count"
      }
    ]
    # Auto-refresh interval: 5 minutes (300000 ms)
    defaultResourceIds = [var.app_insights_id]
    styleSettings = {
      autoRefresh         = true
      autoRefreshInterval = "0:05:00"
    }
  })

  tags = var.tags
}

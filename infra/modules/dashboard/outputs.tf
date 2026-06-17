output "workbook_id" {
  description = "The resource ID of the Azure Monitor Workbook"
  value       = azurerm_application_insights_workbook.dashboard.id
}

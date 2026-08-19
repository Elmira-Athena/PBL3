output "budget_name" {
  description = "Tên budget theo dõi chi phí tháng"
  value       = aws_budgets_budget.monthly.name
}

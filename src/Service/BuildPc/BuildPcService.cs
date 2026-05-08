using OfficeOpenXml;
using OfficeOpenXml.Style;
using PBL3.Shared.DTOs.BuildPc;
using System.Drawing;

namespace PBL3.Service.BuildPc
{
    public class BuildPcService : IBuildPcService
    {
        public Task<byte[]> ExportToExcelAsync(ExportBuildPcRequest request)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Cấu hình PC");

            // ── Row 1: Title ─────────────────────────────────────────────
            ws.Cells[1, 1, 1, 9].Merge = true;
            ws.Cells[1, 1].Value = "CẤU HÌNH PC - HushStore";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Color.SetColor(Color.White);
            ws.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(192, 57, 43));
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Row(1).Height = 32;

            // ── Row 2: Export date ────────────────────────────────────────
            ws.Cells[2, 1, 2, 9].Merge = true;
            ws.Cells[2, 1].Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cells[2, 1].Style.Font.Italic = true;
            ws.Cells[2, 1].Style.Font.Color.SetColor(Color.FromArgb(127, 140, 141));
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // ── Row 3: Headers ────────────────────────────────────────────
            string[] headers = { "STT", "Linh kiện", "Sản phẩm", "Phiên bản", "SKU", "Bảo hành", "Đơn giá", "SL", "Thành tiền" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[3, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(44, 62, 80));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.White);
            }
            ws.Row(3).Height = 22;

            // ── Data rows ─────────────────────────────────────────────────
            decimal total = 0;
            int row = 4;
            foreach (var item in request.Items)
            {
                decimal lineTotal = item.UnitPrice * item.Quantity;
                total += lineTotal;

                bool isEven = (row % 2 == 0);
                var rowColor = isEven ? Color.FromArgb(245, 245, 245) : Color.White;

                ws.Cells[row, 1].Value = item.SlotIndex;
                ws.Cells[row, 2].Value = item.SlotName;
                ws.Cells[row, 3].Value = item.ProductName;
                ws.Cells[row, 4].Value = item.VariantName;
                ws.Cells[row, 5].Value = item.Sku;
                ws.Cells[row, 6].Value = item.WarrantyMonth > 0 ? $"{item.WarrantyMonth} tháng" : "—";
                ws.Cells[row, 7].Value = item.UnitPrice;
                ws.Cells[row, 7].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 8].Value = item.Quantity;
                ws.Cells[row, 9].Value = lineTotal;
                ws.Cells[row, 9].Style.Numberformat.Format = "#,##0";

                var dataRange = ws.Cells[row, 1, row, 9];
                dataRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                dataRange.Style.Fill.BackgroundColor.SetColor(rowColor);
                dataRange.Style.Border.BorderAround(ExcelBorderStyle.Hair, Color.FromArgb(189, 195, 199));

                ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[row, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                row++;
            }

            // ── Total row ─────────────────────────────────────────────────
            ws.Cells[row, 1, row, 8].Merge = true;
            ws.Cells[row, 1].Value = "Tổng chi phí dự tính";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[row, 9].Value = total;
            ws.Cells[row, 9].Style.Numberformat.Format = "#,##0";
            ws.Cells[row, 9].Style.Font.Bold = true;
            ws.Cells[row, 9].Style.Font.Color.SetColor(Color.FromArgb(192, 57, 43));
            ws.Cells[row, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            var totalRange = ws.Cells[row, 1, row, 9];
            totalRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            totalRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(235, 237, 239));
            totalRange.Style.Border.BorderAround(ExcelBorderStyle.Medium, Color.FromArgb(44, 62, 80));

            // ── Column widths ─────────────────────────────────────────────
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 20;
            ws.Column(3).Width = 40;
            ws.Column(4).Width = 25;
            ws.Column(5).Width = 14;
            ws.Column(6).Width = 12;
            ws.Column(7).Width = 14;
            ws.Column(8).Width = 6;
            ws.Column(9).Width = 16;

            return Task.FromResult(package.GetAsByteArray());
        }
    }
}

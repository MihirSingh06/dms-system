using backend;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.IO;
using OfficeOpenXml;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace backend.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Reviewer,Manager,Finance")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("spend-summary")]
    public async Task<IActionResult> SpendSummary(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var query = _context.Documents
            .Where(d => d.Status == DocumentStatus.Approved);

        if (startDate.HasValue)
            query = query.Where(d => d.InvoiceDate >= startDate);

        if (endDate.HasValue)
            query = query.Where(d => d.InvoiceDate <= endDate);

        var totalAmount = await query.SumAsync(d => d.Amount ?? 0);
        var totalVat = await query.SumAsync(d => d.VatAmount ?? 0);
        var count = await query.CountAsync();

        return Ok(new
        {
            DocumentCount = count,
            TotalAmount = totalAmount,
            TotalVat = totalVat
        });
    }

   [HttpGet("vendor-analysis")]
public async Task<IActionResult> VendorAnalysis(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate)
{
    var query = _context.Documents
        .Where(d => d.Status == DocumentStatus.Approved);

    if (startDate.HasValue)
        query = query.Where(d => d.InvoiceDate.HasValue && d.InvoiceDate.Value >= startDate.Value);

    if (endDate.HasValue)
        query = query.Where(d => d.InvoiceDate.HasValue && d.InvoiceDate.Value <= endDate.Value);

    var documents = await query
        .Where(d => !string.IsNullOrEmpty(d.Vendor))
        .ToListAsync(); // ← materialize first

    var result = documents
        .GroupBy(d => d.Vendor)
        .Select(g => new
        {
            Vendor = g.Key,
            DocumentCount = g.Count(),
            TotalAmount = g.Sum(d => d.Amount ?? 0),
            TotalVat = g.Sum(d => d.VatAmount ?? 0)
        })
        .OrderByDescending(x => x.TotalAmount)
        .ToList();

    return Ok(result);
}


 // =========================
    // VAT REPORT
    // =========================
    [HttpGet("vat-report")]
    public async Task<IActionResult> GetVatReport()
    {
        var approved = await _context.Documents
            .Where(d => d.Status == DocumentStatus.Approved)
            .ToListAsync();

        var totalGross = approved.Sum(d => d.Amount ?? 0);
        var totalVat = approved.Sum(d => d.VatAmount ?? 0);
        var totalNet = totalGross - totalVat;

        return Ok(new
        {
            TotalNet = totalNet,
            TotalVat = totalVat,
            TotalGross = totalGross
        });
    }

[HttpGet("status-summary")]
public async Task<IActionResult> StatusSummary()
{
    var documents = await _context.Documents.ToListAsync();

    var result = new
    {
        PendingReviewer = documents.Count(d => d.Status == DocumentStatus.PendingReviewer),
        PendingManager = documents.Count(d => d.Status == DocumentStatus.PendingManager),
        PendingFinance = documents.Count(d => d.Status == DocumentStatus.PendingFinance),
        Approved = documents.Count(d => d.Status == DocumentStatus.Approved),
        Rejected = documents.Count(d => d.Status == DocumentStatus.Rejected),
        TotalDocuments = documents.Count
    };

    return Ok(result);
}

[HttpGet("vendor-analysis/export")]
public async Task<IActionResult> ExportVendorAnalysis(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate)
{
    var query = _context.Documents
        .Where(d => d.Status == DocumentStatus.Approved);

    if (startDate.HasValue)
        query = query.Where(d => d.InvoiceDate.HasValue && d.InvoiceDate.Value >= startDate.Value);

    if (endDate.HasValue)
        query = query.Where(d => d.InvoiceDate.HasValue && d.InvoiceDate.Value <= endDate.Value);

    var documents = await query
        .Where(d => !string.IsNullOrEmpty(d.Vendor))
        .ToListAsync();

    var grouped = documents
        .GroupBy(d => d.Vendor)
        .Select(g => new
        {
            Vendor = g.Key,
            DocumentCount = g.Count(),
            TotalAmount = g.Sum(d => d.Amount ?? 0),
            TotalVat = g.Sum(d => d.VatAmount ?? 0)
        })
        .OrderByDescending(x => x.TotalAmount)
        .ToList();

    using var workbook = new XLWorkbook();
    var worksheet = workbook.Worksheets.Add("Vendor Analysis");

    // Headers
    worksheet.Cell(1, 1).Value = "Vendor";
    worksheet.Cell(1, 2).Value = "Document Count";
    worksheet.Cell(1, 3).Value = "Total Amount";
    worksheet.Cell(1, 4).Value = "Total VAT";

    for (int i = 0; i < grouped.Count; i++)
    {
        worksheet.Cell(i + 2, 1).Value = grouped[i].Vendor;
        worksheet.Cell(i + 2, 2).Value = grouped[i].DocumentCount;
        worksheet.Cell(i + 2, 3).Value = grouped[i].TotalAmount;
        worksheet.Cell(i + 2, 4).Value = grouped[i].TotalVat;
    }

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    stream.Position = 0;

    return File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "VendorAnalysis.xlsx");
}

[HttpGet("filtered")]
public async Task<IActionResult> FilteredReport(
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate,
    [FromQuery] string? vendor,
    [FromQuery] string? status,
    [FromQuery] decimal? minAmount,
    [FromQuery] decimal? maxAmount)
{
    var query = _context.Documents.AsQueryable();

    if (startDate.HasValue)
        query = query.Where(d => d.InvoiceDate.HasValue && d.InvoiceDate.Value >= startDate.Value);

    if (endDate.HasValue)
        query = query.Where(d => d.InvoiceDate.HasValue && d.InvoiceDate.Value <= endDate.Value);

    if (!string.IsNullOrWhiteSpace(vendor))
        query = query.Where(d => d.Vendor != null && d.Vendor.Contains(vendor));

    if (!string.IsNullOrWhiteSpace(status) &&
        Enum.TryParse<DocumentStatus>(status, true, out var parsedStatus))
        query = query.Where(d => d.Status == parsedStatus);

    if (minAmount.HasValue)
        query = query.Where(d => d.Amount.HasValue && d.Amount.Value >= minAmount.Value);

    if (maxAmount.HasValue)
        query = query.Where(d => d.Amount.HasValue && d.Amount.Value <= maxAmount.Value);

    var documents = await query.ToListAsync();

    var totalAmount = documents.Sum(d => d.Amount ?? 0);
    var totalVat = documents.Sum(d => d.VatAmount ?? 0);

    return Ok(new
    {
        Count = documents.Count,
        TotalAmount = totalAmount,
        TotalVat = totalVat,
        Documents = documents
    });
}

[HttpGet("ai-insights")]
public async Task<IActionResult> AiInsights()
{
    var approvedDocs = await _context.Documents
        .Where(d => d.Status == DocumentStatus.Approved && d.Amount.HasValue)
        .ToListAsync();

    if (!approvedDocs.Any())
        return Ok(new { Message = "No approved documents yet." });

    var totalSpend = approvedDocs.Sum(d => d.Amount ?? 0);
    var totalVat = approvedDocs.Sum(d => d.VatAmount ?? 0);

    var averageSpend = approvedDocs.Average(d => d.Amount ?? 0);

    var topVendorGroup = approvedDocs
        .Where(d => !string.IsNullOrEmpty(d.Vendor))
        .GroupBy(d => d.Vendor)
        .OrderByDescending(g => g.Sum(x => x.Amount ?? 0))
        .FirstOrDefault();

    var highestInvoice = approvedDocs
        .OrderByDescending(d => d.Amount ?? 0)
        .FirstOrDefault();

    var insights = new List<string>();

    insights.Add($"Total approved spend: R {totalSpend:N2}");
    insights.Add($"Total VAT paid: R {totalVat:N2}");
    insights.Add($"Average invoice value: R {averageSpend:N2}");

    if (topVendorGroup != null)
    {
        var vendorTotal = topVendorGroup.Sum(x => x.Amount ?? 0);
        var concentrationPercent = (vendorTotal / totalSpend) * 100;

        insights.Add($"Top vendor: {topVendorGroup.Key} with R {vendorTotal:N2} in spend.");

        if (concentrationPercent > 50)
        {
            insights.Add($"⚠️ Vendor concentration risk: {topVendorGroup.Key} represents {concentrationPercent:N1}% of total spend.");
        }
    }

    if (highestInvoice != null)
    {
        insights.Add($"Highest single invoice: {highestInvoice.Vendor} - R {highestInvoice.Amount:N2}");

        if (highestInvoice.Amount > averageSpend * 2)
        {
            insights.Add("⚠️ Anomaly detected: Highest invoice significantly exceeds average invoice value.");
        }
    }

    return Ok(insights);
}

[HttpGet("export-excel")]
public async Task<IActionResult> ExportExcel()
{
    var documents = await _context.Documents.ToListAsync();

    using var package = new ExcelPackage();
    var worksheet = package.Workbook.Worksheets.Add("Report");

    worksheet.Cells[1, 1].Value = "Vendor";
    worksheet.Cells[1, 2].Value = "Invoice";
    worksheet.Cells[1, 3].Value = "Amount";
    worksheet.Cells[1, 4].Value = "Status";

    for (int i = 0; i < documents.Count; i++)
    {
        worksheet.Cells[i + 2, 1].Value = documents[i].Vendor;
        worksheet.Cells[i + 2, 2].Value = documents[i].InvoiceNumber;
        worksheet.Cells[i + 2, 3].Value = documents[i].Amount;
        worksheet.Cells[i + 2, 4].Value = documents[i].Status.ToString();
    }

    var bytes = package.GetAsByteArray();
    return File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Report.xlsx");
}

[HttpGet("export-pdf")]
[Authorize]
public async Task<IActionResult> ExportPdf()
{
    var documents = await _context.Documents.ToListAsync();

    using var stream = new MemoryStream();

    using (var writer = new PdfWriter(stream))
    {
        var pdf = new PdfDocument(writer);
        var document = new iText.Layout.Document(pdf);

        document.Add(new Paragraph("DMS Report"));
        document.Add(new Paragraph(" "));

        foreach (var doc in documents)
        {
            document.Add(new Paragraph(
                $"{doc.Vendor} | {doc.InvoiceNumber} | {doc.Amount} | {doc.Status}"
            ));
        }

        document.Close();
    }

    var bytes = stream.ToArray();

    return File(bytes, "application/pdf", "Report.pdf");
}
}
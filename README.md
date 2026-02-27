# Document Management System (DMS)

## Overview

A secure web-based Document Management System built with ASP.NET Core and React.

The system supports invoice and credit note uploads, automated OCR data extraction, duplicate detection, multi-stage approval workflow, reporting, Excel export, and AI-driven spend insights.

---

## Features

- JWT Authentication
- Role-based 3-step Approval Workflow (Reviewer → Manager → Finance)
- OCR Invoice Extraction (auto-fills invoice fields)
- Duplicate Detection:
  - File hash check
  - Vendor + Invoice Number validation
  - Vendor + Amount validation
- Status Dashboard with KPI cards
- Spend Summary Chart
- AI Insights Engine
- Report Filtering (date, vendor, status, amount range)
- Excel Export (Vendor Analysis)

---

## Tech Stack

**Backend**
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- ClosedXML (Excel export)

**Frontend**
- React (Vite)
- Fetch API
- Custom dashboard styling

---

## How to Run

### Backend


# Document Management System (DMS)

## Overview

A secure full-stack Document Management System built using ASP.NET Core and React.

The system allows users to upload invoices and credit notes, automatically extract invoice data using OCR, route documents through a 3-step approval workflow, detect duplicates, generate reports, export to Excel, and provide AI-driven spend insights.

---

## Features

### Authentication & Security
- JWT-based authentication
- Role-based access control (Reviewer → Manager → Finance)
- Protected API endpoints

### Document Processing
- Invoice & Credit Note upload
- Automatic OCR extraction
- Auto-fill invoice fields
- File hash duplicate detection
- Vendor + Invoice number duplicate detection
- Vendor + Amount duplicate detection

### Approval Workflow
- 3-stage approval process:
  - Reviewer
  - Manager
  - Finance
- Rejection with reason
- Approval history tracking

### Reporting & Insights
- Status summary dashboard
- Spend overview chart
- Vendor analysis
- Date & amount filtering
- Excel export (Vendor Analysis)
- AI-generated financial insights

---

## Tech Stack

### Backend
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- ClosedXML (Excel export)

### Frontend
- React (Vite)
- Fetch API
- Custom dashboard UI
- OCR integration

---

## How to Run

### Backend

```bash
cd backend
dotnet restore
dotnet run
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

---

## Architecture

- Clean separation of Controllers, Services, and Data Models
- Server-side role validation
- Workflow state management using enums
- Aggregated reporting queries
- Modular AI insights service

---

## Author

Mihir Singh
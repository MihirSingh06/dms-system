const API_BASE = "https://ideal-xylophone-5p6g9x779qjhjgj-5078.app.github.dev";

/* =========================
   AUTH
========================= */

export async function login(username, password) {
  const response = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) throw new Error("Invalid credentials");
  return response.json();
}

/* =========================
   DOCUMENTS
========================= */

export async function uploadDocument(formData) {
  const token = localStorage.getItem("token");

  const response = await fetch(`${API_BASE}/api/documents/upload`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
    body: formData,
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText);
  }

  return response.json();
}

export async function getDocuments() {
  const token = localStorage.getItem("token");

  const response = await fetch(`${API_BASE}/api/documents`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!response.ok) throw new Error("Failed to fetch documents");
  return response.json();
}

export async function approveDocument(id) {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/documents/${id}/approve`,
    {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText);
  }

  return true;
}

export async function rejectDocument(id, reason) {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/documents/${id}/reject`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(reason),
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText);
  }

  return true;
}

export async function getDocumentHistory(id) {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/documents/${id}/history`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) {
    throw new Error("Failed to fetch document history");
  }

  return response.json();
}

/* =========================
   AI EXTRACTION (FIXED)
========================= */

export async function extractDocument(file) {
  const token = localStorage.getItem("token");

  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(
    `${API_BASE}/api/documents/extract`,
    {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
      body: formData,
    }
  );

  if (!response.ok) {
    throw new Error("Extraction failed");
  }

  return response.json();
}

/* =========================
   REPORTS
========================= */

export async function getStatusSummary() {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/reports/status-summary`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to fetch status summary");
  return response.json();
}

export async function getSpendSummary() {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/reports/spend-summary`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to fetch spend summary");
  return response.json();
}

export async function getFilteredReport(filters) {
  const token = localStorage.getItem("token");

  const params = new URLSearchParams();

  if (filters.startDate) params.append("startDate", filters.startDate);
  if (filters.endDate) params.append("endDate", filters.endDate);
  if (filters.vendor) params.append("vendor", filters.vendor);
  if (filters.status) params.append("status", filters.status);
  if (filters.minAmount) params.append("minAmount", filters.minAmount);
  if (filters.maxAmount) params.append("maxAmount", filters.maxAmount);

  const response = await fetch(
    `${API_BASE}/api/reports/filtered?${params.toString()}`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to fetch report");
  return response.json();
}

export async function getAiInsights() {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/reports/ai-insights`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to fetch insights");
  return response.json();
}

export async function getVendorAnalysis() {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/reports/vendor-analysis`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to fetch vendor analysis");
  return response.json();
}

export async function getVatReport() {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/reports/vat-report`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to fetch VAT report");
  return response.json();
}

/* =========================
   EXPORTS
========================= */

export async function exportExcel() {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/reports/export-excel`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to export Excel");

  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);

  const a = document.createElement("a");
  a.href = url;
  a.download = "Report.xlsx";
  a.click();
}

export async function exportPdf() {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/reports/export-pdf`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to export PDF");

  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);

  const a = document.createElement("a");
  a.href = url;
  a.download = "Report.pdf";
  a.click();
}

export async function downloadDocumentFile(id, fileName) {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/documents/${id}/file`,
    {
      headers: { Authorization: `Bearer ${token}` },
    }
  );

  if (!response.ok) throw new Error("Failed to download file");

  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);

  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  a.click();
}
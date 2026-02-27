const API_BASE =
  import.meta.env.VITE_API_URL ||
  "https://dms-system-iixe.onrender.com";

export async function login(username, password) {
  const response = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) {
    throw new Error("Invalid credentials");
  }

  return response.json();
}

export async function getStatusSummary() {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/reports/status-summary`,
    {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    }
  );

  if (!response.ok) {
    throw new Error("Failed to fetch status summary");
  }

  return response.json();
}

export async function uploadDocument(formData) {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/documents/upload`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      body: formData,
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText);
  }

  return response.json();
}

export async function getDocuments() {
  const token = localStorage.getItem("token");

  const response = await fetch(`${API_BASE}/api/documents`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    throw new Error("Failed to fetch documents");
  }

  return response.json();
}

export async function approveDocument(id) {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/documents/${id}/approve`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText);
  }

  return response.json();
}

export async function rejectDocument(id) {
  const token = localStorage.getItem("token");

  const response = await fetch(
    `${API_BASE}/api/documents/${id}/reject`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText);
  }

  return response.json();

}

  export async function getSpendSummary() {
  const token = localStorage.getItem("token");

  const response = await fetch(`${API_BASE}/api/reports/spend-summary`, {
    headers: {
      Authorization: `Bearer ${token}`
    }
  });

  if (!response.ok) {
    throw new Error("Failed to fetch spend summary");
  }

  return response.json();
}

export async function extractOcr(file) {
  const token = localStorage.getItem("token");

  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${API_BASE}/api/ocr/extract`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`
    },
    body: formData
  });

  if (!response.ok) {
    throw new Error("OCR extraction failed");
  }

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
      headers: {
        Authorization: `Bearer ${token}`,
      },
    }
  );

  if (!response.ok) {
    throw new Error("Failed to fetch report");
  }

  return response.json();
}
export async function getAiInsights() {
  const token = localStorage.getItem("token");

  const response = await fetch(`${API_BASE}/api/reports/ai-insights`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    throw new Error("Failed to fetch insights");
  }

  return response.json();
}
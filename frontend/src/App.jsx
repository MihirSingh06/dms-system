import { useState, useEffect } from "react";
import SpendChart from "./Components/SpendChart";
import {
  login,
  getStatusSummary,
  uploadDocument,
  getDocuments,
  approveDocument,
  rejectDocument,
  getSpendSummary,
  extractOcr,
  getFilteredReport,
  getAiInsights,
  getVendorAnalysis,
  getVatReport,
  exportExcel,
  exportPdf,
  getDocumentHistory,
} from "./api";
import "./App.css";


function App() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState("");
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  const [summary, setSummary] = useState(null);
  const [spendData, setSpendData] = useState(null);
  const [documents, setDocuments] = useState([]);

  const [vendor, setVendor] = useState("");
  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [invoiceDate, setInvoiceDate] = useState("");
  const [amount, setAmount] = useState("");
  const [vatAmount, setVatAmount] = useState("");
  const [file, setFile] = useState(null);
  const [uploadMessage, setUploadMessage] = useState("");

  const [userRole, setUserRole] = useState(null);

  const [reportData, setReportData] = useState(null);
  const [vendorAnalysis, setVendorAnalysis] = useState([]);
  const [vatReport, setVatReport] = useState(null);
  const [aiInsights, setAiInsights] = useState([]);

  const [filterStartDate, setFilterStartDate] = useState("");
  const [filterEndDate, setFilterEndDate] = useState("");
  const [filterVendor, setFilterVendor] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [filterMinAmount, setFilterMinAmount] = useState("");
  const [filterMaxAmount, setFilterMaxAmount] = useState("");
  const [showRejectModal, setShowRejectModal] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [rejectDocId, setRejectDocId] = useState(null);
  const [history, setHistory] = useState({});

  

  // =========================
  // LOAD DASHBOARD DATA
  // =========================
  useEffect(() => {
    if (isLoggedIn) {
      getStatusSummary().then(setSummary);
      getDocuments().then(setDocuments);
      getSpendSummary().then(setSpendData);
      getAiInsights().then((data) =>
        setAiInsights(Array.isArray(data) ? data : [])
      );
    }
  }, [isLoggedIn]);

  // =========================
  // LOGIN
  // =========================
  const handleLogin = async () => {
    try {
      const data = await login(username, password);
      console.log("LOGIN RESPONSE:", data); 
      localStorage.setItem("token", data.token);

      const payload = JSON.parse(atob(data.token.split(".")[1]));
      setUserRole(
        payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]
      );
      console.log("TOKEN PAYLOAD:", payload);

      setIsLoggedIn(true);
      setMessage("");
    } catch {
      setMessage("Login failed.");
    }
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    setIsLoggedIn(false);
  };

  // =========================
  // FILTER REPORT
  // =========================
  const handleFilter = async () => {
    const data = await getFilteredReport({
      startDate: filterStartDate,
      endDate: filterEndDate,
      vendor: filterVendor,
      status: filterStatus,
      minAmount: filterMinAmount,
      maxAmount: filterMaxAmount,
    });

    setReportData(data);
  };

  // =========================
  // UPLOAD DOCUMENT
  // =========================
  const handleUpload = async () => {
    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("vendor", vendor);
      formData.append("invoiceNumber", invoiceNumber);
      formData.append("invoiceDate", invoiceDate);
      formData.append("amount", amount);
      formData.append("vatAmount", vatAmount);

      await uploadDocument(formData);

      setUploadMessage("Upload successful!");
      setVendor("");
      setInvoiceNumber("");
      setInvoiceDate("");
      setAmount("");
      setVatAmount("");
      setFile(null);

      getDocuments().then(setDocuments);
      getStatusSummary().then(setSummary);
      getSpendSummary().then(setSpendData);
    } catch (error) {
      setUploadMessage(error.message);
    }
  };

  // =========================
  // APPROVAL
  // =========================
  const handleApprove = async (id) => {
    await approveDocument(id);
    getDocuments().then(setDocuments);
    getStatusSummary().then(setSummary);
  };

const submitRejection = async () => {
  if (!rejectReason.trim()) {
    alert("Rejection reason is required.");
    return;
  }

  try {
    await rejectDocument(rejectDocId, rejectReason);

    setShowRejectModal(false);
    setRejectReason("");
    setRejectDocId(null);

    getDocuments().then(setDocuments);
    getStatusSummary().then(setSummary);
  } catch (error) {
    alert(error.message);
  }
};

const loadHistory = async (docId) => {
  try {
    const data = await getDocumentHistory(docId);

    setHistory((prev) => ({
      ...prev,
      [docId]: data
    }));
  } catch (error) {
    alert(error.message);
  }
};

  // =========================
  // LOGIN SCREEN
  // =========================
  if (!isLoggedIn) {
    return (
      <div style={{ padding: 40 }}>
        <h1>DMS Login</h1>
        <input
          placeholder="Username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
        <br /><br />
        <input
          type="password"
          placeholder="Password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <br /><br />
        <button onClick={handleLogin}>Login</button>
        <div style={{ color: "red" }}>{message}</div>
      </div>
    );
  }

  // =========================
  // DASHBOARD
  // =========================
  return (
    <div className="dashboard-container">

      <div className="topbar">
  <div className="topbar-title">
    Document Management System
  </div>

  <div className="topbar-right">
    <div className="role-badge">
      {userRole}
    </div>

    <button className="logout-btn" onClick={handleLogout}>
      Logout
    </button>
  </div>
</div>

      <h2>Status Summary</h2>
{summary && (
  <div className="summary-grid">
    <div className="summary-card status-pending">
      <div className="summary-title">Pending Reviewer</div>
      <div className="summary-value">{summary.pendingReviewer}</div>
    </div>

    <div className="summary-card status-pending">
      <div className="summary-title">Pending Manager</div>
      <div className="summary-value">{summary.pendingManager}</div>
    </div>

    <div className="summary-card status-pending">
      <div className="summary-title">Pending Finance</div>
      <div className="summary-value">{summary.pendingFinance}</div>
    </div>

    <div className="summary-card status-approved">
      <div className="summary-title">Approved</div>
      <div className="summary-value">{summary.approved}</div>
    </div>

    <div className="summary-card status-rejected">
      <div className="summary-title">Rejected</div>
      <div className="summary-value">{summary.rejected}</div>
    </div>

    <div className="summary-card status-total">
      <div className="summary-title">Total Documents</div>
      <div className="summary-value">{summary.totalDocuments}</div>
    </div>
  </div>
)}

      <h2>Spend Overview</h2>
      <SpendChart data={spendData} />

      <h2>AI Insights</h2>
      {aiInsights.length > 0 ? (
        <ul>
          {aiInsights.map((i, idx) => (
            <li key={idx}>{i}</li>
          ))}
        </ul>
      ) : (
        <p>No insights available.</p>
      )}

<h2>Vendor Analysis</h2>
<button
  className="logout-btn"
  onClick={async () => {
    const data = await getVendorAnalysis();
    setVendorAnalysis(data);
  }}
>
  Load Vendor Analysis
</button>

{vendorAnalysis.length > 0 && (
  <table style={{ marginTop: 15 }}>
    <thead>
      <tr>
        <th>Vendor</th>
        <th>Total Amount</th>
      </tr>
    </thead>
    <tbody>
      {vendorAnalysis.map((v, index) => (
        <tr key={index}>
          <td>{v.vendor}</td>
          <td>{v.totalAmount}</td>
        </tr>
      ))}
    </tbody>
  </table>
)}

<hr />

<h2>VAT Report</h2>
<button
  className="logout-btn"
  onClick={async () => {
    const data = await getVatReport();
    setVatReport(data);
  }}
>
  Load VAT Report
</button>

{vatReport && (
  <div style={{ marginTop: 15 }}>
    <p><strong>Total Net:</strong> {vatReport.totalNet}</p>
    <p><strong>Total VAT:</strong> {vatReport.totalVat}</p>
    <p><strong>Total Gross:</strong> {vatReport.totalGross}</p>
  </div>
)}

<hr />

<h2>Export Reports</h2>
<button className="logout-btn" onClick={exportExcel}>
  Export Excel
</button>
<button className="logout-btn" onClick={exportPdf}>
  Export PDF
</button>

      <h2>Upload Document</h2>

      <input type="file" onChange={async (e) => {
        const selectedFile = e.target.files?.[0];
        if (!selectedFile) return;
        setFile(selectedFile);

        const data = await extractOcr(selectedFile);

        setVendor(data.vendor || "");
        setInvoiceNumber(data.invoiceNumber || "");

        if (data.invoiceDate) {
          const d = new Date(data.invoiceDate);
          if (!isNaN(d)) {
            setInvoiceDate(d.toISOString().split("T")[0]);
          }
        }

        setAmount(data.amount || "");
        setVatAmount(data.vatAmount || "");
      }} />

      <input type="date" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} />
      <input placeholder="Vendor" value={vendor} onChange={(e) => setVendor(e.target.value)} />
      <input placeholder="Invoice #" value={invoiceNumber} onChange={(e) => setInvoiceNumber(e.target.value)} />
      <input placeholder="Amount" value={amount} onChange={(e) => setAmount(e.target.value)} />
      <input placeholder="VAT" value={vatAmount} onChange={(e) => setVatAmount(e.target.value)} />

      <button onClick={handleUpload}>Upload</button>
      <div>{uploadMessage}</div>

<h2>Documents</h2>

{documents.length === 0 ? (
  <p>No documents found.</p>
) : (
  <table>
    <thead>
      <tr>
        <th>ID</th>
        <th>Vendor</th>
        <th>Invoice</th>
        <th>Amount</th>
        <th>Status</th>
        <th>Actions</th>
        <th>Rejection Reason</th>
      </tr>
    </thead>
<tbody>
  {documents.map((doc) => (
    <>
      <tr key={doc.id}>
        <td>{doc.id}</td>
        <td>{doc.vendor}</td>
        <td>{doc.invoiceNumber}</td>
        <td>{doc.amount}</td>
        <td>{doc.status}</td>
        <td>
          {(
            (doc.status === "PendingReviewer" && userRole === "Reviewer") ||
            (doc.status === "PendingManager" && userRole === "Manager") ||
            (doc.status === "PendingFinance" && userRole === "Finance")
          ) && (
            <>
              <button onClick={() => handleApprove(doc.id)}>
                Approve
              </button>

              <button
                onClick={() => {
                  setRejectDocId(doc.id);
                  setShowRejectModal(true);
                }}
              >
                Reject
              </button>
            </>
          )}

          <button
            style={{ marginLeft: 8 }}
            onClick={() => loadHistory(doc.id)}
          >
            View History
          </button>
        </td>
      </tr>

      {/* STEP 4 GOES HERE */}
      {history[doc.id] && (
        <tr>
          <td colSpan="6">
            <div style={{ background: "#f4f4f4", padding: 10 }}>
              {history[doc.id].map((h, index) => (
                <div key={index}>
                  <strong>{h.role}</strong> - {h.action}
                  {h.reason && ` (Reason: ${h.reason})`}
                </div>
              ))}
            </div>
          </td>
        </tr>
      )}
    </>
  ))}
</tbody>
  </table>
)}

{showRejectModal && (
  <div className="modal-overlay">
    <div className="modal">
      <h3>Reject Document</h3>

      <textarea
        placeholder="Enter rejection reason..."
        value={rejectReason}
        onChange={(e) => setRejectReason(e.target.value)}
        rows={4}
        style={{ width: "100%" }}
      />

      <div style={{ marginTop: 10 }}>
        <button onClick={submitRejection}>Submit</button>
        <button
          onClick={() => {
            setShowRejectModal(false);
            setRejectReason("");
          }}
          style={{ marginLeft: 10 }}
        >
          Cancel
        </button>
      </div>
    </div>
  </div>
)}
    </div>
  );
}
export default App;
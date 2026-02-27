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
  getAiInsights
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

const [filterStartDate, setFilterStartDate] = useState("");
const [filterEndDate, setFilterEndDate] = useState("");
const [filterVendor, setFilterVendor] = useState("");
const [filterStatus, setFilterStatus] = useState("");
const [filterMinAmount, setFilterMinAmount] = useState("");
const [filterMaxAmount, setFilterMaxAmount] = useState("");
const [aiInsights, setAiInsights] = useState([]);

const handleFilter = async () => {
  try {
    const data = await getFilteredReport({
      startDate: filterStartDate,
      endDate: filterEndDate,
      vendor: filterVendor,
      status: filterStatus,
      minAmount: filterMinAmount,
      maxAmount: filterMaxAmount,
    });

    setReportData(data);
  } catch (error) {
    alert(error.message);
  }
};

  // Load data when logged in
  useEffect(() => {
    if (isLoggedIn) {
      getStatusSummary().then(setSummary).catch(() => setSummary(null));
      getDocuments().then(setDocuments).catch(() => setDocuments([]));
      getSpendSummary().then(setSpendData).catch(() => setSpendData(null));
      getAiInsights().then(setAiInsights).catch(() => setAiInsights([]));
    }

  }, [isLoggedIn]);

  const handleLogin = async () => {
    try {
      const data = await login(username, password);
      localStorage.setItem("token", data.token);

      const payload = JSON.parse(atob(data.token.split(".")[1]));
      setUserRole(
        payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]
      );

      setIsLoggedIn(true);
      setMessage("");
    } catch {
      setMessage("Login failed.");
    }
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    setIsLoggedIn(false);
    setUsername("");
    setPassword("");
  };

  const handleUpload = async () => {
    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("documentType", "Invoice");
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

      getStatusSummary().then(setSummary);
      getDocuments().then(setDocuments);
      getSpendSummary().then(setSpendData);
    } catch (error) {
      setUploadMessage(error.message);
    }
  };

  const handleApprove = async (id) => {
    try {
      await approveDocument(id);
      getDocuments().then(setDocuments);
      getStatusSummary().then(setSummary);
      getSpendSummary().then(setSpendData);
    } catch (error) {
      alert(error.message);
    }
  };

  const handleReject = async (id) => {
    try {
      await rejectDocument(id);
      getDocuments().then(setDocuments);
      getStatusSummary().then(setSummary);
      getSpendSummary().then(setSpendData);
    } catch (error) {
      alert(error.message);
    }
  };

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
        <div style={{ marginTop: 20, color: "red" }}>{message}</div>
      </div>
    );
  }

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

    <hr />
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

    <hr />

    <h2>AI Insights</h2>

{Array.isArray(aiInsights) && aiInsights.length > 0 ? (
  <ul>
    {aiInsights.map((insight, index) => (
      <li key={index}>{insight}</li>
    ))}
  </ul>
) : (
  <p>No insights available.</p>
)}

    <hr />

    <h2>Report Filters</h2>

    <div style={{ display: "grid", gap: 10, maxWidth: 400 }}>
      <input type="date" value={filterStartDate} onChange={(e) => setFilterStartDate(e.target.value)} />
      <input type="date" value={filterEndDate} onChange={(e) => setFilterEndDate(e.target.value)} />
      <input placeholder="Vendor" value={filterVendor} onChange={(e) => setFilterVendor(e.target.value)} />

      <select value={filterStatus} onChange={(e) => setFilterStatus(e.target.value)}>
        <option value="">All Status</option>
        <option value="PendingReviewer">Pending Reviewer</option>
        <option value="PendingManager">Pending Manager</option>
        <option value="PendingFinance">Pending Finance</option>
        <option value="Approved">Approved</option>
        <option value="Rejected">Rejected</option>
      </select>

      <input type="number" placeholder="Min Amount" value={filterMinAmount} onChange={(e) => setFilterMinAmount(e.target.value)} />
      <input type="number" placeholder="Max Amount" value={filterMaxAmount} onChange={(e) => setFilterMaxAmount(e.target.value)} />

      <button onClick={handleFilter}>Apply Filters</button>
    </div>

    {reportData && (
      <div style={{ marginTop: 20 }}>
        <h3>Filtered Results</h3>
        <p>Count: {reportData.count}</p>
        <p>Total Amount: {reportData.totalAmount}</p>
        <p>Total VAT: {reportData.totalVat}</p>
      </div>
    )}

    <hr />

    <h2>Upload Document</h2>

    <input
      placeholder="Vendor"
      value={vendor}
      onChange={(e) => setVendor(e.target.value)}
    />

    <input
      placeholder="Invoice Number"
      value={invoiceNumber}
      onChange={(e) => setInvoiceNumber(e.target.value)}
    />

    <input
      type="date"
      value={invoiceDate}
      onChange={(e) => setInvoiceDate(e.target.value)}
    />

    <input
      type="number"
      placeholder="Amount"
      value={amount}
      onChange={(e) => setAmount(e.target.value)}
    />

    <input
      type="number"
      placeholder="VAT Amount"
      value={vatAmount}
      onChange={(e) => setVatAmount(e.target.value)}
    />

    <input
      type="file"
      onChange={async (e) => {
        const selectedFile = e.target.files?.[0];
        if (!selectedFile) return;

        setFile(selectedFile);

        try {
          const data = await extractOcr(selectedFile);

          setVendor(data.vendor || "");
          setInvoiceNumber(data.invoiceNumber || "");
          setInvoiceDate(data.invoiceDate || "");
          setAmount(data.amount || "");
          setVatAmount(data.vatAmount || "");
        } catch (error) {
          console.error("OCR failed:", error);
        }
      }}
    />

    <button onClick={handleUpload}>Upload</button>
    <div>{uploadMessage}</div>

    <hr />

    <h2>Documents</h2>

    {documents.length === 0 ? (
      <p>No documents found.</p>
    ) : (
      <table>
        <thead>
          <tr>
            <th>ID</th>
            <th>Vendor</th>
            <th>Invoice #</th>
            <th>Amount</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {documents.map((doc) => (
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
                    <button onClick={() => handleReject(doc.id)}>
                      Reject
                    </button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    )}
  </div>
);
}

export default App;
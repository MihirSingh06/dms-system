import React, { useState, useEffect } from "react";
import { downloadDocumentFile } from "./api";
import SpendChart from "./Components/SpendChart";
import {
  login,
  getStatusSummary,
  uploadDocument,
  getDocuments,
  approveDocument,
  rejectDocument,
  getSpendSummary,
  getAiInsights,
  getVendorAnalysis,
  getVatReport,
  exportExcel,
  exportPdf,
  getDocumentHistory,
  extractDocument
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

  const [aiInsights, setAiInsights] = useState([]);
  const [history, setHistory] = useState({});

  const [showRejectModal, setShowRejectModal] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [rejectDocId, setRejectDocId] = useState(null);

  // =========================
  // LOAD DASHBOARD
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
  };

  // =========================
  // UPLOAD DOCUMENT (SAVE)
  // =========================
  const handleUpload = async () => {
    try {
      if (!file) {
        setUploadMessage("Please select a file.");
        return;
      }

      const formData = new FormData();
      formData.append("file", file);

      await uploadDocument(formData);

      setUploadMessage("Upload successful!");

      getDocuments().then(setDocuments);
      getStatusSummary().then(setSummary);
      getSpendSummary().then(setSpendData);

    } catch (error) {
      setUploadMessage(error.message || "Upload failed.");
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
      alert("Rejection reason required.");
      return;
    }

    await rejectDocument(rejectDocId, rejectReason);

    setShowRejectModal(false);
    setRejectReason("");
    setRejectDocId(null);

    getDocuments().then(setDocuments);
    getStatusSummary().then(setSummary);
  };

  const loadHistory = async (docId) => {
    const data = await getDocumentHistory(docId);
    

    setHistory((prev) => ({
      ...prev,
      [docId]: data
    }));
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
        <div className="topbar-title">Document Management System</div>
        <div className="topbar-right">
          <div className="role-badge">{userRole}</div>
          <button className="logout-btn" onClick={handleLogout}>Logout</button>
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
            <div className="summary-title">Total</div>
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

      <h2>Upload Document</h2>

      <input
        type="file"
        onChange={async (e) => {
          const selectedFile = e.target.files?.[0];
          if (!selectedFile) return;

          setFile(selectedFile);

          try {
            const data = await extractDocument(selectedFile);

            setVendor(data?.vendor || "");
            setInvoiceNumber(data?.invoiceNumber || "");

            if (data?.invoiceDate) {
              const d = new Date(data.invoiceDate);
              if (!isNaN(d)) {
                setInvoiceDate(d.toISOString().split("T")[0]);
              }
            }

            setAmount(data?.amount || "");
            setVatAmount(data?.vatAmount || "");

          } catch (err) {
            console.error("Extraction failed:", err);
          }
        }}
      />

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
              <th>File</th>
            </tr>
          </thead>
          <tbody>
            {documents.map((doc) => (
              <React.Fragment key={doc.id}>
                <tr>
                 <td>{doc.id}</td>
                  <td>{doc.vendor}</td>
                  <td>{doc.invoiceNumber}</td>
                  <td>{doc.amount}</td>
                  <td>
                    {doc.status}
                    <div style={{ fontSize: 10, color: "gray" }}>
                      Role: {userRole}
                    </div>
                  </td>
                  <td>
                    {(
                      (doc.status === "PendingReviewer" && userRole === "Reviewer") ||
                      (doc.status === "PendingManager" && userRole === "Manager") ||
                      (doc.status === "PendingFinance" && userRole === "Finance")
                    ) && (
                      <>
                        <button onClick={() => handleApprove(doc.id)}>Approve</button>
                        <button
                          onClick={() => {
                            setRejectDocId(doc.id);
                            setShowRejectModal(true);
                          }}
                          style={{ marginLeft: 8 }}
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
                  <td>
                    <button
                      onClick={() =>
                        downloadDocumentFile(doc.id, doc.fileName)
                      }
                    >
                      Download
                    </button>
                  </td>
                </tr>

                {history[doc.id] && (
                  <tr>
                    <td colSpan="7">
                      <div style={{ background: "#f4f4f4", padding: 10 }}>
                        {history[doc.id].length === 0 ? (
                          <div>No history yet</div>
                            ) : (
                            history[doc.id].map((h, index) => (
                          <div key={index}>
                        <strong>{h.role}</strong> - {h.action}
                         {h.reason && ` (Reason: ${h.reason})`}
                       </div>
                      ))
                      )}
                      </div>
                    </td>
                  </tr>
                )}
              </React.Fragment>
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
          style={{ marginLeft: 10 }}
          onClick={() => {
            setShowRejectModal(false);
            setRejectReason("");
          }}
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
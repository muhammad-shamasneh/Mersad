import { useEffect, useState } from "react";
import "./App.css";

type Alert = {
  id: string;
  deviceId: string;
  detectionRuleId: string;
  title: string;
  description: string;
  severity: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  processEventId: string | null;
  networkEventId: string | null;
};

type ApiStatus = "connecting" | "connected" | "disconnected";

function App() {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [apiStatus, setApiStatus] = useState<ApiStatus>("connecting");

  useEffect(() => {
    let isActive = true;

    const loadAlerts = async () => {
      try {
        const response = await fetch("http://localhost:5121/api/alerts");

        if (!response.ok) {
          throw new Error(`API returned ${response.status}`);
        }

        const data: Alert[] = await response.json();

        if (!Array.isArray(data)) {
          throw new Error("API returned an invalid alerts response");
        }

        if (isActive) {
          setAlerts(data);
          setError(null);
          setApiStatus("connected");
          setLoading(false);
        }
      } catch (err: unknown) {
        if (isActive) {
          setError(
            err instanceof Error ? err.message : "An unexpected error occurred",
          );
          setApiStatus("disconnected");
          setLoading(false);
        }
      }
    };

    void loadAlerts();

    const intervalId = window.setInterval(() => {
      void loadAlerts();
    }, 5000);

    return () => {
      isActive = false;
      window.clearInterval(intervalId);
    };
  }, []);

  const highSeverityCount = alerts.filter(
    (alert) => alert.severity === "High",
  ).length;

  const newAlertCount = alerts.filter((alert) => alert.status === "New").length;

  return (
    <div className="dashboard">
      {" "}
      <header className="header">
        {" "}
        <div>
          {" "}
          <h1>Mersad</h1> <p>Security Operations Dashboard</p>{" "}
        </div>
        <div className="status">
          <span className={`status-dot ${apiStatus}`} />
          API{" "}
          {apiStatus === "connecting"
            ? "Connecting..."
            : apiStatus === "connected"
              ? "Connected"
              : "Disconnected"}
        </div>
      </header>
      <main>
        <section className="summary">
          <div className="summary-card">
            <span>Total Alerts</span>
            <strong>{alerts.length}</strong>
          </div>

          <div className="summary-card">
            <span>High Severity</span>
            <strong>{highSeverityCount}</strong>
          </div>

          <div className="summary-card">
            <span>New</span>
            <strong>{newAlertCount}</strong>
          </div>
        </section>

        <section className="alerts-section">
          <div className="section-header">
            <div>
              <h2>Alerts</h2>
              <p>Latest security findings from monitored devices.</p>
            </div>
          </div>

          {loading && <p className="message">Loading alerts...</p>}

          {error && (
            <p className="message error">
              {apiStatus === "disconnected"
                ? `Unable to refresh alerts: ${error}. Showing the last successfully loaded data, if available.`
                : `Failed to load alerts: ${error}`}
            </p>
          )}

          {!loading && !error && alerts.length === 0 && (
            <p className="message">No alerts found.</p>
          )}

          <div className="alert-list">
            {alerts.map((alert) => (
              <article className="alert-card" key={alert.id}>
                <div className="alert-main">
                  <div className="alert-title-row">
                    <span
                      className={`severity ${alert.severity.toLowerCase()}`}
                    >
                      {alert.severity}
                    </span>

                    <span className="rule">{alert.detectionRuleId}</span>

                    <span className="alert-status">{alert.status}</span>
                  </div>

                  <h3>{alert.title}</h3>
                  <p>{alert.description}</p>

                  <small>Device: {alert.deviceId}</small>
                </div>

                <time dateTime={alert.createdAt}>
                  {new Date(alert.createdAt).toLocaleString()}
                </time>
              </article>
            ))}
          </div>
        </section>
      </main>
    </div>
  );
}

export default App;

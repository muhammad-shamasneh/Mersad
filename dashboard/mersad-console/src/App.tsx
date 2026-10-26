import { useEffect, useState, type FormEvent } from "react";
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

const API_URL = "http://localhost:5121";

function App() {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [keyInput, setKeyInput] = useState("");
  const [apiKey, setApiKey] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loginError, setLoginError] = useState<string | null>(null);
  const [apiStatus, setApiStatus] = useState<ApiStatus>("disconnected");

  const signIn = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoginError(null);

    const candidate = keyInput.trim();
    if (!candidate) {
      setLoginError("Enter the dashboard API key.");
      return;
    }

    setLoading(true);

    try {
      const response = await fetch(`${API_URL}/api/alerts`, {
        headers: { Authorization: `Bearer ${candidate}` },
      });

      if (response.status === 401 || response.status === 403) {
        setLoginError("The dashboard API key was rejected.");
        return;
      }

      if (!response.ok) {
        throw new Error(`API returned ${response.status}`);
      }

      const data: Alert[] = await response.json();
      if (!Array.isArray(data)) {
        throw new Error("API returned an invalid alerts response");
      }

      setAlerts(data);
      setApiKey(candidate);
      setKeyInput("");
      setError(null);
      setApiStatus("connected");
    } catch (err: unknown) {
      setLoginError(
        err instanceof Error ? err.message : "Unable to connect to the API",
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!apiKey) return;

    let isActive = true;

    const loadAlerts = async () => {
      try {
        const response = await fetch(`${API_URL}/api/alerts`, {
          headers: { Authorization: `Bearer ${apiKey}` },
        });

        if (response.status === 401 || response.status === 403) {
          if (isActive) {
            setApiKey(null);
            setAlerts([]);
            setError("The dashboard key was rejected. Please sign in again.");
            setApiStatus("disconnected");
          }
          return;
        }

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
        }
      } catch (err: unknown) {
        if (isActive) {
          setError(
            err instanceof Error ? err.message : "An unexpected error occurred",
          );
          setApiStatus("disconnected");
        }
      } finally {
        if (isActive) setLoading(false);
      }
    };

    void loadAlerts();
    const intervalId = window.setInterval(() => void loadAlerts(), 5000);

    return () => {
      isActive = false;
      window.clearInterval(intervalId);
    };
  }, [apiKey]);

  const highSeverityCount = alerts.filter(
    (alert) => alert.severity === "High",
  ).length;
  const newAlertCount = alerts.filter(
    (alert) => alert.status === "New",
  ).length;

  return (
    <div className="dashboard">
      <header className="header">
        <div>
          <h1>Mersad</h1>
          <p>Security Operations Dashboard</p>
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
        {!apiKey ? (
          <section className="auth-panel">
            <h2>Dashboard sign in</h2>
            <p>Enter your dashboard API key to access security alerts.</p>
            <form onSubmit={signIn}>
              <label htmlFor="dashboard-key">Dashboard API key</label>
              <input
                id="dashboard-key"
                type="password"
                autoComplete="off"
                value={keyInput}
                onChange={(event) => setKeyInput(event.target.value)}
                placeholder="Enter API key"
                required
              />
              <button type="submit" disabled={loading}>
                {loading ? "Connecting..." : "Sign in"}
              </button>
            </form>
            {loginError && <p className="message error">{loginError}</p>}
            {error && <p className="message error">{error}</p>}
          </section>
        ) : (
          <>
            <div className="dashboard-actions">
              <span>Authenticated dashboard session</span>
              <button
                type="button"
                className="secondary-button"
                onClick={() => {
                  setApiKey(null);
                  setAlerts([]);
                  setError(null);
                  setApiStatus("disconnected");
                }}
              >
                Sign out
              </button>
            </div>

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
                  Unable to refresh alerts: {error}. Showing the last
                  successfully loaded data, if available.
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
          </>
        )}
      </main>
    </div>
  );
}

export default App;

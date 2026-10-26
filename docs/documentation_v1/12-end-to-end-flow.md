# Mersad V1 — End-to-End Data Flow

## 1. Purpose

This document describes the verified V1 path from endpoint telemetry ingestion to alert presentation in the dashboard.

It focuses on the current synchronous API flow and distinguishes implemented behavior from future capabilities.

## 2. Architecture Overview

```text
+-----------------------+
| Mersad Endpoint Agent |
+-----------+-----------+
            |
            | HTTP telemetry request
            v
+-----------------------+
| ASP.NET Core API      |
| Telemetry Controller  |
+-----------+-----------+
            |
            | Process and persist events
            v
+-----------------------+
| PostgreSQL            |
| Telemetry Records     |
+-----------------------+
            |
            | Build DetectionContext
            v
+-----------------------+
| Detection Engine      |
| Registered Rules      |
+-----------+-----------+
            |
            | Generated detections
            v
+-----------------------+
| Alert Service         |
+-----------+-----------+
            |
            | Persist alerts
            v
+-----------------------+
| PostgreSQL            |
| Alert Records         |
+-----------+-----------+
            |
            | GET /api/alerts
            v
+-----------------------+
| React Dashboard       |
+-----------------------+
```

## 3. Telemetry Submission

The endpoint agent submits telemetry to the API using the telemetry ingestion endpoint.

Telemetry represents endpoint activity collected by the agent, such as process or network events.

The API is responsible for receiving the request and coordinating subsequent processing.

## 4. Telemetry Persistence

The backend processes the incoming telemetry and persists the relevant records through Entity Framework Core and PostgreSQL.

The detection flow runs after telemetry persistence in the verified implementation.

This ordering means the database stores the telemetry records independently of the dashboard's later request to retrieve alerts.

## 5. Detection Evaluation

The telemetry controller creates a `DetectionContext` for the current ingestion batch and passes it to the `DetectionEngine`.

The engine evaluates its registered `IDetectionRule` implementations.

The verified rules cover:

- Suspicious PowerShell execution (`PROC-001`)
- Suspicious remote network ports (`NET-001`)
- PowerShell activity correlated with suspicious network activity across ingestion requests (`CORR-001`). The controller loads persisted events for the device and filters timestamps in application code to identify events within the five-minute correlation window.

When a rule matches, it produces a detection containing information such as the rule ID, title, description, severity, and relevant event references.

The correlation rule does not establish historical correlation across separate ingestion requests.

## 6. Alert Creation and Persistence

The telemetry controller passes generated detections to `AlertService.CreateAlertsAsync()`.

The alert service creates persistent alert records and saves them to PostgreSQL.

Each alert includes relevant detection information, device identity, event references when available, creation and update timestamps, and an initial status of `New`.

Application-level alert deduplication is implemented using the detection rule and associated evidence-event references. It reduces duplicates during sequential processing but is not concurrency-safe. Alert aggregation and suppression are not implemented.

## 7. API Response

The telemetry endpoint returns an HTTP response describing the result of processing the submitted telemetry.

The alerts endpoint, `GET /api/alerts`, provides persisted alerts to the dashboard.

Telemetry ingestion and alert retrieval are separate API operations.

## 8. Dashboard Retrieval

The React dashboard requests alerts from the backend and refreshes the data periodically.

It displays the alert severity, rule identifier, title, description, device identifier, status, and timestamp.

The dashboard derives summary counts from the alerts returned by the API.

It also displays whether the API is connecting, connected, or disconnected. The current implementation uses periodic polling rather than a verified server-push mechanism.

## 9. Failure and Reliability Considerations

The current design is a development-stage MVP.

The verified flow does not establish guarantees for:

- Exactly-once alert creation
- Concurrency-safe deduplication during retries
- Durable asynchronous processing
- Automatic recovery of partially processed ingestion requests
- High-volume ingestion performance
- Historical correlation across ingestion batches

These behaviors require separate implementation and testing.

## 10. Security Boundary

The flow describes data movement, not a complete security boundary.

Authentication, authorization, agent identity, production secret management, and deployment transport security require additional implementation and verification.

CORS configuration alone does not authenticate API callers.

## 11. Future Evolution

Potential future improvements include:

- Centralized configuration for detection policies
- Alert deduplication and lifecycle management
- Historical event correlation
- Search and investigation workflows
- Device inventory and health reporting
- Strong agent identity and API authentication
- Background processing for higher telemetry volume
- AI-assisted explanations as a separate, explicitly identified capability

These are future improvements, not claims about current V1 functionality.

## 12. Summary

Mersad V1 connects endpoint telemetry ingestion, persistent event storage, synchronous rule-based detection, persistent alert creation, an alerts API, and a dashboard.

This end-to-end path forms the foundation for incremental product development. Each new capability should be implemented, tested, documented, and committed as a distinct milestone.

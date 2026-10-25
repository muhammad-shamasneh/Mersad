# Mersad V1 — Known Limitations

## 1. Purpose

This document describes the known limitations of the current Mersad V1 implementation. It distinguishes existing functionality from capabilities that require additional implementation.

The list reflects the code and behavior verified during V1 development. It is not a guarantee that every possible defect or limitation has been identified.

## 2. Detection Limitations

### Rule-Based Detection

Detection currently relies on explicitly implemented rules. The current rules cover suspicious PowerShell execution, suspicious remote network ports, and a correlation between PowerShell and suspicious network activity.

The system does not currently provide general behavioral analytics or machine-learning-based detection.

### Correlation and Scalability

The PowerShell/network correlation rule can correlate recent persisted telemetry across separate ingestion requests. It requires matching device and process IDs, a configured suspicious remote port, and timestamps within five minutes.

The current controller loads the device's events before filtering them by timestamp in application code. This approach can become expensive as telemetry grows and should be optimized before handling substantial event volumes.
### Fixed Detection Policy

The suspicious network ports are defined by the current detection policy. A centralized, externally configurable detection configuration system is not yet verified as implemented.

### Detection Coverage

The existing rules cover a narrow set of scenarios. They do not provide comprehensive endpoint detection, network detection, or threat coverage.

## 3. Alert Management Limitations

The API persists alerts and exposes them through the alerts endpoint. The dashboard displays alerts and derives summary counts from returned data.

`AlertService` performs application-level deduplication by checking the detection rule ID and associated process/network event references. This reduces repeated alerts for the same evidence during ordinary sequential processing.

The check is not concurrency-safe: simultaneous requests may both observe that an alert does not exist and then insert duplicates. A database uniqueness constraint or equivalent protection has not been implemented for this guarantee.

Other limitations include:

- Alert aggregation and suppression
- Alert assignment and ownership
- Alert comments and investigation notes
- A verified workflow for changing alert status
- Case management and incident grouping
- Historical investigation and timeline views
## 4. Dashboard Limitations

The current dashboard provides basic alert presentation and API connectivity status.

Advanced investigation and operational capabilities have not been verified, including:

- Search, filtering, and advanced sorting
- Pagination for large alert volumes
- Detailed alert investigation views
- Device inventory and device detail pages
- Alert assignment and status-management controls
- Real-time push updates
- User accounts and role-based access control

The dashboard polls the alerts API periodically rather than relying on a verified push-based event stream.

## 5. AI Limitations

AI-assisted alert explanation is not implemented in the verified V1 code.

The existing detection descriptions come from rule-based logic. AI-generated explanations, investigation recommendations, and model-provider integrations remain future capabilities.

## 6. Security and Deployment Limitations

The inspected backend startup configuration does not show explicit authentication or authorization middleware.

The current CORS configuration is designed for the local dashboard. It is not a security boundary for direct API clients.

The database configuration and transport-security settings require environment-specific review before deployment beyond local development.

Production-grade identity management, secret management, hardened deployment configuration, and a complete security assessment have not been verified.

## 7. Scalability and Reliability Limitations

The current ingestion flow evaluates detection rules synchronously as part of request processing.

A background processing queue, durable message broker, and independently scalable detection workers have not been verified.

Performance under sustained high telemetry volume, recovery from prolonged service outages, retention policies, and large-scale multi-tenant operation require additional design and testing.

## 8. Configuration Limitations

A centralized configuration system for detection policies and other configurable behavior has not been verified as implemented.

Future configuration work should define validation, defaults, error handling, and how configuration changes take effect.

## 9. Testing and Verification

The project has undergone development-time build and integration checks. These checks do not replace comprehensive security testing, load testing, fault-injection testing, or end-to-end validation across deployment environments.

Any feature should be considered complete only after its implementation and expected behavior have been verified.

## 10. Summary

Mersad V1 provides a foundation for endpoint telemetry ingestion, rule-based detection, persistent alert storage, an alerts API, and a dashboard.

It remains an early-stage MVP. Improving detection coverage, investigation workflows, security controls, centralized configuration, and operational reliability are important next steps.

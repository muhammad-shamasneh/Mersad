#pragma once

#include <string>
#include <vector>

#include "NetworkCollector.h"
#include "ProcessCollector.h"

class HttpClient;

class TelemetryClient
{
public:
    explicit TelemetryClient(HttpClient& httpClient);

    bool SendProcessBatch(
        const std::string& agentId,
        const std::vector<ProcessInfo>& processes);

    bool SendNetworkBatch(
        const std::string& agentId,
        const std::vector<NetworkConnection>& connections);

private:
    HttpClient& httpClient_;
};

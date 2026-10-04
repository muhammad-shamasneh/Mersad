#pragma once

#include <string>
#include <vector>

#include "ProcessCollector.h"

class HttpClient;

class TelemetryClient
{
public:
    explicit TelemetryClient(HttpClient& httpClient);

    bool SendProcessBatch(
        const std::string& agentId,
        const std::vector<ProcessInfo>& processes);

private:
    HttpClient& httpClient_;
};
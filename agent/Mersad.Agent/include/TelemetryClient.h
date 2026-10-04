#pragma once

#include <string>

#include "ProcessCollector.h"

class HttpClient;

class TelemetryClient
{
public:
    explicit TelemetryClient(HttpClient& httpClient);

    bool SendProcessEvent(
        const std::string& deviceId,
        const ProcessInfo& process);

private:
    HttpClient& httpClient_;
};
#include "TelemetryClient.h"
#include "HttpClient.h"

#include <iostream>

TelemetryClient::TelemetryClient(HttpClient& httpClient)
    : httpClient_(httpClient)
{
}

bool TelemetryClient::SendProcessEvent(
    const std::string& deviceId,
    const ProcessInfo& process)
{
    std::string body =
        "{\"deviceId\":\"" + deviceId +
        "\",\"processId\":" + std::to_string(process.processId) +
        ",\"processName\":\"" + process.processName + "\"}";

    std::string response;

    if (!httpClient_.Post(
            L"/api/processevents",
            body,
            response))
    {
        std::cerr << "Failed to send process event."
                  << std::endl;

        return false;
    }

    std::cout << "Process event sent: "
              << process.processName
              << " (PID "
              << process.processId
              << ")"
              << std::endl;

    return true;
}
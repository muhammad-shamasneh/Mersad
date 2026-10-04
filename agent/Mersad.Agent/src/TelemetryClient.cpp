#include "TelemetryClient.h"
#include "HttpClient.h"

#include <chrono>
#include <ctime>
#include <iomanip>
#include <iostream>
#include <sstream>

TelemetryClient::TelemetryClient(HttpClient& httpClient)
    : httpClient_(httpClient)
{
}

bool TelemetryClient::SendProcessBatch(
    const std::string& agentId,
    const std::vector<ProcessInfo>& processes)
{
    if (processes.empty())
    {
        return true;
    }

    auto now = std::chrono::system_clock::now();
    std::time_t currentTime =
        std::chrono::system_clock::to_time_t(now);

    std::tm utcTime{};

    gmtime_s(&utcTime, &currentTime);

    std::ostringstream timestampStream;

    timestampStream << std::put_time(
        &utcTime,
        "%Y-%m-%dT%H:%M:%SZ");

    const std::string timestamp =
        timestampStream.str();

    std::ostringstream body;

    body << "{\"agentId\":\""
         << agentId
         << "\",\"events\":[";

    for (size_t i = 0; i < processes.size(); ++i)
    {
        const auto& process = processes[i];

        if (i > 0)
        {
            body << ",";
        }

        body << "{"
             << "\"type\":\"process\","
             << "\"timestamp\":\""
             << timestamp
             << "\","
             << "\"data\":{"
             << "\"processId\":"
             << process.processId
             << ","
             << "\"processName\":\""
             << process.processName
             << "\"}"
             << "}";
    }

    body << "]}";

    std::string response;

    if (!httpClient_.Post(
            L"/api/telemetry",
            body.str(),
            response))
    {
        std::cerr << "Failed to send telemetry batch."
                  << std::endl;

        return false;
    }

    std::cout << "Telemetry batch sent: "
              << processes.size()
              << " process events."
              << std::endl;

    return true;
}
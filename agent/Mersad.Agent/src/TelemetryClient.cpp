
#include "TelemetryClient.h"
#include "HttpClient.h"
#include "JsonUtils.h"

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

    const std::string timestamp = timestampStream.str();

    std::ostringstream body;

    body << "{\"agentId\":\""
         << JsonUtils::EscapeString(agentId)
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
             << JsonUtils::EscapeString(process.processName)
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

bool TelemetryClient::SendNetworkBatch(
    const std::string& agentId,
    const std::vector<NetworkConnection>& connections)
{
    if (connections.empty())
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

    const std::string timestamp = timestampStream.str();

    std::ostringstream body;

    body << "{\"agentId\":\""
         << JsonUtils::EscapeString(agentId)
         << "\",\"events\":[";

    for (size_t i = 0; i < connections.size(); ++i)
    {
        const auto& connection = connections[i];

        if (i > 0)
        {
            body << ",";
        }

        body << "{"
             << "\"type\":\"network\","
             << "\"timestamp\":\""
             << timestamp
             << "\","
             << "\"data\":{"
             << "\"processId\":"
             << connection.processId
             << ","
             << "\"localAddress\":\""
             << JsonUtils::EscapeString(connection.localAddress)
             << "\","
             << "\"localPort\":"
             << connection.localPort
             << ","
             << "\"remoteAddress\":\""
             << JsonUtils::EscapeString(connection.remoteAddress)
             << "\","
             << "\"remotePort\":"
             << connection.remotePort
             << ","
             << "\"state\":\""
             << JsonUtils::EscapeString(connection.state)
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
        std::cerr << "Failed to send network telemetry batch."
                  << std::endl;

        return false;
    }

    std::cout << "Telemetry batch sent: "
              << connections.size()
              << " network events."
              << std::endl;

    return true;
}
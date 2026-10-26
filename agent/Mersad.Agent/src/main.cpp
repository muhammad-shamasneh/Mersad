
#include "DeviceManager.h"
#include "HttpClient.h"
#include "ProcessCollector.h"
#include "TelemetryClient.h"
#include "AgentIdentity.h"
#include "NetworkCollector.h"

#include <iostream>
#include <cstdlib>
#include <string>
#include <vector>

#include <windows.h>

std::string GetHostname()
{
    char hostname[MAX_COMPUTERNAME_LENGTH + 1] = {};
    DWORD size = sizeof(hostname);

    if (GetComputerNameA(hostname, &size))
    {
        return std::string(hostname, size);
    }

    return "UNKNOWN";
}

int main()
{
    std::cout << "Mersad Agent started." << std::endl;

    const std::string hostname = GetHostname();

    std::cout << "Hostname: "
              << hostname
              << std::endl;

    AgentIdentity identity;

    const std::string agentId = identity.GetOrCreate();

    if (agentId.empty())
    {
        std::cerr << "Failed to initialize AgentId."
                  << std::endl;

        return 1;
    }

    std::cout << "AgentId: "
              << agentId
              << std::endl;

    const char* apiKey = std::getenv("MERSAD_AGENT_API_KEY");

    if (apiKey == nullptr || *apiKey == '\0')
    {
        std::cerr << "MERSAD_AGENT_API_KEY is not set." << std::endl;
        return 1;
    }

    HttpClient httpClient(
        L"localhost",
        5121,
        apiKey);

    DeviceManager deviceManager(httpClient);

    const std::string deviceId =
        deviceManager.RegisterDevice(
            agentId,
            hostname);

    if (deviceId.empty())
    {
        std::cerr << "Device registration failed."
                  << std::endl;

        return 1;
    }

    std::cout << "Device ID: "
              << deviceId
              << std::endl;

    ProcessCollector processCollector;

    std::vector<ProcessInfo> processes;

    if (!processCollector.Collect(processes))
    {
        std::cerr << "Process collection failed."
                  << std::endl;

        return 1;
    }

    std::cout << "Processes found: "
              << processes.size()
              << std::endl;

    NetworkCollector networkCollector;

    const std::vector<NetworkConnection> connections =
        networkCollector.Collect();

    std::cout << "Network connections found: "
              << connections.size()
              << std::endl;

    TelemetryClient telemetryClient(httpClient);

    if (!telemetryClient.SendProcessBatch(
            agentId,
            processes))
    {
        std::cerr << "Process telemetry submission failed."
                  << std::endl;

        return 1;
    }

    if (!telemetryClient.SendNetworkBatch(
            agentId,
            connections))
    {
        std::cerr << "Network telemetry submission failed."
                  << std::endl;

        return 1;
    }

    std::cout << "Telemetry submission completed successfully."
              << std::endl;

    return 0;
}

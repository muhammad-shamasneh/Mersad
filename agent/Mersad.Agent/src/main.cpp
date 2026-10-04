#include "DeviceManager.h"
#include "HttpClient.h"
#include "ProcessCollector.h"
#include "TelemetryClient.h"

#include <iostream>
#include <string>
#include <vector>
#include <windows.h>

std::string GetHostname()
{
    char hostname[MAX_COMPUTERNAME_LENGTH + 1];
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

    std::string hostname = GetHostname();

    std::cout << "Hostname: "
              << hostname
              << std::endl;

    HttpClient httpClient(
        L"localhost",
        5121);

    DeviceManager deviceManager(httpClient);

    std::string deviceId =
        deviceManager.RegisterDevice(hostname);

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

    std::vector<ProcessInfo> processes =
        processCollector.Collect();

    std::cout << "Processes found: "
              << processes.size()
              << std::endl;

    TelemetryClient telemetryClient(httpClient);

    for (const auto& process : processes)
    {
        telemetryClient.SendProcessEvent(
            deviceId,
            process);
    }

    return 0;
}
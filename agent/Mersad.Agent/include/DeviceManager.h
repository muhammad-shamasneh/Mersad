#pragma once

#include <string>

class HttpClient;

class DeviceManager
{
public:
    explicit DeviceManager(HttpClient& httpClient);

    std::string RegisterDevice(
        const std::string& agentId,
        const std::string& hostname);

private:
    HttpClient& httpClient_;
};
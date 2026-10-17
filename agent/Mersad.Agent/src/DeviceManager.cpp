
#include "DeviceManager.h"
#include "HttpClient.h"
#include "JsonUtils.h"

#include <iostream>

DeviceManager::DeviceManager(HttpClient& httpClient)
    : httpClient_(httpClient)
{
}

std::string DeviceManager::RegisterDevice(
    const std::string& agentId,
    const std::string& hostname)
{
    const std::string body =
        "{\"agentId\":\"" + JsonUtils::EscapeString(agentId) +
        "\",\"hostname\":\"" + JsonUtils::EscapeString(hostname) +
        "\",\"os\":\"Windows\",\"osVersion\":\"11\"}";

    std::string response;

    if (!httpClient_.Post(
            L"/api/devices",
            body,
            response))
    {
        std::cerr << "Device registration request failed."
                  << std::endl;

        return "";
    }

    std::cout << "Registration response: "
              << response
              << std::endl;

    const std::string idPrefix = "\"id\":\"";

    size_t idStart = response.find(idPrefix);

    if (idStart == std::string::npos)
    {
        std::cerr << "Device ID not found in API response."
                  << std::endl;

        return "";
    }

    idStart += idPrefix.length();

    size_t idEnd = response.find('"', idStart);

    if (idEnd == std::string::npos)
    {
        std::cerr << "Invalid Device ID in API response."
                  << std::endl;

        return "";
    }

    return response.substr(
        idStart,
        idEnd - idStart);
}
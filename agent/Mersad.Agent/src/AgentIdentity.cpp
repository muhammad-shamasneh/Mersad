#include "AgentIdentity.h"

#include <fstream>
#include <iostream>
#include <rpc.h>

#pragma comment(lib, "Rpcrt4.lib")

std::string AgentIdentity::GetOrCreate()
{
    std::string agentId = LoadAgentId();

    if (!agentId.empty())
    {
        return agentId;
    }

    agentId = GenerateAgentId();

    if (agentId.empty())
    {
        return "";
    }

    if (!SaveAgentId(agentId))
    {
        return "";
    }

    return agentId;
}

std::string AgentIdentity::GenerateAgentId()
{
    UUID uuid{};

    if (UuidCreate(&uuid) != RPC_S_OK)
    {
        std::cerr << "Failed to generate AgentId."
                  << std::endl;

        return "";
    }

    RPC_CSTR uuidString = nullptr;

    if (UuidToStringA(&uuid, &uuidString) != RPC_S_OK)
    {
        std::cerr << "Failed to convert UUID to string."
                  << std::endl;

        return "";
    }

    std::string agentId(
        reinterpret_cast<char*>(uuidString));

    RpcStringFreeA(&uuidString);

    return agentId;
}

std::string AgentIdentity::LoadAgentId()
{
    std::ifstream file("Mersad.AgentId");

    if (!file)
    {
        return "";
    }

    std::string agentId;

    std::getline(file, agentId);

    return agentId;
}

bool AgentIdentity::SaveAgentId(
    const std::string& agentId)
{
    std::ofstream file("Mersad.AgentId");

    if (!file)
    {
        std::cerr << "Failed to save AgentId."
                  << std::endl;

        return false;
    }

    file << agentId;

    return true;
}
#include "AgentIdentity.h"

#include <Windows.h>
#include <knownfolders.h>
#include <shlobj.h>
#include <rpc.h>

#include <filesystem>
#include <fstream>
#include <iostream>
#include <system_error>

#pragma comment(lib, "Rpcrt4.lib")
#pragma comment(lib, "Shell32.lib")
#pragma comment(lib, "Ole32.lib")

namespace
{
    bool GetIdentityFilePath(std::filesystem::path& filePath)
    {
        PWSTR programDataPath = nullptr;

        const HRESULT result = SHGetKnownFolderPath(
            FOLDERID_ProgramData,
            KF_FLAG_DEFAULT,
            nullptr,
            &programDataPath);

        if (FAILED(result))
        {
            std::cerr << "Failed to locate the ProgramData directory. HRESULT: "
                      << std::hex << result
                      << std::dec << std::endl;

            return false;
        }

        const std::filesystem::path directory =
            std::filesystem::path(programDataPath) / L"Mersad";

        CoTaskMemFree(programDataPath);

        filePath = directory / L"Mersad.AgentId";

        std::error_code error;

        std::filesystem::create_directories(directory, error);

        if (error)
        {
            std::cerr << "Failed to create the Mersad data directory: "
                      << error.message()
                      << std::endl;

            return false;
        }

        return true;
    }
}

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

    const RPC_STATUS status = UuidCreate(&uuid);

    if (status != RPC_S_OK && status != RPC_S_UUID_LOCAL_ONLY)
    {
        std::cerr << "Failed to generate AgentId. Status: "
                  << status
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

    const std::string agentId(
        reinterpret_cast<const char*>(uuidString));

    RpcStringFreeA(&uuidString);

    return agentId;
}

std::string AgentIdentity::LoadAgentId()
{
    std::filesystem::path filePath;

    if (!GetIdentityFilePath(filePath))
    {
        return "";
    }

    std::ifstream file(filePath);

    if (!file)
    {
        return "";
    }

    std::string agentId;
    std::getline(file, agentId);

    return agentId;
}

bool AgentIdentity::SaveAgentId(const std::string& agentId)
{
    std::filesystem::path filePath;

    if (!GetIdentityFilePath(filePath))
    {
        return false;
    }

    std::ofstream file(filePath, std::ios::trunc);

    if (!file)
    {
        std::cerr << "Failed to save AgentId to the Mersad data directory."
                  << std::endl;

        return false;
    }

    file << agentId;
    file.flush();

    if (!file)
    {
        std::cerr << "Failed to write AgentId completely."
                  << std::endl;

        return false;
    }

    return true;
}

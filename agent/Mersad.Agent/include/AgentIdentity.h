#pragma once

#include <string>

class AgentIdentity
{
public:
    std::string GetOrCreate();

private:
    std::string GenerateAgentId();
    std::string LoadAgentId();
    bool SaveAgentId(const std::string& agentId);
};
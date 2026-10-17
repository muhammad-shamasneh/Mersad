#pragma once

#include <string>
#include <vector>
#include <windows.h>

struct ProcessInfo
{
    DWORD processId;
    std::string processName;
};

class ProcessCollector
{
public:
    // Returns true when enumeration completes successfully.
    // An empty result is valid if enumeration succeeds with no entries.
    bool Collect(std::vector<ProcessInfo>& processes);
};

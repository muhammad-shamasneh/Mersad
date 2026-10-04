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
    std::vector<ProcessInfo> Collect();
};
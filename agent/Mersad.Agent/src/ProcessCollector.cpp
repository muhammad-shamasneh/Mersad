#include "ProcessCollector.h"

#include <iostream>
#include <tlhelp32.h>

std::vector<ProcessInfo> ProcessCollector::Collect()
{
    std::vector<ProcessInfo> processes;

    HANDLE snapshot = CreateToolhelp32Snapshot(
        TH32CS_SNAPPROCESS,
        0);

    if (snapshot == INVALID_HANDLE_VALUE)
    {
        std::cerr << "CreateToolhelp32Snapshot failed: "
                  << GetLastError()
                  << std::endl;

        return processes;
    }

    PROCESSENTRY32 processEntry{};
    processEntry.dwSize = sizeof(PROCESSENTRY32);

    if (!Process32First(snapshot, &processEntry))
    {
        std::cerr << "Process32First failed: "
                  << GetLastError()
                  << std::endl;

        CloseHandle(snapshot);
        return processes;
    }

    do
    {
        ProcessInfo process;

        process.processId = processEntry.th32ProcessID;
        process.processName = processEntry.szExeFile;

        processes.push_back(process);

    } while (Process32Next(snapshot, &processEntry));

    CloseHandle(snapshot);

    return processes;
}
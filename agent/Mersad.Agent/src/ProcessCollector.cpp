
#include "ProcessCollector.h"

#include <iostream>
#include <tlhelp32.h>

bool ProcessCollector::Collect(
    std::vector<ProcessInfo>& processes)
{
    processes.clear();

    HANDLE snapshot = CreateToolhelp32Snapshot(
        TH32CS_SNAPPROCESS,
        0);

    if (snapshot == INVALID_HANDLE_VALUE)
    {
        std::cerr << "CreateToolhelp32Snapshot failed: "
                  << GetLastError()
                  << std::endl;

        return false;
    }

    PROCESSENTRY32 processEntry{};
    processEntry.dwSize = sizeof(processEntry);

    if (!Process32First(snapshot, &processEntry))
    {
        const DWORD error = GetLastError();

        CloseHandle(snapshot);

        if (error == ERROR_NO_MORE_FILES)
        {
            return true;
        }

        std::cerr << "Process32First failed: "
                  << error
                  << std::endl;

        return false;
    }

    do
    {
        if (processEntry.th32ProcessID == 0)
        {
            continue;
        }

        ProcessInfo process{};
        process.processId = processEntry.th32ProcessID;
        process.processName = processEntry.szExeFile;

        processes.push_back(std::move(process));

    } while (Process32Next(snapshot, &processEntry));

    const DWORD error = GetLastError();

    CloseHandle(snapshot);

    if (error != ERROR_NO_MORE_FILES)
    {
        std::cerr << "Process32Next failed: "
                  << error
                  << std::endl;

        processes.clear();
        return false;
    }

    return true;
}

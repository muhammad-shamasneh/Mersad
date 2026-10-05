#include "NetworkCollector.h"

#include <iphlpapi.h>
#include <ws2tcpip.h>

#include <iostream>
#include <vector>

#pragma comment(lib, "iphlpapi.lib")
#pragma comment(lib, "ws2_32.lib")

std::vector<NetworkConnection> NetworkCollector::Collect()
{
    std::vector<NetworkConnection> connections;

    DWORD size = 0;

    DWORD result = GetExtendedTcpTable(
        nullptr,
        &size,
        FALSE,
        AF_INET,
        TCP_TABLE_OWNER_PID_ALL,
        0);

    if (result != ERROR_INSUFFICIENT_BUFFER)
    {
        std::cerr << "Failed to determine TCP table size."
                  << std::endl;

        return connections;
    }

    std::vector<BYTE> buffer(size);

    auto* table =
        reinterpret_cast<PMIB_TCPTABLE_OWNER_PID>(
            buffer.data());

    result = GetExtendedTcpTable(
        table,
        &size,
        FALSE,
        AF_INET,
        TCP_TABLE_OWNER_PID_ALL,
        0);

    if (result != NO_ERROR)
    {
        std::cerr << "Failed to retrieve TCP table."
                  << std::endl;

        return connections;
    }

    for (DWORD i = 0; i < table->dwNumEntries; ++i)
    {
        const auto& row = table->table[i];

        NetworkConnection connection{};

        connection.processId = row.dwOwningPid;
        connection.localPort = ntohs(
            static_cast<u_short>(row.dwLocalPort));
        connection.remotePort = ntohs(
            static_cast<u_short>(row.dwRemotePort));

        char localAddress[INET_ADDRSTRLEN]{};
        char remoteAddress[INET_ADDRSTRLEN]{};

        inet_ntop(
            AF_INET,
            &row.dwLocalAddr,
            localAddress,
            INET_ADDRSTRLEN);

        inet_ntop(
            AF_INET,
            &row.dwRemoteAddr,
            remoteAddress,
            INET_ADDRSTRLEN);

        connection.localAddress = localAddress;
        connection.remoteAddress = remoteAddress;

        connection.state =
            std::to_string(row.dwState);

        connections.push_back(
            std::move(connection));
    }

    return connections;
}
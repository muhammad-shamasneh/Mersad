#pragma once

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>

#include <string>
#include <vector>

struct NetworkConnection
{
    DWORD processId;
    std::string localAddress;
    DWORD localPort;
    std::string remoteAddress;
    DWORD remotePort;
    std::string state;
};

class NetworkCollector
{
public:
    std::vector<NetworkConnection> Collect();
};
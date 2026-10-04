#include "HttpClient.h"

#include <iostream>
#include <vector>

#pragma comment(lib, "winhttp.lib")

HttpClient::HttpClient(
    const std::wstring& host,
    INTERNET_PORT port)
    : host_(host),
      port_(port)
{
}

bool HttpClient::Post(
    const std::wstring& path,
    const std::string& body,
    std::string& response)
{
    HINTERNET session = WinHttpOpen(
        L"MersadAgent/1.0",
        WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
        WINHTTP_NO_PROXY_NAME,
        WINHTTP_NO_PROXY_BYPASS,
        0);

    if (!session)
    {
        std::cerr << "WinHttpOpen failed: "
                  << GetLastError() << std::endl;
        return false;
    }

    HINTERNET connection = WinHttpConnect(
        session,
        host_.c_str(),
        port_,
        0);

    if (!connection)
    {
        std::cerr << "WinHttpConnect failed: "
                  << GetLastError() << std::endl;

        WinHttpCloseHandle(session);
        return false;
    }

    HINTERNET request = WinHttpOpenRequest(
        connection,
        L"POST",
        path.c_str(),
        nullptr,
        WINHTTP_NO_REFERER,
        WINHTTP_DEFAULT_ACCEPT_TYPES,
        0);

    if (!request)
    {
        std::cerr << "WinHttpOpenRequest failed: "
                  << GetLastError() << std::endl;

        WinHttpCloseHandle(connection);
        WinHttpCloseHandle(session);
        return false;
    }

    const wchar_t* headers =
        L"Content-Type: application/json";

    BOOL result = WinHttpSendRequest(
        request,
        headers,
        -1L,
        (LPVOID)body.c_str(),
        static_cast<DWORD>(body.size()),
        static_cast<DWORD>(body.size()),
        0);

    if (!result)
    {
        std::cerr << "WinHttpSendRequest failed: "
                  << GetLastError() << std::endl;

        WinHttpCloseHandle(request);
        WinHttpCloseHandle(connection);
        WinHttpCloseHandle(session);
        return false;
    }

    if (!WinHttpReceiveResponse(request, nullptr))
    {
        std::cerr << "WinHttpReceiveResponse failed: "
                  << GetLastError() << std::endl;

        WinHttpCloseHandle(request);
        WinHttpCloseHandle(connection);
        WinHttpCloseHandle(session);
        return false;
    }

    response.clear();

    DWORD availableSize = 0;

    while (WinHttpQueryDataAvailable(
        request,
        &availableSize) &&
        availableSize > 0)
    {
        std::vector<char> buffer(availableSize);

        DWORD bytesRead = 0;

        if (!WinHttpReadData(
                request,
                buffer.data(),
                availableSize,
                &bytesRead))
        {
            std::cerr << "WinHttpReadData failed: "
                      << GetLastError() << std::endl;

            WinHttpCloseHandle(request);
            WinHttpCloseHandle(connection);
            WinHttpCloseHandle(session);
            return false;
        }

        response.append(buffer.data(), bytesRead);
    }

    WinHttpCloseHandle(request);
    WinHttpCloseHandle(connection);
    WinHttpCloseHandle(session);

    return true;
}
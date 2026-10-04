#pragma once

#include <string>
#include <windows.h>
#include <winhttp.h>

class HttpClient
{
public:
    HttpClient(
        const std::wstring& host,
        INTERNET_PORT port);

    bool Post(
        const std::wstring& path,
        const std::string& body,
        std::string& response);

private:
    std::wstring host_;
    INTERNET_PORT port_;
};
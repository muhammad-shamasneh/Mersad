
#pragma once

#include <string>
#include <string_view>

namespace JsonUtils
{
    inline std::string EscapeString(std::string_view value)
    {
        std::string escaped;
        escaped.reserve(value.size());

        constexpr char hexDigits[] = "0123456789abcdef";

        for (unsigned char ch : value)
        {
            switch (ch)
            {
            case '"':
                escaped += "\\\"";
                break;

            case '\\':
                escaped += "\\\\";
                break;

            case '\b':
                escaped += "\\b";
                break;

            case '\f':
                escaped += "\\f";
                break;

            case '\n':
                escaped += "\\n";
                break;

            case '\r':
                escaped += "\\r";
                break;

            case '\t':
                escaped += "\\t";
                break;

            default:
                if (ch < 0x20)
                {
                    escaped += "\\u00";
                    escaped += hexDigits[(ch >> 4) & 0x0F];
                    escaped += hexDigits[ch & 0x0F];
                }
                else
                {
                    escaped += static_cast<char>(ch);
                }
                break;
            }
        }

        return escaped;
    }
}

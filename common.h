#include <cstdint>

#pragma once

namespace vdd_switcher
{
    const char *PIPE_NAME = R"(\\.\pipe\vddswitcherd)";

    enum Command : uint32_t
    {
        StopVirtualDisplay = 200,
    };

    struct Request
    {
        Command command;
    };

    const size_t REQUEST_SIZE = sizeof(Request);
}

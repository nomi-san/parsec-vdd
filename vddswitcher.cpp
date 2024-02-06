#include <getopt.h>
#include <cstddef>
#include <iostream>
#include <Windows.h>

#include "common.h"

int main()
{
    auto hMutex = CreateMutex(NULL, TRUE, "56A4EE84-1ED3-D133-73EF-A3B8E479C4DF");
    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        std::cerr << "You can only run one vddswitcher instance at a time." << std::endl;
        CloseHandle(hMutex);
        exit(EXIT_FAILURE);
    }

    do
    {
        // 连接到服务器的命名管道
        auto hPipe = CreateFile(
            vdd_switcher::PIPE_NAME, // 管道名称
            GENERIC_WRITE,           // 只写
            0,                       // 不共享
            NULL,                    // 默认安全属性
            OPEN_EXISTING,           // 打开已存在的管道
            0,                       // 默认属性
            NULL                     // 默认模板文件
        );

        if (hPipe == INVALID_HANDLE_VALUE)
        {
            break;
        }

        // 向管道写入数据
        vdd_switcher::Request request;
        ZeroMemory(&request, sizeof(request));

        request.command = vdd_switcher::Command::StopVirtualDisplay;
        DWORD bytesWritten;
        if (!WriteFile(hPipe, &request, vdd_switcher::REQUEST_SIZE, &bytesWritten, NULL))
        {
            std::cerr << "Error writing to pipe." << std::endl;
        }

        // 关闭管道
        CloseHandle(hPipe);

        break;
    } while (false);

    CloseHandle(hMutex);

    exit(EXIT_SUCCESS);
}

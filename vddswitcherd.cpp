#include <iostream>
#include <chrono>
#include <thread>
#include <unistd.h>
#include <Windows.h>

#include "common.h"
#include "vddswitcherd.h"
#include "include/parsec-vdd.h"

int main(int argc, char *argv[])
{
    if (argc > 1)
    {
        std::string command = argv[1];

        if (command != "async")
        {
            std::cerr << "Invalid command!" << std::endl;
            return 1;
        }
    }
    else
    {
        TCHAR currentPath[MAX_PATH];
        GetModuleFileName(NULL, currentPath, MAX_PATH);
        ShellExecute(NULL, "open", currentPath, "async", NULL,
#ifdef HIDE_CONSOLE
                     SW_HIDE
#else
                     SW_SHOW
#endif
        );
        return 0;
    }

    auto hMutex = CreateMutex(NULL, TRUE, "586291D7-3993-5607-8C32-F2E7569E1DCA");
    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        std::cerr << "You can only run one vddswitcherd instance at a time." << std::endl;
        CloseHandle(hMutex);
        return 0;
    }

    do
    {
        auto status = parsec_vdd::QueryDeviceStatus(&parsec_vdd::VDD_CLASS_GUID, parsec_vdd::VDD_HARDWARE_ID);
        if (status != parsec_vdd::DEVICE_OK)
        {
            std::cerr << "Parsec VDD device is not OK, got status" << status << "." << std::endl;
            break;
        }

        auto vdd = parsec_vdd::OpenDeviceHandle(&parsec_vdd::VDD_ADAPTER_GUID);
        if (vdd == NULL || vdd == INVALID_HANDLE_VALUE)
        {
            std::cerr << "Failed to obtain the device handle." << std::endl;
            break;
        }

        start(vdd);

        // Close the device handle.
        parsec_vdd::CloseDeviceHandle(vdd);

    } while (0);

    CloseHandle(hMutex);

    return 0;
}

void start(const HANDLE &vdd)
{
    std::byte buffer[vdd_switcher::REQUEST_SIZE];
    DWORD bytesRead;

    // 创建命名管道
    auto hPipe = CreateNamedPipe(
        vdd_switcher::PIPE_NAME, // 管道名称
        PIPE_ACCESS_DUPLEX,      // 可读写
        PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
        1,
        0,                          // 输出缓冲区大小
        vdd_switcher::REQUEST_SIZE, // 输入缓冲区大小
        0,                          // 默认超时时间
        NULL                        // 默认安全属性
    );

    do
    {
        if (hPipe == INVALID_HANDLE_VALUE)
        {
            std::cerr << "Error creating named pipe." << std::endl;
            exit(0);
        }

        bool running = true;
        bool vd = true;
        std::thread updater([&running, vdd]
                            {
        while (running) 
        {
            parsec_vdd::VddUpdate(vdd);
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        } });

        updater.detach();

        parsec_vdd::VddAddDisplay(vdd);
        std::cout << "width:" << getenv("SUNSHINE_CLIENT_WIDTH") << std::endl;

        while (running)
        {
            // 等待客户端连接
            if (!ConnectNamedPipe(hPipe, NULL))
            {
                std::cerr << "Error connecting to client." << std::endl;
                CloseHandle(hPipe);
                break;
            }

            // 从管道读取客户端发送的数据
            if (ReadFile(hPipe, buffer, vdd_switcher::REQUEST_SIZE, &bytesRead, NULL))
            {
                if (bytesRead == vdd_switcher::REQUEST_SIZE)
                {
                    auto request = reinterpret_cast<const vdd_switcher::Request &>(buffer);
                    if (!process_request(vdd, request, vd))
                    {
                        running = false;
                    }
                }
                else
                {
                    std::cerr << "Request is malformed: insufficient length." << buffer << std::endl;
                }
                std::cout << "Received from client: " << buffer << std::endl;
            }
            else
            {
                std::cerr << "Error reading from pipe." << std::endl;
            }

            // 断开客户端连接
            DisconnectNamedPipe(hPipe);
        }

        running = false;

        if (vd)
        {
            parsec_vdd::VddRemoveDisplay(vdd, 0);
        }

        if (updater.joinable())
        {
            updater.join();
        }
    } while (0);

    CloseHandle(hPipe);
}

bool process_request(const HANDLE &vdd, const vdd_switcher::Request &request, bool &vd)
{
    if (request.command == vdd_switcher::Command::StopVirtualDisplay && vd)
    {
        parsec_vdd::VddRemoveDisplay(vdd, 0);
        return false;
    }

    return true;
}
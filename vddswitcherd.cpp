#include <iostream>
#include <sstream>
#include <vector>
#include <set>
#include <getopt.h>
#include <chrono>
#include <thread>
#include <unistd.h>
#include <Windows.h>

#include "common.h"
#include "include/parsec-vdd.h"

bool process_request(const HANDLE &vdd, const vdd_switcher::Request &request, bool &vd)
{
    if (request.command == vdd_switcher::Command::StopVirtualDisplay && vd)
    {
        parsec_vdd::VddRemoveDisplay(vdd, 0);
        return false;
    }

    return true;
}

void start(const HANDLE &vdd, DWORD x, DWORD y, DWORD r)
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

        std::cout << "stream resolution:" << x << "x" << y << "@" << r << std::endl;
        std::vector<std::tuple<DWORD, DWORD, DWORD>> resolutions;

        DISPLAY_DEVICE dd;
        dd.cb = sizeof(DISPLAY_DEVICE);

        const size_t DEVICE_NAME_MAX = 32;
        char device_name[DEVICE_NAME_MAX];
        for (DWORD i = 0; EnumDisplayDevices(NULL, i, &dd, 0); ++i)
        {
            if (!(dd.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) &&
                (dd.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) && strcmp(dd.DeviceID, parsec_vdd::VDD_HARDWARE_ID) == 0)
            {
                DEVMODE devMode;
                int modeNum = 0;

                strcpy_s(device_name, DEVICE_NAME_MAX, dd.DeviceName);

                std::cout << device_name << " resolutions:" << std::endl;

                while (EnumDisplaySettings(device_name, modeNum, &devMode))
                {
                    resolutions.emplace_back(
                        devMode.dmPelsWidth,
                        devMode.dmPelsHeight,
                        devMode.dmDisplayFrequency);

                    std::cout << "    " << devMode.dmPelsWidth << "x" << devMode.dmPelsHeight << "@" << devMode.dmDisplayFrequency << std::endl;

                    ++modeNum;
                }
            }
        }

        bool find = false;
        for (auto resolution = resolutions.begin(); resolution != resolutions.end(); resolution++)
        {
            if ((x == std::get<0>(*resolution)) && (y == std::get<1>(*resolution)) && (r == std::get<2>(*resolution)))
            {
                DEVMODE devMode;
                ZeroMemory(&devMode, sizeof(devMode));
                devMode.dmSize = sizeof(devMode);
                devMode.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
                devMode.dmPelsWidth = x;
                devMode.dmPelsHeight = y;
                devMode.dmDisplayFrequency = r;

                ChangeDisplaySettingsEx(device_name, &devMode, NULL, CDS_GLOBAL | CDS_UPDATEREGISTRY, NULL);
                find = true;
                break;
            }
        }

        if (!find)
        {
            std::cerr << "No match resolution in the primary display." << std::endl;
        }

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

int main(int argc, char *argv[])
{
    int opt;
    bool async = false;
    bool debug = false;
    DWORD x = 0;
    DWORD y = 0;
    DWORD r = 0;

    struct option long_options[] = {
        {"async", no_argument, NULL, 'a'},
        {"debug", no_argument, NULL, 'd'},
        {"width", required_argument, NULL, 'x'},
        {"height", required_argument, NULL, 'y'},
        {"fps", required_argument, NULL, 'r'},
        {NULL, 0, NULL, 0}};

    while ((opt = getopt_long(argc, argv, "adx:y:r:", long_options, NULL)) != -1)
    {
        switch (opt)
        {
        case 'a':
            async = true;
            break;
        case 'd':
            debug = true;
            break;
        case 'x':
            x = atoi(optarg);
            break;
        case 'y':
            y = atoi(optarg);
            break;
        case 'r':
            r = atoi(optarg);
            break;
        default:
            std::cerr << "Usage:" << argv[0] << "--width <width> --height <height> --fps <fps> (--async) (--debug)" << std::endl;
            exit(EXIT_FAILURE);
        }
    }

    if (x <= 0 || y <= 0 || r <= 0)
    {
        std::cerr << "Usage:" << argv[0] << "--width <width> --height <height> --fps <fps> (--async) (--debug)" << std::endl;
        exit(EXIT_FAILURE);
    }

    if (!async)
    {
        std::stringstream ss;
        ss << "--async"
           << " --width " << x << " --height " << y << " --fps " << r;
        ShellExecute(NULL, "open", argv[0], ss.str().c_str(), NULL, debug ? SW_SHOW : SW_HIDE);

        exit(EXIT_SUCCESS);
    }

    auto hMutex = CreateMutex(NULL, TRUE, "586291D7-3993-5607-8C32-F2E7569E1DCA");
    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        std::cerr << "You can only run one vddswitcherd instance at a time." << std::endl;
        CloseHandle(hMutex);
        exit(EXIT_SUCCESS);
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

        start(vdd, x, y, r);

        // Close the device handle.
        parsec_vdd::CloseDeviceHandle(vdd);

    } while (0);

    CloseHandle(hMutex);

    exit(EXIT_SUCCESS);
}

#include <stdio.h>
#include <conio.h>
#include <thread>
#include <chrono>
#include <vector>
#include "parsec-vdd.h"

using namespace std::chrono_literals;
using namespace parsec_vdd;

int main()
{
    // Check driver status.
    DeviceStatus status = QueryDeviceStatus(&VDD_CLASS_GUID, VDD_HARDWARE_ID);
    if (status != DEVICE_OK)
    {
        printf("Parsec VDD device is not OK, got status %d.\n", status);
        return 1;
    }

    // Obtain device handle.
    HANDLE vdd = OpenDeviceHandle(&VDD_ADAPTER_GUID);
    if (vdd == NULL || vdd == INVALID_HANDLE_VALUE) {
        printf("Failed to obtain the device handle.\n");
        return 1;
    }

    bool running = true;
    std::vector<int> displays;

    // Side thread for updating vdd.
    std::thread updater([&running, vdd] {
        while (running) {
            VddUpdate(vdd);
            std::this_thread::sleep_for(100ms);
        }
    });

    // Print out guide.
    printf("Press A to add a virtual display.\n");
    printf("Press R to remove the last added.\n");
    printf("Press Q to quit (then unplug all).\n\n");

    while (running) {
        switch (_getch()) {
            // quit
            case 'q':
                running = false;
                break;
            // add display
            case 'a':
                if (displays.size() < VDD_MAX_DISPLAYS) {
                    int index = VddAddDisplay(vdd);
                    if (index != -1) {
                        displays.push_back(index);
                        printf("Added a new virtual display, index: %d.\n", index);
                    }
                    else {
                        printf("Add virtual display failed.");
                    }
                }
                else {
                    printf("Limit exceeded (%d), could not add more virtual displays.\n", VDD_MAX_DISPLAYS);
                }
                break;
            // remove display
            case 'r':
                if (displays.size() > 0) {
                    int index = displays.back();
                    VddRemoveDisplay(vdd, index);
                    displays.pop_back();
                    printf("Removed the last virtual display, index: %d.\n", index);
                }
                else {
                    printf("No added virtual displays.\n");
                }
                break;
        }
    }

    // Remove all before exiting.
    for (int index : displays) {
        VddRemoveDisplay(vdd, index);
    }

    if (updater.joinable()) {
        updater.join();
    }

    // Close the device handle.
    CloseDeviceHandle(vdd);

    return 0;
}
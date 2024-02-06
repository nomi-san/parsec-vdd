#include <windows.h>

#include "common.h"

#pragma once

void start(const HANDLE &vdd);

bool process_request(const HANDLE &vdd, const vdd_switcher::Request &request, bool &vd);
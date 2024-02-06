cmake_minimum_required(VERSION 3.5)

project(vddswitcher VERSION 0.2.0 LANGUAGES C CXX)

add_executable(vddswitcherd vddswitcherd.cpp)
add_executable(vddswitcher vddswitcher.cpp)

target_link_libraries(vddswitcherd setupapi.lib cfgmgr32.lib)

if(HIDE_CONSOLE)
    target_compile_definitions(vddswitcherd PRIVATE -DHIDE_CONSOLE)
endif()

set(CPACK_PROJECT_NAME ${PROJECT_NAME})
set(CPACK_PROJECT_VERSION ${PROJECT_VERSION})
include(CPack)

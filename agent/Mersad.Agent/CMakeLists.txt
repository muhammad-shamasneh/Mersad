cmake_minimum_required(VERSION 3.20)

project(Mersad.Agent LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(
    Mersad.Agent
    src/main.cpp
    src/HttpClient.cpp
    src/DeviceManager.cpp
    src/ProcessCollector.cpp
    src/TelemetryClient.cpp
)

target_include_directories(
    Mersad.Agent
    PRIVATE
    include
)
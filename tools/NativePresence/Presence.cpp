// BedrockLoader's optional ordinary Windows x64 preload mod. No game ABI required.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <restartmanager.h>
#include <sddl.h>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>
namespace fs = std::filesystem;

static bool ownedLock(const fs::path& file) {
    // Only ask Restart Manager about existing locks with an active file owner.
    HANDLE probe = CreateFileW(file.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (probe != INVALID_HANDLE_VALUE) { CloseHandle(probe); return false; }
    if (GetLastError() != ERROR_SHARING_VIOLATION) return false;
    DWORD session;
    WCHAR key[CCH_RM_SESSION_KEY + 1]{};
    if (RmStartSession(&session, 0, key) != ERROR_SUCCESS) return false;
    LPCWSTR name = file.c_str();
    RM_PROCESS_INFO apps[16]{};
    UINT needed = 0, count = 16;
    DWORD reason = 0;
    bool owned = false;
    if (RmRegisterResources(session, 1, &name, 0, nullptr, 0, nullptr) == ERROR_SUCCESS &&
        RmGetList(session, &needed, &count, apps, &reason) == ERROR_SUCCESS) {
        for (UINT i = 0; i < count; ++i)
            if (apps[i].Process.dwProcessId == GetCurrentProcessId()) owned = true;
    }
    RmEndSession(session);
    return owned;
}
static bool directory(const fs::path& path) {
    DWORD attr = GetFileAttributesW(path.c_str());
    return attr != INVALID_FILE_ATTRIBUTES && (attr & FILE_ATTRIBUTE_DIRECTORY) && !(attr & FILE_ATTRIBUTE_REPARSE_POINT);
}
static std::string frame(const fs::path& roaming) {
    const std::string unknown = R"({"Protocol":1,"Kind":"unknown"})";
    std::error_code ec;
    for (auto game : {L"Minecraft Bedrock", L"Minecraft Bedrock Preview"}) {
        fs::path users = roaming / game / L"Users";
        if (!directory(users)) continue;
        for (const auto& player : fs::directory_iterator(users, ec)) {
            auto worlds = player.path() / L"games/com.mojang/minecraftWorlds";
            if (!directory(player.path()) || !directory(worlds)) continue;
            for (const auto& world : fs::directory_iterator(worlds, ec)) {
                if (!directory(world.path()) || !ownedLock(world.path() / L"db/LOCK")) continue;
                std::ifstream input(world.path() / L"levelname.txt", std::ios::binary);
                char buf[192]{}; input.getline(buf, sizeof(buf));
                std::string name, raw(buf);
                if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, raw.data(), (int)raw.size(), nullptr, 0)>0) {
                    for (unsigned char c : raw) {
                        if (c < 32) continue;
                        if (c == '"' || c == '\\') name += '\\';
                        name += char(c);
                    }
                }
                return "{\"Protocol\":1,\"Kind\":\"local\",\"World\":\""+name+"\"}";
            }
        }
    }
    return unknown;
}
static DWORD WINAPI serve(void*) {
    HMODULE pin;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN, (LPCWSTR)&serve, &pin)) return 0;
    wchar_t appData[32768];
    DWORD chars = GetEnvironmentVariableW(L"APPDATA", appData, 32768);
    if (chars == 0 || chars >= 32768) return 0;
    const fs::path root(appData);
    auto name = L"\\\\.\\pipe\\LOCAL\\BedrockLoader.Presence.v1." + std::to_wstring(GetCurrentProcessId());
    PSECURITY_DESCRIPTOR sd = nullptr;
    if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(L"D:P(A;;GA;;;OW)", SDDL_REVISION_1, &sd, nullptr)) return 0;
    SECURITY_ATTRIBUTES security{sizeof(security), sd, FALSE};
    HANDLE pipe = CreateNamedPipeW(name.c_str(), PIPE_ACCESS_DUPLEX | FILE_FLAG_FIRST_PIPE_INSTANCE,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS, 1, 8192, 512, 0, &security);
    LocalFree(sd);
    if (pipe == INVALID_HANDLE_VALUE) return 0;
    for (;;) {
        if (!ConnectNamedPipe(pipe,nullptr) && GetLastError()!=ERROR_PIPE_CONNECTED) {DisconnectNamedPipe(pipe); Sleep(1000); continue;}
        for (;;) {
            if (!PeekNamedPipe(pipe,nullptr,0,nullptr,nullptr,nullptr)) break;
            std::string data;
            try {data=frame(root);} catch (...) {data=R"({"Protocol":1,"Kind":"unknown"})";}
            DWORD size=(DWORD)data.size(), written;
            if (!WriteFile(pipe,&size,sizeof(size),&written,nullptr) || written!=sizeof(size) ||
                !WriteFile(pipe,data.data(),size,&written,nullptr) || written!=size) break;
            Sleep(5000);
        }
        DisconnectNamedPipe(pipe);
    }
}
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(instance);
        if (HANDLE worker=CreateThread(nullptr,0,serve,nullptr,0,nullptr)) CloseHandle(worker);
    }
    return TRUE;
}

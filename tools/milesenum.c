/* Lists the Miles 3D providers the game's Mss32.dll finds (run from the game folder). */
#include <windows.h>
#include <stdio.h>

typedef int (__stdcall *startup_t)(void);
typedef int (__stdcall *enum3d_t)(unsigned *next, unsigned *dest, char **name);
typedef int (__stdcall *open3d_t)(unsigned lib);

int main(void)
{
    HMODULE m = LoadLibraryA("mss32.dll");
    startup_t startup;
    enum3d_t enum3d;
    open3d_t open3d;
    unsigned next = 0, prov;
    char *name;

    if (!m) { printf("mss32.dll not found\n"); return 1; }
    startup = (startup_t)GetProcAddress(m, "_AIL_startup@0");
    enum3d = (enum3d_t)GetProcAddress(m, "_AIL_enumerate_3D_providers@12");
    open3d = (open3d_t)GetProcAddress(m, "_AIL_open_3D_provider@4");
    if (!startup || !enum3d) { printf("exports missing\n"); return 1; }
    startup();
    while (enum3d(&next, &prov, &name))
        printf("%-50s open=%d\n", name, open3d ? open3d(prov) : -1);
    return 0;
}

#pragma once

#include <cstdint>

#if defined(__cplusplus)
extern "C" {
#endif

typedef struct PavoEglOwner PavoEglOwner;

PavoEglOwner* pavo_egl_create(void* native_window);
int pavo_egl_make_current(PavoEglOwner* owner);
int pavo_egl_suspend(PavoEglOwner* owner);
int pavo_egl_resume(PavoEglOwner* owner, void* native_window);
int pavo_egl_resize(PavoEglOwner* owner, int* width, int* height);
int pavo_egl_begin_frame(PavoEglOwner* owner, float red, float green, float blue, float alpha);
int pavo_egl_swap(PavoEglOwner* owner);
void pavo_egl_destroy(PavoEglOwner* owner);
const char* pavo_egl_last_error(const PavoEglOwner* owner);

#if defined(__cplusplus)
}
#endif

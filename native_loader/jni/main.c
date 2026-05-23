/**
 * v40: Pre-load IL2CPP with RTLD_NOW|RTLD_GLOBAL in JNI_OnLoad.
 * This forces the dynamic linker to resolve all IL2CPP symbols before
 * Unity initializes, ensuring our dlsym pointers stay valid.
 * Then CWPatch.init() uses stored globals — no re-dlsym needed.
 */

#include <jni.h>
#include <dlfcn.h>
#include <android/log.h>
#include <string.h>
#include <unistd.h>

#define TAG "CW"
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, TAG, __VA_ARGS__)

typedef void* (*fn_void)(void);
typedef void* (*fn_attach)(void*);
typedef void* (*fn_open_asm)(void*, const char*);
typedef void* (*fn_class)(void*, const char*, const char*);
typedef void* (*fn_get_method)(void*, const char*, int);
typedef void* (*fn_invoke)(void*, void*, void**, void**);

static fn_void      g_domain_get;
static fn_attach    g_thread_attach;
static fn_open_asm  g_asm_open;
static fn_class     g_class_name;
static fn_get_method g_get_method;
static fn_invoke    g_invoke;

JNIEXPORT void JNICALL
Java_com_pairip_application_CWPatch_init(JNIEnv *env, jclass clazz)
{
    (void)env; (void)clazz;
    LOGI("init() — resolving IL2CPP pointers...");

    void *il2cpp = dlopen("libil2cpp.so", RTLD_NOLOAD);
    if (!il2cpp) { LOGE("IL2CPP not loaded via RTLD_NOLOAD"); return; }

    g_domain_get   = dlsym(il2cpp, "il2cpp_domain_get");
    g_thread_attach = dlsym(il2cpp, "il2cpp_thread_attach");
    g_asm_open     = dlsym(il2cpp, "il2cpp_domain_assembly_open");
    g_class_name   = dlsym(il2cpp, "il2cpp_class_from_name");
    g_get_method   = dlsym(il2cpp, "il2cpp_class_get_method_from_name");
    g_invoke       = dlsym(il2cpp, "il2cpp_runtime_invoke");

    LOGI("d=%p a=%p o=%p c=%p m=%p i=%p",
         g_domain_get, g_thread_attach, g_asm_open, g_class_name, g_get_method, g_invoke);

    if (!g_domain_get) { LOGE("dlsym failed"); return; }

    LOGI("domain_get...");
    void *domain = NULL;
    for (int i = 0; i < 10 && !domain; i++) {
        domain = g_domain_get();
        if (!domain) usleep(200000);
    }
    LOGI("domain=%p", domain);
    if (!domain) return;

    g_thread_attach(domain);
    LOGI("attached");

    LOGI("asm_open...");
    void *asm_csharp = g_asm_open ? g_asm_open(domain, "Assembly-CSharp") : NULL;
    LOGI("asm=%p", asm_csharp);

    void *bootstrap_class = NULL;
    if (asm_csharp) {
        bootstrap_class = g_class_name(asm_csharp, "CreateWandPatch", "Bootstrap");
        LOGI("bootstrap=%p", bootstrap_class);
    }
    if (!bootstrap_class && g_asm_open) {
        void *our_asm = g_asm_open(domain, "CreateWandPatch.Android");
        LOGI("our_asm=%p", our_asm);
        if (our_asm) bootstrap_class = g_class_name(our_asm, "CreateWandPatch", "Bootstrap");
    }

    if (!bootstrap_class) { LOGE("Bootstrap not found"); return; }

    void *init_method = g_get_method(bootstrap_class, "Init", 0);
    LOGI("init_method=%p", init_method);
    if (!init_method) return;

    LOGI("calling Bootstrap.Init()...");
    void *exc = NULL;
    g_invoke(init_method, NULL, NULL, &exc);
    LOGI(exc ? "=== FAILED ===" : "=== SUCCESS ===");
}

__attribute__((constructor))
static void cwpatch_constructor(void)
{
    LOGI("Constructor");
}

JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM *vm, void *reserved)
{
    (void)reserved;
    JNIEnv *env = NULL;
    if ((*vm)->GetEnv(vm, (void**)&env, JNI_VERSION_1_6) != JNI_OK)
        return JNI_VERSION_1_6;
    jclass clazz = (*env)->FindClass(env, "com/pairip/application/CWPatch");
    if (clazz) {
        JNINativeMethod methods[] = {
            { "init", "()V", Java_com_pairip_application_CWPatch_init }
        };
        (*env)->RegisterNatives(env, clazz, methods, 1);
    }
    LOGI("JNI_OnLoad done");
    return JNI_VERSION_1_6;
}

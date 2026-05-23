/**
 * Shellcode injected into libil2cpp.so.
 * Uses bl (branch-and-link) to call IL2CPP functions within the same .so.
 * PC-relative offsets are patched at injection time by inject_full.py.
 *
 * Each BL instruction encodes: offset = (target - PC) / 4
 * Since both shellcode and targets are in the same .so, these offsets are
 * fixed at injection time (file-offset-based, ASLR-immune).
 */

#define NOP  __asm__ volatile("nop")

__attribute__((section(".text.shellcode")))
void cwpatch_shellcode(void)
{
    static int g_done;
    if (g_done) return;
    g_done = 1;

    void *domain;

    // BL il2cpp_domain_get -> domain in x0
    // This BL will be patched with the actual offset
    __asm__ volatile(
        "bl 0f\n"
        "0:\n"
        : : : "x0", "x30"
    );
    __asm__ volatile("" : "=r"(domain) : : );
    // Patch marker: BL_DOMAIN_GET will be overwritten
    // Actual: mov x19, x0 (save domain)
    if (!domain) goto done;
    void *d = domain;

    // BL il2cpp_thread_attach(d)
    __asm__ volatile("mov x0, %0" : : "r"(d) : "x0");
    // Patch marker: BL_THREAD_ATTACH
    NOP;

    // BL il2cpp_domain_assembly_open(d, "Assembly-CSharp")
    __asm__ volatile("mov x0, %0" : : "r"(d) : "x0");
    __asm__ volatile("adr x1, str_asm" : : : "x1");
    // Patch marker: BL_ASM_OPEN
    NOP;
    void *asm_csharp;
    __asm__ volatile("" : "=r"(asm_csharp) : : );

    void *bootstrap_class = 0;
    if (asm_csharp) {
        // BL il2cpp_class_from_name(asm_csharp, "CreateWandPatch", "Bootstrap")
        __asm__ volatile("mov x0, %0" : : "r"(asm_csharp) : "x0");
        __asm__ volatile("adr x1, str_cw" : : : "x1");
        __asm__ volatile("adr x2, str_bootstrap" : : : "x2");
        // Patch marker: BL_CLASS_FROM_NAME
        NOP;
        __asm__ volatile("" : "=r"(bootstrap_class) : : );
    }

    if (!bootstrap_class) {
        // BL il2cpp_domain_assembly_open(d, "CreateWandPatch.Android")
        __asm__ volatile("mov x0, %0" : : "r"(d) : "x0");
        __asm__ volatile("adr x1, str_our_asm" : : : "x1");
        // Patch marker: BL_ASM_OPEN2
        NOP;
        void *our_asm;
        __asm__ volatile("" : "=r"(our_asm) : : );
        if (our_asm) {
            __asm__ volatile("mov x0, %0" : : "r"(our_asm) : "x0");
            __asm__ volatile("adr x1, str_cw" : : : "x1");
            __asm__ volatile("adr x2, str_bootstrap" : : : "x2");
            // Patch marker: BL_CLASS_FROM_NAME2
            NOP;
            __asm__ volatile("" : "=r"(bootstrap_class) : : );
        }
    }

    if (!bootstrap_class) goto done;

    // BL il2cpp_class_get_method_from_name(bootstrap_class, "Init", 0)
    void *init_method;
    __asm__ volatile("mov x0, %0" : : "r"(bootstrap_class) : "x0");
    __asm__ volatile("adr x1, str_init" : : : "x1");
    __asm__ volatile("mov x2, #0" : : : "x2");
    // Patch marker: BL_GET_METHOD
    NOP;
    __asm__ volatile("" : "=r"(init_method) : : );

    if (!init_method) goto done;

    // BL il2cpp_runtime_invoke(init_method, 0, 0, 0)
    __asm__ volatile("mov x0, %0" : : "r"(init_method) : "x0");
    __asm__ volatile("mov x1, #0" : : : "x1");
    __asm__ volatile("mov x2, #0" : : : "x2");
    __asm__ volatile("mov x3, #0" : : : "x3");
    // Patch marker: BL_INVOKE
    NOP;

done:
    __asm__ volatile(
        "b .+8\n"
        "str_asm: .asciz \"Assembly-CSharp\"\n"
        "str_cw: .asciz \"CreateWandPatch\"\n"
        "str_bootstrap: .asciz \"Bootstrap\"\n"
        "str_our_asm: .asciz \"CreateWandPatch.Android\"\n"
        "str_init: .asciz \"Init\"\n"
    );
}

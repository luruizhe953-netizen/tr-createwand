LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)
LOCAL_MODULE := cwpatch
LOCAL_SRC_FILES := main.c
LOCAL_LDLIBS := -llog -ldl
LOCAL_CFLAGS := -fPIC -O2 -std=c11
LOCAL_LDFLAGS := -Wl,--unresolved-symbols=ignore-all
include $(BUILD_SHARED_LIBRARY)

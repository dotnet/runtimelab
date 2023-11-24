// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Runtime.InteropServices;
using Java.Interop;
using Android.NativeAOT;
using System.Diagnostics;

public static class Program
{
    static JniRuntime? runtime;
    static IntPtr invocationPointer;
    
    // Retrieve JavaVM invocation pointer from JNI_OnLoad
    [UnmanagedCallersOnly(EntryPoint="JNI_OnLoad")]
    static int jniOnLoad(IntPtr vm, IntPtr unused)
    {
        invocationPointer = vm;
        return (int) JniVersion.v1_6;
    }

    [UnmanagedCallersOnly(EntryPoint="Java_net_dot_MonoRunner_initRuntime")]
    static int initRuntime (IntPtr env, IntPtr klass)
    {
        try {
            // Initialize Java interop and runtime
            runtime = new AndroidRuntime(env, invocationPointer);
            return Main (null);
        } catch (Exception e) {
			Console.Error.WriteLine ($"JNI_OnLoad: error: {e}");
			return 0;
		}
    }

    private static int counter = 0;

    private static void SetText(IntPtr env, string txt)
    {
        var envp = new JniTransition (env);
        try {
            var jclass = JniEnvironment.Types.FindClass("net/dot/MonoRunner");
            var methodId = JniEnvironment.StaticMethods.GetStaticMethodID (jclass, "setText", "(Ljava/lang/String;)V");
            unsafe {
                JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
                    new JniArgumentValue (JniEnvironment.Strings.NewString (txt)),
                };
                JniEnvironment.StaticMethods.CallStaticVoidMethod (jclass, methodId, parameters);
            }
        } catch (Exception e) {
			Console.Error.WriteLine ($"JNI_OnLoad: error: {e}");
            envp.SetPendingException (e);
		} finally {
            envp.Dispose();
        }
    }

    [UnmanagedCallersOnly(EntryPoint="Java_net_dot_MonoRunner_OnButtonClick")]
    static void OnButtonClick(IntPtr env, IntPtr klass)
    {
        string str = "OnButtonClick! #" + counter++;
        SetText(env, str);
    }

    public static int Main(string[] args)
    {
        Console.WriteLine("Hello from C#!"); // logcat
        return 42;
    }
}

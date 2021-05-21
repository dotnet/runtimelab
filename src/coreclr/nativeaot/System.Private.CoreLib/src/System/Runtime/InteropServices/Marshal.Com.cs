// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

using Internal.Reflection.Augments;

namespace System.Runtime.InteropServices
{
    public static partial class Marshal
    {
        private static readonly Guid IID_IDispatch = new Guid("00020400-0000-0000-C000-000000000046");
        private const int DISP_E_PARAMNOTFOUND = unchecked((int)0x80020004);

        public static int GetHRForException(Exception? e)
        {
            return PInvokeMarshal.GetHRForException(e);
        }

        public static bool AreComObjectsAvailableForCleanup() => false;

        [SupportedOSPlatform("windows")]
        public static IntPtr CreateAggregatedObject(IntPtr pOuter, object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [RequiresUnreferencedCode("Built-in COM support is not trim compatible", Url = "https://aka.ms/dotnet-illink/com")]
        [SupportedOSPlatform("windows")]
        public static object BindToMoniker(string monikerName)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static void CleanupUnusedObjectsInCurrentContext()
        {
        }

        [SupportedOSPlatform("windows")]
        public static IntPtr CreateAggregatedObject<T>(IntPtr pOuter, T o) where T : notnull
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static object? CreateWrapperOfType(object? o, Type t)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static TWrapper CreateWrapperOfType<T, TWrapper>(T? o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static void ChangeWrapperHandleStrength(object otp, bool fIsWeak)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static int FinalReleaseComObject(object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static IntPtr GetComInterfaceForObject(object o, Type T)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            if (T is null)
            {
                throw new ArgumentNullException(nameof(T));
            }

            return ComWrappers.ComInterfaceForObject(o, new Guid(T.GetCustomAttribute<GuidAttribute>().Value));
        }

        [SupportedOSPlatform("windows")]
        public static IntPtr GetComInterfaceForObject(object o, Type T, CustomQueryInterfaceMode mode)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static IntPtr GetComInterfaceForObject<T, TInterface>([DisallowNull] T o)
        {
            return GetComInterfaceForObject(o!, typeof(T));
        }

        [SupportedOSPlatform("windows")]
        public static object? GetComObjectData(object obj, object key)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static IntPtr GetIDispatchForObject(object o)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            return ComWrappers.ComInterfaceForObject(o, IID_IDispatch);
        }

        [SupportedOSPlatform("windows")]
        public static IntPtr GetIUnknownForObject(object o)
        {
            return ComWrappers.ComInterfaceForObject(o);
        }

        [SupportedOSPlatform("windows")]
        public static unsafe void GetNativeVariantForObject(object? obj, IntPtr pDstNativeVariant)
        {
            Variant* data = (Variant*)pDstNativeVariant;
            if (obj == null)
            {
                data->VariantType = VarEnum.VT_EMPTY;
                return;
            }

            if (obj is bool flag)
            {
                data->AsBool = flag;
            }
            else if (obj is byte b)
            {
                data->AsUi1 = b;
            }
            else if (obj is sbyte sb)
            {
                data->AsI1 = sb;
            }
            else if (obj is short s)
            {
                data->AsI2 = s;
            }
            else if (obj is ushort us)
            {
                data->AsUi2 = us;
            }
            else if (obj is int i)
            {
                data->AsI4 = i;
            }
            else if (obj is uint ui)
            {
                data->AsUi4 = ui;
            }
            else if (obj is long l)
            {
                data->AsI8 = l;
            }
            else if (obj is ulong ul)
            {
                data->AsUi8 = ul;
            }
            else if (obj is float f)
            {
                data->AsR4 = f;
            }
            else if (obj is double d)
            {
                data->AsR8 = d;
            }
            else if (obj is DateTime date)
            {
                data->AsDate = date;
            }
            else if (obj is decimal dec)
            {
                data->AsDecimal = dec;
            }
            else if (obj is char c)
            {
                data->AsUi2 = c;
            }
            else if (obj is string str)
            {
                data->AsBstr = str;
            }
            else if (obj is BStrWrapper strWrapper)
            {
                data->AsBstr = strWrapper.WrappedObject;
            }
            else if (obj is CurrencyWrapper currWrapper)
            {
                data->AsCy = currWrapper.WrappedObject;
            }
            else if (obj is UnknownWrapper unkWrapper)
            {
                data->AsUnknown = unkWrapper.WrappedObject;
            }
            else if (obj is DBNull)
            {
                data->SetAsNULL();
            }
            else if (obj is System.Reflection.Missing)
            {
                data->AsError = DISP_E_PARAMNOTFOUND;
            }
            else
            {
                data->AsDispatch = obj;
            }
        }

        [SupportedOSPlatform("windows")]
        public static void GetNativeVariantForObject<T>(T? obj, IntPtr pDstNativeVariant)
        {
            GetNativeVariantForObject((object?)obj, pDstNativeVariant);
        }

        [SupportedOSPlatform("windows")]
        public static object GetTypedObjectForIUnknown(IntPtr pUnk, Type t)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static object GetObjectForIUnknown(IntPtr pUnk)
        {
            return ComWrappers.ComObjectForInterface(pUnk);
        }

        [SupportedOSPlatform("windows")]
        public static unsafe object? GetObjectForNativeVariant(IntPtr pSrcNativeVariant)
        {
            if (pSrcNativeVariant == IntPtr.Zero)
            {
                return null;
            }

            Variant* data = (Variant*)pSrcNativeVariant;

            return data->ToObject();
        }

        [return: MaybeNull]
        [SupportedOSPlatform("windows")]
        public static T GetObjectForNativeVariant<T>(IntPtr pSrcNativeVariant)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static object?[] GetObjectsForNativeVariants(IntPtr aSrcNativeVariant, int cVars)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static T[] GetObjectsForNativeVariants<T>(IntPtr aSrcNativeVariant, int cVars)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static int GetStartComSlot(Type t)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static int GetEndComSlot(Type t)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        internal static Type? GetTypeFromCLSID(Guid clsid, string? server, bool throwOnError)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.GetTypeFromCLSID(clsid, server, throwOnError);
        }

        [SupportedOSPlatform("windows")]
        public static string GetTypeInfoName(ITypeInfo typeInfo)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static object GetUniqueObjectForIUnknown(IntPtr unknown)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static bool IsComObject(object o)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            return false;
        }

        public static bool IsTypeVisibleFromCom(Type t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }
            return false;
        }

        [SupportedOSPlatform("windows")]
        public static int ReleaseComObject(object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [SupportedOSPlatform("windows")]
        public static bool SetComObjectData(object obj, object key, object? data)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }
    }
}

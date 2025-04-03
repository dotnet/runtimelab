# Swift code generation

Certain parts of the Swift ABI are poorly documented and too complex to project directly to .NET, such as metadata, async contexts, and dynamic dispatch thunks. In these cases, Swift wrappers offer the flexibility to encapsulate these poorly documented ABI components within Swift code, enabling more maintainable C#/Swift interop boundary.

The main tradeoff is between more maintainable C#/Swift interop boundary and the increased cost of shipping and trimming. To minimize complexity, we propose generating Swift wrappers only when absolutely necessary, keeping them as thin as possible.

Below, we outline the specific scenarios where Swift wrappers are required.

## Async

Swift async function is projected into a function with C# Task. For each Swift function, there is a Swift wrapper function that calls into C# callback upon completion of the async operation. Here is an example:

Swift code:
```swift
public func waitFor(seconds: UInt64) async -> Int32 {
    do {
        try await Task.sleep(nanoseconds: seconds * 1_000_000_000)
    } catch {
        print("Failed to sleep: \(error)")
        return -1
    }
    return returnValue
}
```

Generated Swift wrapper:
```swift
public struct TimerStruct {
    let returnValue: Int32
    
    public init(returnValue: Int32) {
        self.returnValue = returnValue
    }
    
    public func waitFor(seconds: UInt64) async -> Int32 {
        do {
            try await Task.sleep(nanoseconds: seconds * 1_000_000_000)
        } catch {
            print("Failed to sleep: \(error)")
            return -1
        }
        return returnValue
    }
}
```

Generated C# code:
```csharp
private static unsafe delegate* unmanaged[Cdecl]<Int32, IntPtr, void> s_waitForCallback = &waitForOnComplete;
[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
private static void waitForOnComplete(Int32 result, IntPtr task)
{
    GCHandle handle = GCHandle.FromIntPtr(task);
    try
    {
        if (handle.Target is TaskCompletionSource<Int32> tcs)
        {
            tcs.TrySetResult(result);
        }
    }
    finally
    {
        handle.Free();
    }
}
public unsafe Task<Int32> waitFor( UInt64 seconds)
{
    TaskCompletionSource<Int32> task = new TaskCompletionSource<Int32>();
    GCHandle handle = GCHandle.Alloc(task, GCHandleType.Normal);

    var self = new SwiftSelf((void*)_payload);
    PInvoke_waitFor((IntPtr)s_waitForCallback, IntPtr.Zero, GCHandle.ToIntPtr(handle), seconds, self);
    return task.Task;
}

[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
[DllImport(EntryPoint = "waitFor_async")]
private static extern void PInvoke_waitFor(IntPtr callback, IntPtr context, IntPtr task,  UInt64 seconds,  SwiftSelf self);
```
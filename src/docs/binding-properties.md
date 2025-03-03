# Properties

## Simple Properties on Swift Structs

Properties in Swift structs have different calling conventions depending on whether the struct is frozen or not, and whether it's a getter or setter:

- Getters:
  - For frozen structs: Take the struct value lowered into registers
  - For non-frozen structs: Take a pointer to the struct instance
  
- Setters:
  - For both frozen and non-frozen structs: Always take a pointer to self and a value to set
  - The pointer to self is required because setters need to modify the struct's memory in-place

This means that when binding properties to C#, we need to handle these different cases appropriately:

- For frozen struct getters, we can pass `SwiftSelf<T>`.
- For non-frozen struct getters, we need to pass `SwiftSelf` pointer.
- For all setters, we must always pass the new value and `SwiftSelf` pointer.

Example of how this affects the binding:

```swift
@frozen public struct FrozenPoint {
    public var x: Int
}

public struct NonFrozenPoint {
    public var x: Int
}
```

The generated C# binding needs to handle the different calling conventions:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct FrozenPoint {
    nint x;

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport("MySwiftModule", EntryPoint = "...")]
    private static extern nint PInvoke_get_x(SwiftSelf<FrozenPoint> self);
    
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport("MySwiftModule", EntryPoint = "...")]
    private static extern void PInvoke_set_x(nint value, SwiftSelf self);

    private unsafe nint Get_x()
    {
        var self = new SwiftSelf<FrozenPoint>(instance);
        return PInvoke_get_x(self);
    }

    private unsafe void Set_x(nint value)
    {
        fixed (FrozenPoint* p = &this)
        {
            var self = new SwiftSelf(p);
            PInvoke_set_x(value, self);
        }
    }

    public int X
    {
        get => Get_x();
        set => Set_x(nint value);
    }
}

public class NonFrozenPoint : IDisposable {
    private SwiftHandle _payload;

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport("MySwiftModule", EntryPoint = "...")]
    private static extern nint PInvoke_get_x(SwiftSelf self);
    
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport("MySwiftModule", EntryPoint = "...")]
    private static extern void PInvoke_set_x(nint value, SwiftSelf self);

    private unsafe nint Get_x()
    {
        var self = new SwiftSelf((void*)_payload);
        return PInvoke_get_x(self);
    }

    private unsafe void Set_x(nint value)
    {
        var self = new SwiftSelf((void*)_payload);
        PInvoke_set_x(value, self);
    }

    public int X
    {
        get => Get_x();
        set => Set_x(value);
    }
}
```

## Static Properties

TODO

## Async Properties

TODO

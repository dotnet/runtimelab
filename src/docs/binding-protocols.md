# Binding Protocols

Protocols in Swift fall into two categories that are handled similarly but are different enough to warrant their own sections.

Protocols are a vestige of Objective C and work very much like interfaces in C#. Protocols are a contract that a type has a set of methods available. Unlike ObjC, Swift implements protocols through a vtable called a protocol witness table. The layout of protocol witness tables is fragile and undefined.

The representation of protocols in most cases is a type called an Existential Container. This is more or less like a box. It consists of a 3 machine word payload followed by the type metadata for the type in the payload and then followed by 0 or more pointers to protocol witness tables. In swift there is a magic protocol, `Any` which has 0 witness tables.
The ordering of the protocols is done using lexical ordering of the full type name.

## Language Parity Mismatches

The only language parity mismatch is that Swift offers protocol composition through the `&` operator. For anything but return types, this can be handled with generic types with multiple constraints. If a swift function takes an argument of type `P1 & P2 & P3` this can be handled in C# by making the argument generic and added `where T: P1, P2, P3`. This cannot be applied to the return value though because when you have `public func foo() -> P1 & P2` in Swift you are saying that the function (callee) can choose the return type as long as it conforms to the protocols, whereas in C# if you write `T Foo<T>() : where T: P1 & P2` you are saying that the *caller* decides the type. This is problematic since there is no analog to this in C#. Solutions we can apply include returning `object` or picking one of the protocols and using that. Finally we could return a representation of the existential container in which we could put a method to extract the type as a given interface type. This essentially leaves the unboxing to the user. All of these solutions will have unique side effects, which will be discussed later.

## ABI Differences

There should be none. In Binding Tools for Swift, I created a representation of an existential container starting with an interface:
```csharp
public interface ISwiftExistentialContainer {
    IntPtr Data0 { get; set; }
    IntPtr Data1 { get; set; }
    IntPtr Data2 { get; set; }
    SwiftTypeMetadata ObjectMetadata { get; set; }
    IntPtr this [int index] { get; set; }
    int Count { get; }
    int Sizeof { get; }
    unsafe IntPtr CopyTo (IntPtr memory);
    void CopyTo <T>(ref T container) where T : ISwiftExistentialContainer;
}
```

and implemented 8 of these, assuming that 8 protocol compositions are enough for anyone. These types were generally only seen in P/Invokes.

There is a case where the calling convention is different even though it looks like it should be the same:
```swift
public protocol SomeProto {
    // ...
}

public func foo(a: SomeProto) {
    // ...
}

public func foo<T: SomeProto>(a: T) {
    // ...
}
```
In the first version of `foo` there is 1 argument which is an existential container. In the second, there are 3 arguments: a pointer to a, the type metadata for T, and the protocol witness table for SomeProto with respect to T.

## Runtime Differences
Since the protocol witness table is effectively a box, we need to pay attention to what's been boxed and make sure that we handle reference counting issues (if any).

## Idiomatic Differences
None beyond protocol composition.

## Accessibility
One thing that we would like to be able to do is project types that implement the protocol into C# and be consumed correctly. In addition, we would like to be able to project C# types that implement the corresponding interface into Swift. For most cases, this is straightforward, but there are some cases where this is impossible or creates unexpected results.

To project swift types into C#, this can be done by creating an interface for the protocol and a proxy class. The proxy class is dual purpose. It implements the C# interface and it either contains a C# implementation of the interface or it contained a handle to the Swift type. If it was a Swift type being used in C#, any calls to the interface would get redirected to Swift. If it was a C# type being called by Swift, the Swift code would be working with an instance of `EveryProtocol` and extension methods in Swift would redirect the protocol methods into C# via a simulated vtable. 

This works in nearly all of the cases that we care about. There is an issue which has to deal with referential transparency. When we provide a type to Swift, we are presenting `EveryProtocol` and if Swift code tries to cast it to other types or otherwise assume things about its provenance, it may surprisingly fail or surprisingly succeed. In the former case, Swift code will always see a type of `EveryProtocol` as the underlying type and might not operate correctly. In the latter case, casts that might (correctly) fail may succeed since `EveryProtocol` is exactly what it says on the box: it implements every protocol.

On the C# side, marshaling code looks at the type metadata and tries to hand you an instance of the actual projected swift type, but if that type wasn't bound then that will be impossible and you will get an instance of the proxy implementing the interface.

In the case of functions that return protocol compositions, that presents more challenges. If C# can identify the type correctly, it will act as expected, but if not and we take the route of returning an existential container, asking for the payload as any of the given protocols will result in n different proxies, one for each protocol.

To best understand the overall process, it's probably best to start with a simple example.
To start, here is the implementation of `EveryProtocol`
```swift
public final class EveryProtocol {
    public init() { }
}
```
And here is a simple protocol:
```swift
public protocol ReturnsFloat {
    func val() -> Float
}
```
Bindings Tools for Swift generates the following Swift code:
```swift

// this is the definition for the vtable
fileprivate struct ReturnsFloat_xam_vtable
{
    // this is a GCHandle from C#
    public var csVTHandle: OpaquePointer? = nil
    // this is the call to execute val in C#. The UnsafeRawPointer is the existential container making the call
    fileprivate var func0: (@convention(c)(OpaquePointer?, UnsafeRawPointer)->Float)?;
}
private var _vtable: ReturnsFloat_xam_vtable = MontyWSMFloat_xam_vtable();

// implement ReturnsFloat for EveryProtocol
extension EveryProtocol : ReturnsFloat
{
    public func val() -> Float
    {

        var selfProto: ReturnsFloat = self; // get the existential container
        return _vtable.func0!(_vtable.csVTHandle, &selfProto); // call into C#
    }
}

// set the vtable for this protocol
public func setReturnsFloat_xam_vtable(uvt: UnsafeRawPointer)
{
    let vt: UnsafePointer<MontyWSMFloat_xam_vtable> = fromIntPtr(ptr: uvt);

    _vtable = vt.pointee;
}

// implementation of a wrapper to call the existential protocol's val func
public func xamarin_ReturnsFloatDval(this: UnsafeMutablePointer<MontyWSMFloat>) -> Float
{
    return this.pointee.val();
}
```

From here, BTfS generates the following interface for the protocol and proxy:
```csharp
[SwiftProtocolType(typeof(ReturnsFloatXamProxy), "libProtocolTests.dylib",
        "$s13ProtocolTests13MontyWSMFloatMp")] // the last string is symbol for the protocol descriptor
[SwiftTypeName("ProtocolTests.MontyWSMFloat")]
public interface IReturnsFloat
{
    float Val();
}

public class ReturnsFloatXamProxy : BaseProxy, IReturnsFloat {
    static IntPtr protocolWitnessTable;
    IReturnsFloat xamarinImpl = null!;

    SwiftExistentialContainer1 xamarinContainer;
    static ReturnsFloatXamProxy()
    {
        // we actually need two vtables, one in Swift and one in C#.
        // the latter is so we can handle the restrictions on [UnmanagedCallersOnly]
        _SetLocalVTable(); // call into swift to set up the vtable
    }

    // constructor when the protocol is implemented in C#
    public  ReturnsFloatXamProxy(IReturnsFloat actualImplementation, EveryProtocol everyProtocol)
        : base(typeof(IReturnsFloat), everyProtocol)
    {
        xamarinImpl = actualImplementation;
        xamarinContainer = new SwiftExistentialContainer1(everyProtocol, ProtocolWitnessTable);
    }

    // constructor for when the protocol is implemented in Swift
    public  ReturnsFloatXamProxy(ISwiftExistentialContainer container)
        : base(typeof(IReturnsFloat), null)
    {
        xamarinContainer = new SwiftExistentialContainer1(container);
    }

    public float Val()
    {
        if (xamarinImpl != null) // if the actual implementation is in C#, call its version of Val
        {
            return xamarinImpl.Val();
        } else { // if the actual implementation is in Swift, call its version of Val
            unsafe {
                // register this proxy (if not already registered)
                var thisProxy = SwiftObjectRegistry.Registry.ProxyForInterface<IReturnsFloat>(this);

                // make an existential container
                var thisContainer =
                    new SwiftExistentialContainer1(((BaseProxy)thisProxy).ProxyExistentialContainer);

                var thisContainerPtr = &thisContainer;

                // call the Swift wrapper
                return NativeMethodsForIReturnsFloat.PImethod_IReturnsFloatXamarin_ReturnsloatDval(new IntPtr      (thisContainerPtr));
            }
        }
    }

    // this method will get called from Swift when EveryProtocol's Val method gets called
    static unsafe float _Receiver0(SwiftExistentialContainer1 *self)
    {
        return SwiftObjectRegistry.Registry.InterfaceForExistentialContainer<IReturnsFloat> (*self).Val();
    }

    static unsafe void _SetLocalVTable()
    {
        var localVT = new ReturnsFloatXamProxyLocalCSVTable();
        localVT.Func0 = _Receiver0;
        MontyWSMFloatXamProxyUnmanagedReceivers.SetVTable(localVT);
    }

    public static IntPtr ProtocolWitnessTable {
        get {
            if (protocolWirnessTable == IntPtr.Zero)
            {
                protocolWitnessTable = SwiftCore.ProtocolWitnessTableFromFile("libXamWrapping.dylib",
                    "$s7XamGlue13EveryProtocolC0D5Tests13MontyWSMFloat0A8WrappingMc", EveryProtocol.GetSwiftMetatype());
            }
            return protocolWitnessTable;
        }
    }
    
    public override ISwiftExistentialContainer ProxyExistentialContainer {
        get { return xamarinContainer; }
    }

    // this is the C# vtable
    internal class ReturnsFloatXamProxyLocalCSVTable
    {
        public unsafe delegate float Func0Delegate(SwiftExistentialContainer1 *self);

        public unsafe Func0Delegate? Func0;
    }

    // this is the Swift vtable
    internal struct ReturnsFloatXamProxySwiftVTable
    {
        public IntPtr csVTHandle; // handle to the C# vtable
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public unsafe delegate *unmanaged<IntPtr, IntPtr, float> Func0;
    }

    // this is a class to hold the [UnmanagedCallersOnly] receivers
    internal static class ReturnsFloatXamProxyUnmanagedReceivers
    {
        [UnmanagedCallersOnly()]
        static unsafe float _Receiver0(IntPtr _vtGCH, IntPtr self)
        {
            // retrieve the GC Handle
            var gch = GCHandle.FromIntPtr(_vtGCH);
            // get the local vtable (C#)
            var localVT = (ReturnsFloatXamProxyLocalCSVTable)(gch.Target!);
            // call it
            return localVT.Func0!((SwiftExistentialContainer1 *)self);
        }

        // set up local (C#) and Swift vtables
        public static unsafe void SetVTable(ReturnsFloatXamProxyLocalCSVTable vt)
        {
            var gch = GCHandle.Alloc(vt);
            var swiftVT = new ReturnsFloatXamProxySwiftVTable();
            swiftVT.csVTHandle = GCHandle.ToIntPtr(gch);
            swiftVT.Func0 = &_Receiver0;
            NativeMethodsForIReturnsFloat.SwiftXamSetVtable(&swiftVT);
        }
    }
}
```


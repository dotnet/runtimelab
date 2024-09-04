# Binding Classes

Classes function very similarly to C# classes with some important semantic and runtime differences which affect how they are exposed in C#, but generally speaking, they can exist as a 1:1 mapping. It should be noted that while Swift provides the ability to create traditional OO code, Apple is eschewing the use of classes in their own code and preferring value types instead. It is telling that in the main Swift runtime library, libswiftCore, there are no exported classes.

Swift classes can be either native Swift or Objective-C. The latter is in place strictly for compatability with legacy code and in fact, if a Swift class is declared to be `@ObjC`, it must inherit from `NSObject` and the swift compiler will generate a selector dispatcher for all methods marked as `@ObjC` but these just go to the Swift implementation, therefore Objective-C compatability is just a thin veneer on top of the Swift implementation.

## Language Parity Mismatches
Swift provides a different set of accessibility of methods that C#:
- open - callable by all and overridable (effectively public virtual)
- public - callable by all not overridable
- private - callable only by members of the class
- internal (default) - callable only by members of the module
- fileprivate - callable only members defined in the same file

For the most part, we only care about public and open as these two represent the public facing API of the class.
There is no `protected` (private but for inheritors) access which will create an issue for exposing C# types to Swift.

Swift has very specific rules for initialization. Under the hood, Swift breaks up the process of making an instance of a class into two pieces: allocation and initialization. Early on these were very much separate, but in the current swift runtime, the allocator will also call the initializer.

The allocating initializer takes the type metadata for the class in the `self` register. We can call this directly, however under enable-library-evolution, the swift compiler generates a dispatch thunk for this method and we should probably call that.

Swift divides initializers into two categories: designated and convenience. Designated initializers are initializers which fully initialize a class. If a subclass inherits from base class, it must implement all designated initializers and call the parent class. A convenience intializer will have a different signature than all desinated initializers and **must** call a designated initializer.

The generic programming model in Swift does specialization at runtime. It does this by creating the type metadata for the specialized type
using the Metadata Accessor function. The Metadata Accessor function has one type metadata argument for each specialized type in the generic class. It is **very important** that the binding code calls the Metadata Accessor rather than trying to synthesize the type metadata object. This is because from Swift's point of view, every type metadata object is a singleton and the runtime will cache generic specializations. 

Swift initializers can fail - this is something that doesn't have an exact analog in C#. There are two ways to handle this:
- make the corresponding C# constructor throw
- write a factory method that returns `BoundType?`

## ABI differences
- Instance methods get called with the `self` register pointing to the instance.
- Class methods get called with the `self` register pointing to the type metadata.
- Init methods get called with the type metadata in the first argument.
- Methods that throw return the error in the error register.
- Virtual methods in swift get called via a vtable. Since the ordering of the vtable is undefined, methods can't be called using the vtable from C#. In addition, the vtable is consider fragile and may change between versions of any given library. Fortunately, with enable-library-evolution set, the compiler will inject a dispatch thunk for each virtual method.


## Runtime Differences
Swift classes are reference counted rather than garbage collected. This can create some interesting conditions when an object from Swift surfaced in C# gets garbage collected, but the object may still be live in Swift, so it's necessary to manage this. The way this is handled in Binding Tools for Swift is to add it to an object registry which is a `Dictionary&lt;NativeHandle, GCHandle&gt;` The native handle is the Swift object instance and the GC Handle is made from the corresponding C# instance. When the pair is added to the registry, the registry also takes a weak reference to the Swift
object in order to insure that it doesn't away if it gets disposed.

Naturally, when the C# object gets disposed, the registry removes and does a weak release of the object.

## Idiomatic Differences
There are very few idiomatic differences between Swift classes and C# classes.

## Accessibility
This is where the true challenge lies.
Let's break Swift classes into two varieties: virtual and non-virtual. Non-virtual are easy because the implementation will be a `sealed` C# class for which each method would be a p/invoke into the Swift implementation.

Virtual classes are another matter.

The goal would be to have subclasses of the bound Swift class to be able to operate in Swift.

To do this, you need to write Swift code that overrides all the virtual methods in the class and delegates them out to C#. In Binding Tools for Swift, this is was done by created a simulated vtable in Swift and populating it from C#.

```swift
// original swift class
open class YourAge {
    public init () { }
    open func getAge() -> Int {
        return 7
    }
}

// simulated vtable:
fileprivate struct YourAge_VTable {
    var getAge: @convention(c) (OpaquePointer)-> Int
}

// simulated vtable instance
fileprivate var yourAgeVt: YourAge_Vtable? = nil

// code to call the vtable setter from C#
public func setYourAgeVt(p: OpaquePointer)
{
    let vt:UnsafePointer<YourAge_Vtable> = UnsafePointer(p);
    yourAgeVt = vt.pointee;
}

open class YourAgeWrapper : YourAge {
    var classHandle: OpaquePointer; // C# class handle
    public init(handle: OpaquePointer) {
        classHandle = handle;
        super.init()
    }
    open override func getAge() -> Int {
        if let vt = yourAgeVt {
            return vt.getAge(classHandle)
        } else {
            return super.getAge()
        }
    }
    public func superGetAge() -> Int {
        return super.getAge();
    }
}
```

Then in C# you need something like this:
```csharp
public class YourAge :  SwiftNativeObject { // SwiftNativeObject has a class handle and an instance handle and is IDisposable
    // set the vtable in the initializer
    static YourAge {
        SetVtable();
    }

    // parallel vtable structure
    struct YourAgeVt {
        public delegate *unmanaged<IntPtr, nint> GetAge;
    }

    // initialize a vtable and copy it into swift
    static void SetVtable()
    {
        unsafe {
            var vt = new YourAgeVt() {
                GetAge = &GetAgeReceiver;
            }
            SetYourAgeVt(ref vt);
        }
    }

    [DllImport(/* ... */)]
    static extern void SetYourAgeVt(ref YourAgeVt vt);

    [UnmanagedCallersOnly]
    static nint GetAgeReveiver(IntPtr handle)
    {
        var gch = GCHandle.FromIntPtr(handle);
        if (gch.Target is YourAge ya) {
            return ya.GetAge();
        }
        throw new NotImplementedException();
    }

    // actual implementations
    public YourAge() : base(DispatchThunkForAllocatingInit(YourAgeMetadataAccessor(), GCHandle.Alloc(this).ToIntPtr()), YourAgeMetadataAccessor())
    {
    }

    public virtual GetAge()
    {
        return SuperGetAge(SwiftHandle);
    }

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(/* ... */)]
    static extern IntPtr DispatchThunkForAllocatingInit(SwiftSelf<IntPtr> metadata, IntPtr handle);

    [DllImport(/* ... */)]
    static extern IntPtr YourAgeMetadataAccessor();

    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
    [DllImport(/* ... */)]
    static extern nint SuperGetAge(SwiftSelf<IntPtr> p);
}
```

What happens is that the wrapper class in swift defines a vtable type which has a C calling convention callback. It also defines a method to call the
super class implementation.

What that leaves us with a C# class that when called from C# will call the superclass implementation in Swift. When passed into swift, `getAge` will call the vtable, which will go into C#, get a GCHandle for the actual C# class and call its implementation of `GetAge`, which will then call the Swift superclass implementation.

If a user creates subclasses `YourAge` and overrides `GetAge`, that implementation will be used.

This gets more complicated for generic classes.

For generic classes you don't have a singleton vtable. Instead you need a vtable for every specialization of the class.

In addition, because of issues with the way that C# handles unmanaged callbacks, you can't have an unmanaged callback inside a generic class.
As a result it's necessary to create an unmanaged vtable and a managed vtable. 

For each unmanaged vtable function of the form `delegate *unmanaged<IntPtr, IntPtr, args..., Result>` there will be a managed delegate of the form
`Func<IntPtr, args, Result>`. The unmanaged vtable will also have an IntPtr which is a `GCHandle` to the managed table. The unmanaged delegate will include an `IntPtr` as the first argument which is the handle to the managed vtable.

Therefore a receiver in C# will look something like this:

```csharp
[UnmanagedCallersOnly]
public static Result SomeUnmanagedReceiver(IntPtr managedVTPtr, IntPtr csHandle, args...)
{
    var gch = GCHandle.FromIntPtr(managedVTPtr);
    if (gch.Target is ManagedVT mvt) {
        return mvt.SomeManagedReceiver(csHandle, args)
    }
}
```

One thing that this does not do is expose new members to Swift nor does it make protocol compliance visible in all circumstances.
For example, if I do something like this in C#:
```csharp
public class OneAge : YourAge, ISomeSwiftProtocol {
    public int ExtraProperty { get; set; }
    public void DoSomethingCool ()
    {
        TheBoundNamespace.SomeOtherFunc(this);
    }
}
```
and had this in Swift:
```swift
public func someOtherFunc(y: YourAge) {
    if let ssp = y as? SomeSwiftProtocol { // as called from C# this will never work since y is YourAgeWrapper
        ssp.ProtocolAction()
    }
}
```

The way to get this to work 100% in both directions is to have a reverse binding step that exposes the new C# members and protocol compliance in Swift.

It may seem tempting to synthesize the protocol witness table, a type object and a synthetic swift object, but this would require knowing the layout of the protocol witness tables as well as the layout of the actual vtables which we don't and can't know. In addition, both tables are fragile and susceptible to changes in the class and/or protocol(s).

Here is a complete example of an open class in swift with a virtual member. The code represents a Swift class which is open and should be exposed in C# as a class with a virtual method.

To do this, the type is overridden in Swift with code to vector into C# and to make all the superclass methods available.
The C# code creates two vtables: a local one with methods that get a C# instance from the Swift self pointer, and calls its implementation of the virtual method. If that method has *not* been overridden, then the C# code with call the Swift superclass method. If that method has been overridden by user code then the user code will get called.

It will look unusual that the C# code has 2 vtables: one in C# and one in Swift. The reason behind this is that in
generic classes, you can't have `[UnmanagedCallersOnly]` methods. It was easier to implement both generic and non-generic code uniformly.

```swift
open class ValBool {
    public init () { }
    open func val() -> Bool { return true; }
}
```
This class gets wrapped with the following Swift code:
```swift
public final class xam_sub_ValBool : ValBool {
    // swift side vtable that points to C# receiver with a handle
    // to the local C# vtable
    fileprivate struct MontyWSMBool_xam_vtable
    {
        public var csVTHandle: OpaquePointer? = nil;
        fileprivate var func0: (@convention(c)(OpaquePointer?, UnsafeRawPointer) -> Bool)?;
    }

    // if the C# class hasn't been initialized, then the vtable
    // won't be set.
    private var _xamclassIsInitialized: Bool = false
    // vtable for the virtual functions
    private static var _vtable: ValBool_xam_vtable = ValBool_xam_vtable()
    fileprivate static func set ValBool_xam_vtable(vtable: ValBool_xam_vtable)
    {
        _vtable = vtable;
    }

    public override init()
    {
        super.init();
        _xamarinClassIsinitialized = true;
    }

    // vector to super implementation
    fileprivate final func xam_super_val()-> Bool {
        return super.val()
    }

    public override func val() -> Bool {
        if (_xamarinClassIsInitialized && _vtable.func0 != nil) {
            return _vtable.func0!(_vtable.csVTHandle, toIntPtr(value: self))
        } else {
            return super.val()
        }
    }
}

// set the vtable to point to C# receivers
public func setValBool_xam_vtable(uvt: UnsafeRawPointer)
{
    let vt: UnsafePointer<xam_sub_ValBool.ValBool_xam_vtable> = fromIntPtr(ptr: uvt);
    xam_sub_ValBool.setValBool_xam_vtable(vtable: vt.pointee);
}

// constructor wrapper - this won't be needed with runtime support
public func xamarin_ValBoolDValBool() -> ValBool
{
    return xam_sub_ValBool();
}

// wrapper onto the super class implementation - this won't be needed with runtime support
public func xamarin_xam_sub_ValBoolDxam_super_val(this: xam_sub_MontyWSMBool) -> Bool
{
    return this.xam_super_val();
}
```
With this in place, the following C# wrapper gets written:

```csharp
[SwiftTypeName("OverrideTests.ValBool)]
public class ValBool : SwiftNativeObject {
    // pass the vtable to swift
    static ValBool()
    {
        _SetLocalVtable();
    }
    // type metadata accessor
    public static SwiftMetatype GetSwiftMetatype()
    {
        return PInvokes.PIMetadataAccessor(SwiftMetadataRequest.Complete);
    }
    // implementation of the native ctor - this lives here because some cases (with generics in particular)
    // need more work than can fit in the case to the base ctor
    static IntPtr _XamValBoolCtorImpl()
    {
        return PInvokes.PIValBool();
    }
    public ValBool()
        : base(_XamValBoolCtorImpl(), GetSwiftMetatype(), SwiftObjectRegistry.Registry)
    {
    }

    // a factory method that all class types implement
    // if C# gets passed an instance to a class and we can determine the
    // type, then this does the C# initialization without doing the Swift initialization
    public static MontyWSMBool XamarinFactory(IntPtr p)
    {
        return new MontyWSMBool(p, GetSwiftMetatype(), SwiftObjectRegistry.Registry);
    }

     ~MontyWSMBool()
    {
        Dispose(false);
    }

    // implementation of the superclass' Val method
    bool BaseVal()
    {
        var retval = default(bool);
        // call the super method
        retval = PInvokes.PImethod_Xam_sub_ValBoolXamarin_xam_sub_ValBoolDxam_super_val(thisIntPtr);
        return retval;
    }

    // default implementation calls the super class
    public virtual bool Val()
    {
        return BaseVal();
    }

    // this gets called by Swift to implement the val method
    // it returns nint instead of bool because [UnmanagedCallersOnly] doesn't like booleans
    static nint _Receiver0(IntPtr self)
    {
        return SwiftObjectRegistry.Registry.CSObjectForSwiftObject <MontyWSMBool> (self)!.Val()
            ? 1 : 0;
    }

    // initializes a local vtable with receivers
    static unsafe void _SetLocalVTable()
    {
        var localVT = new ValBoolLocalCSVTable();
        localVT.Func0 = _Receiver0;
        ValBoolUnmanagedReceivers.SetVTable(localVT);
    }

    // local vtable
    internal class ValBoolLocalCSVTable
    {
        public delegate nint Func0Delegate(IntPtr self);
        public unsafe Func0Delegate? Func0;
    }

    // swift vtable
    internal struct MontyWSMBoolSwiftVTable
    {
        // handle to the local vtable
        public IntPtr csVTHandle;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public unsafe delegate *unmanaged<IntPtr, IntPtr, nint> Func0;
    }

    // actual funcion(s) that go into the Swift vtable, callable from Swift
    internal static class ValBoolUnmanagedReceivers
    {
        [UnmanagedCallersOnly]
        static nint _Receiver0(IntPtr _vtGCH, IntPtr self)
        {
            // get the local vtable from the handle we get passed
            var gch = GCHandle.FromIntPtr(_vtGCH);
            var localVT = (ValBoolLocalCSVTable)(gch.Target!);
            return localVT.Func0!(self);
        }

        public static unsafe void SetVTable(ValBoolLocalCSVTable vt)
        {
            var gch = GCHandle.Alloc(vt);
            var swiftVT = new ValBoolSwiftVTable();
            swiftVT.csVTHandle = GCHandle.ToIntPtr(gch);
            swiftVT.Func0 = &_Receiver0;
            PInvokes.SwiftXamSetVtable(&swiftVT);
        }
    }    
}
```
# Binding Protocols With Associated Types (PATs)

Swift PATs are an entity that do not have a direct analog in C# and as such can feel very foreign to C# programmers. The main issue with PATs is that they are not actual types, at least not in the sense of the other types in Swift and are not simply generic interfaces.

[This is a great video](https://www.youtube.com/watch?v=XWoNjiSPqI8) about PATs. Even though it's based on an earlier version of Swift syntax, it still applies. When looking at the video, you'll see that he uses the keyword `typealias`, but in current Swift they use `associatedtype` instead. There is a paper which is referenced in this video [here](https://www.cambridge.org/core/services/aop-cambridge-core/content/view/C97D5964ECC2E651EEF9A70BC50600A6/S0956796806006198a.pdf/an_extended_comparative_study_of_language_support_for_generic_programming.pdf).

The easiest way to grasp onto how PATs work is to look at very simple examples. One such example is checking for equality. Consider this PAT:

```swift
public protocol Sameness {
    func isSame(as: Self) -> Bool
}
```

In this case `Self` is a special placeholder in Swift for "the type which has implemented this protocol". While not labeled as such, it *is* an associated type and in Swift. Apple's documentation on `Self` is [here](https://docs.swift.org/swift-book/documentation/the-swift-programming-language/types#Self-Type).

In adopting this protocol in Swift, you could do something like this:

```swift
public struct Imaginary : Sameness {
    private var r, i: Double
    public init (r: Double, i: Double) {
        self.r = r
        self.i = i
    }
    public func isSame(as: Self) {
        return r == as.r && i == as.i
    }
}

public func printSame<T:Sameness>(a: T, b: T) {
    let same = a.isSame(as: b)
    if same {
        print("a and b are the same.")
    } else {
        print("a and b are different.")
    }
}
```

Note that `printSame` declares neither `a` nor `b` as `Sameness` but instead uses a generic type constrained to `Sameness`. This is because `Sameness` is **not** an actual type. One way to look at a PAT is not so much as a type but rather a type-shaped hole.

And while it looks like this could be represented in C# as:

```csharp
public interface ISameness<T> {
    bool IsSame(T @as);
}
```
It's really closer to this:
```csharp
public interface ISameness<TSelf> where TSelf: ISameness<TSelf> {
    bool IsSame(TSelf @as);
}
```

This only gets more complicated as associated types are added and as those associated types have constraints on them. This is because the generic relationships and constraints are encapsulted in the PAT whereas in other languages the generic relationships and constraints need to be part of the declaration which uses it.

One of the nice things about PATs, is that it makes it much easier to write statically typed generic code. One example would be the canonical animal example (similar to the video referenced above):

```swift
public protocol Animal {
    associatedtype Food
    func feed(food: Food)
}

public class Grass { } 

public class Cow : Animal {
    public func feed(food: Grass) {
        print ("yummy grass")
    }
}

public func feedAnyAnimal<T: Animal>(animal: T, food: T.Food)
{
    animal.feed(food: food)
}
```

In C#, this will look something like this:
```csharp
public interface IAnimal<TSelf, TFood> where TSelf: IAnimal<TSelf, TFood> {
    void Feed(TFood food);
}

public class Grass { }

public class Cow :IAnimal<Cow, Grass> {
    public void Feed(Grass food)
    {
        Console.WriteLine ("yummy grass");
    }
}

public static class TopFuncs {
    public static void FeedAnyAnimal<T, U>(T animal, U food) where T:IAnimal<T, U> {
        animal.Feed(food);
    }
}
```

# Language Parity Mismatches

Obviously, C# doesn't have PATs and the closest we can get is to use generic interfaces. In addition, there is a language feature in Swift which is the keyword `some` which is used for return types that are PATs without having to specify the generic in the declaration. Think of it this was: in C# if you want to return a generic type from a method, it is the caller that determines what the expected return type will be. In Swift, `some` allows the *callee* not the caller to determine the specialization of the PAT. There is a way to model this in C#, and I'll explain that later.

# ABI Differences

# Runtime Differences

Because PATs inherently require a generic declaration, it means that the implementation details of the type comes along separately.

Recall that if you have this function in Swift:
```swift
public func areTangent<T: TangentialProto>(a: T, b: T) -> Bool { // TangentialProto is NOT a PAT
   // ...
}
```
there are two extra implicit arguments added to the function: the type metadata for `T` and the protocol witness table for `T` with respect to `TangentialProto`. This is also the case with PATs except that the protocol witness table is not necessarily known at compile time because. Swift therefore has a data structure called a Protocol Conformance Descriptor which describes the shape of the protocol witness table and given the type metadata for the associated type(s) can generate a protocol witness table for that specialization. There is some discussion from Apple on that matter in [this forum post](https://forums.swift.org/t/need-help-understanding-protocols-and-generics/37564/35).


# Accessibility

This is an excerpt from a document I wrote about how PATs are bound in Binding Tools for Swift. It should be noted that with NativeAOT we don't need the vtable, as outlined [here](vtable-alternative.md).

To start to understand the binding of PATs (protocols with associated types), it's easiest to start with a very simple PAT, `IteratorProtocol`:

```swift
public protocol IteratorProtocol {
    mutating func next() -> Element?
}
```

In BTfS, I created an interface to define the type in C# - this is an imperfect binding, but I'll get to that later.

```csharp
[SwiftTypeName ("Swift.IteratorProtocol")]
[SwiftProtocolType (typeof (SwiftIteratorProtocolProtocol<>), "libswiftCore.dylib", "$sStMp", true)]
public interface ISwiftIteratorProtocol<ATElement> {
		SwiftOptional<ATElement> Next ();
}
```

Taking this apart, the C# interface has the `SwiftTypeNameAttribute` on it which says "This type in Swift has a different name than how it was projected in C#. This is a nicety especially when the type has a name that is not supported in C#.

Next is the `SwiftProtocolTypeAttribute` which, given an interface, tells us what the name of the C# proxy type is, the name of the library where the protocol descriptor is defined and the symbol name of that descriptor and whether or not this is a PAT.

### Wrapping And Proxies

There are two cases that we need to manage:
- a C# type unknown to Swift implements `ISwiftIteratorProtocol`
- a Swift type unknown to C# implements `IteratorProtocol`
  
It should be noted that if the Swift type is known to C# and was bound knowing that it implements `IteratorProtocol` then the bound C# type will already implement `ISwiftIteratorProtocol`. This is effectively a trivial case. There is also the possibility that we have a type known to C# but at the time of binding we did not know that it adopts `IteratorProtocol` This can happen when protocol adoption happens through retroactive modeling (extension). This is not within the scope of this document.

Both of the cases listed above should be usable by the other language.

We accomplish this with proxy types. A proxy type is a type that can stand in the place of another type. This is an example of the [Gang of Four](https://en.wikipedia.org/wiki/Design_Patterns) pattern "[Adapter](https://en.wikipedia.org/wiki/Adapter_pattern)".

Every proxy in C# inherits from `BaseAssociatedTypeProxy`. This type inherits from `SwiftNativeObject`, but it is designed to be initialized from either a heap allocated Swift object or a Swift value type. This type manages the lifespan of the underlying object.

The proxies we create for PATs inherit from this type and are **dual purpose**. This means that it can act as a proxy for the C# type to Swift or the proxy for the Swift type to C#.

Let's consider the proxying of a Swift object first.

C# gets presented a generic Swift instance that implements `IteratorProtocol`. To work with it, we construct an instance of `SwiftIteratorProtocolProtocol<ATElement>` which implements the interface `ISwiftIteratorProtocol<ATElement>`.

This will have been constructed with some type defined in Swift.
To call the `next` method, we call this wrapper:

```swift
public func xamarin_static_wrapper_IteratorProtocol_next<T0, T1>(this: inout T0) ->
    Optional<T1> where T0 : IteratorProtocol, T1 == T0.Element
{
    return this.next();
}
```

Note that there are two generics here, `T0` and `T1`. `T0` is effectively the associated type `Self`. Unfortunately, when Binding Tools for Swift was created C# could't pinvoke to this directly, so there is a wrapper that can be called from C#, plus the Swift compiler will inject code to get the protocol witness table for us:

```swift
public func xamarin_XamWrappingFxamarin_static_wrapper_ProtocolTests_IteratorProtocol_next00000000<T0, T1>
                        (retval: UnsafeMutablePointer<Optional<T1>>, this: inout T0)
                                where T0 : IteratorProtocol, T1 == T0.Element
{
        retval.initialize(to: xamarin_static_wrapper_IteratorProtocol_next(this: &this));
}
```
This is done so that the caller will allocate space for the optional as the return value and pass the object by reference.
The pinvoke to call that wrapper looks like this:

```csharp
[DllImport (SwiftCore.kXamGlue, EntryPoint = "...")]
internal static extern void PImethod_SwiftIteratorProtocolProtocolXamarin_SwiftIteratorProtocolProtocolDnext00000001 (IntPtr retval, IntPtr this0);
```

Finally the actual implementation in the proxy looks like this:
```csharp
public SwiftOptional<ATElement> Next ()
{
    // removed the C# side for now
    unsafe {
        // allocate a C# version of the return value
        SwiftOptional<ATElement> retval = new SwiftOptional<ATElement> ();
        // PrepareValueType allocates a payload for the given value type if it hasn't
        // yet been allocated
        fixed (byte* retvalSwiftDataPtr = StructMarshal.Marshaler.PrepareValueType (retval)) {
            NativeMethodsForSwiftIteratorProtocolProtocol.PImethod_SwiftIteratorProtocolProtocolXamarin_SwiftIteratorProtocolProtocolDnext00000001 ((IntPtr)retvalSwiftDataPtr, thisIntPtr);
            return retval;
        }
    }
}

```
And that's it for the swift side.

For the C# goes to swift side, we need to make a swift proxy for this type. This proxy will delegate all calls to `Next` to C# code. In order to do this, we need a swift type that can call in C#. In order to do this, we're going to need a vtable. The vtable looks like this:

```swift
fileprivate struct SwiftIteratorProtocol_xam_vtable
{
    fileprivate var func0: (@convention(c)(UnsafeRawPointer, UnsafeRawPointer) -> ())?;
}
```
The contents is a single function pointer following C calling conventions that takes two pointers and returns nothing. It will point to an `[UnmanagedCallersOnly]` receiver in C#.

Now because we're dealing with generics, we can't get by with a single vtable for all usage. Instead, we could have any number of vtables. Because of that, we have a hashtable of vtables and accessors for that table:
```swift
fileprivate var _vtable: [TypeCacheKey : SwiftIteratorProtocol_xam_vtable]
    = [TypeCacheKey : SwiftIteratorProtocol_xam_vtable]();

public func setSwiftIteratorProtocol_xam_vtable(_ uvt: UnsafeRawPointer, _ t0: Any.Type)
{
    let vt: UnsafePointer<SwiftIteratorProtocol_xam_vtable> = fromIntPtr(ptr: uvt);

    _vtable[TypeCacheKey(types: ObjectIdentifier(t0))] = vt.pointee;
}

fileprivate func getSwiftIteratorProtocol_xam_vtable(_ t0: Any.Type) -> SwiftIteratorProtocol_xam_vtable?
{
    return _vtable[TypeCacheKey(types: ObjectIdentifier(t0))];
}
```
Now let's look at the implementation of the Swift proxy:

```swift
public final class SwiftIteratorProtocolProtocol<T0> : IteratorProtocol
{
    public init()
    {
    }

    public func next() -> Optional<T0>
    {   
        // get the vtable
        let vt: SwiftIteratorProtocol_xam_vtable = getSwiftIteratorProtocol_xam_vtable(T0.self)!;

        // allocate space for the return value
        let retval = UnsafeMutablePointer<Swift.Optional<T0>>.allocate(capacity: 1);

        // call C#
        vt.func0!(retval, toIntPtr(value: self));

        // copy out the return value
        let actualRetval = retval.move();

        // deallocate the space used
        retval.deallocate();
        return actualRetval;
    }
}
```

The only thing that's missing is wrappers to call the Swift proxy's ctor and to call its `next` method. These are trivial, so I'm leaving them out and with runtime support they shouldn't be necessary.

Back into C#.

In C# we have some type that implements `ISwiftIteratorProtocol` we want to use it in Swift, so we need to make a proxy for it in C#. This proxy will make the above swift proxy, first making a vtable for it:
```csharp
internal struct SwiftIteratorProtocol_xam_vtable {
    public delegate void Delfunc0 (IntPtr xam_retval, IntPtr self);
    [MarshalAs (UnmanagedType.FunctionPtr)]
    public Delfunc0 func0;
}
```
Next we need code to initialize that vtable:
```csharp
static void XamSetVTable ()
{
    xamVtableISwiftIteratorProtocol.func0 = xamVtable_recv_Next_SwiftOptionalT0;
    unsafe {
        byte* vtData = stackalloc byte [Marshal.SizeOf (xamVtableISwiftIteratorProtocol)];
        IntPtr vtPtr = new IntPtr (vtData);
        Marshal.WriteIntPtr (vtPtr, Marshal.GetFunctionPointerForDelegate (xamVtableISwiftIteratorProtocol.func0));
        NativeMethodsForSwiftIteratorProtocolProtocol.SwiftXamSetVtable (vtPtr,
            StructMarshal.Marshaler.Metatypeof (typeof (ATElement)));
    }
}
```
The func0 entry is initialized to a *receiver* which will get called from Swift:
```csharp
static void xamVtable_recv_Next_SwiftOptionalT0 (IntPtr xam_retval, IntPtr self)
{
    // get the C# object for the instance and call its Next method
    SwiftOptional<ATElement> retval = SwiftObjectRegistry.Registry.CSObjectForSwiftObject<SwiftIteratorProtocolProtocol<ATElement>> (self).Next ();
    // if it's a heap allocated object, it's either a pointer or null
    if (typeof (ISwiftObject).IsAssignableFrom (typeof (SwiftOptional<ATElement>))) {
        Marshal.WriteIntPtr (xam_retval, ((ISwiftObject)retval).SwiftObject);
    } else {
    // if not, marshal it
        StructMarshal.Marshaler.ToSwift (typeof (SwiftOptional<ATElement>), retval, xam_retval);
    }
}
```
Finally let's go back to the implementation of `Next` and rewrite it to handle either Swift or C# objects:
```csharp
public SwiftOptional<ATElement> Next ()
{
    // if there exists a managed implementation of the interface, call that.
    if (xamarinImpl is not null) {
        return xamarinImpl.Next ();
    } else {
        unsafe {
            // allocate a C# version of the return value
            SwiftOptional<ATElement> retval = new SwiftOptional<ATElement> ();
            fixed (byte* retvalSwiftDataPtr = StructMarshal.Marshaler.PrepareValueType (retval)) {
                NativeMethodsForSwiftIteratorProtocolProtocol.PImethod_SwiftIteratorProtocolProtocolXamarin_SwiftIteratorProtocolProtocolDnext00000001 ((IntPtr)retvalSwiftDataPtr, thisIntPtr);
                return retval;
            }
        }
    }
}
```
In this case, you can see how the duality is managed:
If the proxy has a C# implementation, it calls that. If it has a Swift implementation, it calls that.

And for `ISwiftIteratorProtocol<T>`, I created an extension method `AsIEnumerable<T>` so that you can do something like:
```
foreach (var elem in someSwiftIterator.AsIEnumerable()) { }
```
Similarly, I create a type to adapt any `IEnumerator<T>` as an `ISwiftIteratorProtocol<T>` and made an extension for that so that you could call a swift method that wants `IteratorProtocol` using, say, `List<T>`.

In sum, the process of binding a PAT is
- write a C# interface
- write a Swift proxy and that uses a vtable (or call the C# receivers directly via NativeAOT entry points)
- write a dual proxy in C#
- direct the proxy in Swift to call C# supplied functions

The one thing that this interface is missing is the `Self` type. If it has a self type, the interface will look like this:
```csharp
public interface ISomeProtocol<ATSelf, ATElement> where ATSelf: ISomeProtocol<ATSelf, ATElement> {
    ATSelf GetSelf ();
    void DoSomethingWith(a: ATElement);
}
```
In this case, you see that there is a recursive constraint. This is necessary to mimic the behavior of Self. Unfortunately when the associated types themselves have constraints, things get ugly fast.

## Handling the `some` keyword

When a function in Swift returns `some PAT` this is an issue because in C# the ownership of the type of a generic is in the hands of the caller not the callee. However, Swift has a similar restriction. Revisiting the animal example before, if we change it to this:

```swift
public protocol Animal {
    associatedtype Food
    func feed(food: Food)
    func speak()
}

public class Grass { }

public class Cow : Animal {
    public func feed(food: Grass) {
        print ("yummy grass")
    }
    public func speak()
    {
        print ("moo")
    }
}

public func getAnAnimal() -> some Animal
{
    return Cow()
}

public func getAndSpeak() {
    let c = getAnAnimal()
    c.speak() // speak is legal to access since it doesn't require associated types
    c.feed(/* what should go here?*/) // illegal
}
```

To bind a PAT in C# it should be presented as two interfaces: a non-generic one and a generic one which inherits from the non-generic one:

```csharp
public interface IAnimal
{
    void Speak();
}
public interface IAnimal<TSelf, TFood> where TSelf: IAnimal<TSelf, TFood> : IAnimal
{
    void Feed(TFood food);
}
```

This way a Swift function bound into C# that returns `some PAT` will return the non-generic interface and not the generic interface.

Since the non-generic version is devoid of references to the associated types, then C# will be modeling the Swift behavior accurately.
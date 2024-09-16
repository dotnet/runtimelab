# Binding Structs

Structs in Swift are value types that can have a number of forms each of which requires a certain amount of consideration: frozen/non-frozen and blittable and non-blittable ([.NET definitions here](https://learn.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types)).

Structs that are frozen are guaranteed not to change. They have a fixed layout and can be passed by value lowered up until the point when the size of the struct exceeds the number of registers dedicated for value types in Swift (currently 4 and unlikely to change).

Non-frozen structs are passed by reference since the layout and size are not guaranteed.

Similarly when calling members on frozen structs, the instance is passed by value as they can fit into registers. However for non-frozen structs the instance is passed by reference in the self pointer.

Blittable structs are structs that can be correctly copied with `memcpy`. Essentially, if a struct contains a field which is heap allocated or contains a non-blittable data type, it will have issues with regards to reference counting. In addition, generic structs need to be considered non-blittable since they *may* contain non-blittable types, but we can't know that ahead of time.

Structs in Swift are encouraged to be immutable. To help along with this, the compiler enforces a number of rules to prevent you from trying to mutate an immutable value. All arguments to a method are considered to be `let` bindings. The compiler won't let you modify public fields of struct that is defined with a `let` binding. Similarly, you cannot call a `mutating` method on a struct that is bound with `let`.

Every value type in Swift comes with a compiler-generated Value Witness Table. Unlike other function tables in Swift (Protocol witness tables, vtables etc), the layout of value witness tables are well-defined. The value witness table is documented [here](binding-value-witness-table.md).

## Language Parity Mismatches

Structs in C# and Swift are very similar. They both represent value types with a concrete memory layout. The primary difference is that structs in Swift will execute code whenever it goes out of scope.

Given the following Swift code:
```swift
public class Named {
    private let name: String
    public init(name n: String)
    {
        name = n
    }
    deinit
    {
        print("\(name) left the building.")
    }
}

public struct NamedHolder {
    public init (name n: String) {
        name = Named(name: n);
    }
    public var name: Named
}

public func runIt()
{
    var n1:NamedHolder = NamedHolder(name: "Corey")
    let n2:NamedHolder = NamedHolder(name: "Ian")
    n1 = n2
}
runIt()
print("all done")
```
It will generate the following output:
```
Corey left the building.
Ian left the building.
all done
```
This shows how the destructor in the class gets executed when a struct gets overwritten ("you killed Corey") and when it goes out of scope. C# will do neither of this things.

Swift allows 0-length structs, whereas C# does not.
Swift allows structs to be non-copyable by adding the pseudo inheritance `: ~Copyable` to the type declaration. These types may present issues when
being bound. The value witness table contains functions that will execute an illegal instruction if they are called to copy the type and all mechanisms
within swift to get the address of an non-copyable instance are forbidden.

## ABI Differences
Swift has very specific rules for packing structs which Apple has laid out [here](https://github.com/swiftlang/swift/blob/main/docs/ABI/TypeLayout.rst).

Structs are passed to functions in one of two ways depending on whether or not they are frozen under `enable-module-evolution` rules.
If the struct is frozen and bitwise-copyable, it is lowered into up to 4 registers, otherwise it is passed by reference. This is usually done by copying it onto the stack and taking the address into a register. If the func is an instance method it will be passed by reference in the self register.

## Runtime Differences

None beyond the fact that the value witness table's `destroy` function will get executed when the value type goes out of scope.

## Idiomatic Differences

The main issue that we'll run into is non-blittable structs. Consider the C# binding of the previous example:

```csharp
public struct NamedHolder {
    // would need space for the private payload
    public NamedHolder(SwiftString name)
    {
        PInvokeNamedHolderInit(ref this, name);
    }
}

// ...
// Consuming code, which seems totally reasonable for C#
var n1 = new NamedHolder(SwiftString.FromString("Corey"));
var n2 = new NamedHolder(SwiftString.FromString("Ian));
n1 = n2; // memory leak.
```

## Accessibility
The main problem that we have is with non-blittable structs. We would either need our users to manually destroy structs when they're no longer needed (this a really bad idea - people are awful at memory management - that's why we have garbage collection and `IDisposable`)

The way this was handled in BTfS was to make no distinction between blittable and non-blittable structs and to implement them as a class with a byte array payload that implemented `IDisposable`. This would give the effect of having the destroy method called when the class gets disposed. The downside to this is that all structs, regardless of blitability, incur a cost in terms of heap allocation and at GC time. The up side is that the code to do the binding is simpler and handled uniformly.

Moving forward I think that non-blittable structs should be bound by a class with an opaque payload.


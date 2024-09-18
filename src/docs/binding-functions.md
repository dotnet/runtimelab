# Binding Functions

Functions can be broken down into several different classes

- Global - not attached to any type
- Instance - methods that have an implicit extra argument in the self register for accessing instance variables and functions
- Static - methods that have no implicit extra argument but can access static variables and static functions
- Class - methods that have an implicit extra argument in the self register which is the type metadata for the class. Class methods can be overridden in subclasses. It appeared that Apple's intent was to make the type metdata object more of a first class object, but support for other things that might make class methods more useful like class fields are not there, but you can make computed variables.

Within these set of method types there are several variations that are common to the function classes:
- Convenience - getter, setter, get_indexer, set_indexer, operators. It should be noted that unlike C#, Swift doesn't allow set-only accessors: if there is a setter, there will be a getter. Also indexers can only exist as member functions.
- Throws - throws a type that implements the `Error` protocol. The error ends up in a dedicated register which is nil is there was no error. It should be noted that `throws` is not the same as throwing in other languages in that an error that is thrown **must** be handled or rethrown. Swift has no notion of an exception automatically propagating up the call stack. This is no doubt because reference count updating under those circumstances would be non-trivial at best.
- Async - function runs asynchronously
- Mutating - function allows a value type to be changed within the function. This can only be used in the context of a value type.
- Homonyms - swift allows functions to be distinct only on return type with all else the same


## Language Parity
In terms of language parity issues, the biggest one are Swift class methods as C# doesn't have this notion. One approach that could be taken is to implement a base class for all type metadata objects and represent class methods in that class. For example, given this swift:
```swift
public class Foo {
    public class func getName() -> String {
        return "Pat"
    }
}
```
We could have C# like this:
```csharp
public class TypeMetadata {
    public TypeMetadata(NativeHandle handle)
    {
        Handle = handle;
    }
    public NativeHandle Handle { get; private set; }
}

public class Foo {
    public class FooTypeMetadata : TypeMetadata {
        public virtual SwiftString GetName()
        {
            return _getName(Handle);
        }

        [DllImport()]
        static extern SwiftString _getName(NativeHandle self);
    }

    public NativeHandle ClassHandle { get; } // singleton implementation not important
}
```

The downside to this is that this is that we'd be using a heap allocated type to represent type metadata instead of a value type. It might not make a huge difference because type metadata be a singleton for every unique type (this includes generic instances).

C# does not have top-level entities. We can get around that by declaring a static class in the bound namespace and putting top-level entities into that class. In Binding Tools for Swift, that class was either named `TopLevelEntities` or a name supplied on the command line. This was never a comfortable name from a C# point of view. The default name could also be the name of the SwiftModule, which is a reasonable default. There could be concerns of the Swift module defining a type with the same name as the module type. This is less of a concern because the Swift compiler will generate a warning
to avoid naming the type the same as the module

Swift allows identifiers with unicode values that are not present in C#. Notably, functions can be named with emoji. There needs to be a mechanism to map illegal unicode characters to something reasonable. Binding tools for Swift has a configurable mapping table defaulting to substituting uNNNN where NNNN is the unicode code point.

Swift allows function homonyms and C# does not. In order to handle this, homonyms can be detected and the function name can have the name of the return type appended to it. This can get more complicated when the return types don't have pretty names on their own such as tuples or generic types
or inner types.

Like C#, Swift has keyword escaping, but uses backticks as delimeters instead of the at-sign prefix in C#.

Operators are problematic as C# has only a limited set of available of operators whereas Swift has a very flexible set of operators.

## ABI Differences
The runtime team is taking care of both the self and return pointers as well as value type passing and returning.

## Runtime Differences
There are likely issues in mapping async functions which are out of scope and would need runtime support.
Generic functions in Swift work by adding an extra implicit argument for every generic in the function declaration which is a pointer to Swift type metadata for the generic argument. In addition, whatever argument is passed to the function gets passed as a reference. If a generic argument has one or more protocol constraints, the compiler injects an argument which is a pointer to the protocol witness table for the type with respect to the protocol. For example, if you have this Swift function:
```swift
public func Identity<T: SomeProtocol>(a: T) -> T {
    return a
}
```
When compiled, this function actual takes 3 arguments, not two:
```swift
public func Identity(a *T, type: T.Self, ProtoWitness(T.Self, SomeProtocol.Self)) -> T { }
```

## Idiomatic Differences
Functions in swift start with a lowercase letter. We should auto-capitalize the first letter of a function name.
Since property getters and setters are implemented as functions they map naturally onto C# properties. One difference, which is inconsequential except in projecting C# into Swift is that C# allows set-only properties whereas in Swift if a property has a setter it must have a getter.

## Accessibility Differences
For the most part, we should strive to address the language parity issues in ways that feel "right" in C# or at least "least bad". For example, this were definied in Swift:
```swift
prefix operator -+-

public struct Container {
    public var x: Int
    public init (x: Int) {
        self.x = x
    }
    public static prefix -+- (c: Container)
    {
        return Container(c.x + c.x)
    } 
}
```
We run into two problems: C# doesn't have an operator `-+-` nor can we define one. If implemented as a method, `-+-` is unpronounceable in C#. Taking the approach of using a prefix like "Op_" or "Oper_" or "Operator_" we can make it clear that this was an operator. Then the operator name would need to be run through a sanitizer to turn it into something pronounceable. The best we could hope for is getting a name out the other side like "Op_PlusMinusPlus".
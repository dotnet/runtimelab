# Binding Closures

Closures in Swift come in two general forms: escaping and non-escaping. A closure is considered escaping if it continues to exist outside of the context that created it. For example, a closure parameter to a function that gets stored in a member variable in a class *must* be marked `@escaping` or the compiler will flag it as an error.

In either case, Swift closures, unlike some other languages, are full-fledged closures that can have both free and bound variables within the body of the closure.

The distinction between the two is that escaping closures that capture free variables use a dynamically allocated object to contain any captured variables. This object is reference counted. Non-escaping closures use stack allocated memory to contain any captured variables.

Beyond the broad classification of escaping/non-escaping, closures can also be async and they can throw.

Internally, closures are represented as 2 machine words:

- Pointer to a function entry point for the closure (see below for details)
- Pointer to the data context object for captured free variables or 0/null if there is no context object. When the closure is called, this will be in the context/self register.

## Language Parity Mismatches

None in particular. From a language standpoint, they both align in capability although the implementation are different.

Swift has a delightful bit of syntax sugar that allows you to supply a closure at the call site in a way that it looks like inline code.
If you have a function like this in Swift:

```swift
public func sorter<T>(arr: [T], by: (T, T) -> Int) { /* implementation not important */ }

```

And `sorter` can be called by any of the following means:

```swift
private func sort0 (a: Int, b: Int) -> Int {
    return a - b
}

let arr = [7, 1, 27]
sorter(arr: a, by: sort0) // 1: sort0 is your closure
sorter(arr: a, by: { a, b in return a - b}) // 2: inline closure
sorter(arr: a) { a, b in  // 3: trailing closure
    return a - b
}
```

The last case is a trailing closure which is exactly equivalent to the second inline closure

## ABI Differences

Closures follow the same calling conventions as [functions](binding-functions.md).

With support for Swift calling conventions in the runtime, we should be insulated from issues in the ABI differences.

We would need to be aware reference counting of the context object. Closures are more or like like this:

```swift
public struct EscapingClosure {
    public var entryPoint: OpaquePointer
    public var context: AnyObject?
}

public struct Closure {
    public var entryPoint: OpaquePointer
    public var context: OpaquePointer?
}
```

When invoking the closure, the self register needs to be set to contents of the context pointer.
So essentially:

```asm
mov entryPoint[closure], rax
mov context[closure], r13
jsr [rax]
```

or something similar.

One interesting thing is that the Swift compiler writes closure with arguments for free variables as well as a forwarder.

For example, if I have a function that returns a closure like this:

```swift
public func getSummer (a: Int) -> (Int) -> Int {
    return { (b: Int) in
        a + b
    }
}
```

Then the compiler will write this:

```Swift
private func getSummerImplementation(a: Int, b: Int) -> Int {
    return a + b
}
```

And a forwarder that looks like this:

```asm
move 10[r13], rsi // b goes into argument 2
jmp _getSummerImplementation
```

## Runtime Differences

Ideally, we would like to be able to pass C# delegates to Swift functions or store them into Swift types and have them call back into the right place.  The problem with this is that it would be essentially a reverse p/invoke so an arbitrary C# delegate is incompatible as is, but with support from the runtime, this should be less of an issue, but there are still some things that we would need to care about.

If there is a case when we can't directly adapt a Swift closure into C#, there are some options that are available to us. In BTfS, which has no benefits from the runtime, this is done by converting Swift adapters into a more general form:

Given a closure of the form `(arguments) -> return`, this can be converted into the form: `(UnsafeMutablePointer<return>, UnsafeMutablePointer<(arguments)>)->()` For example:

```swift
public func callsIntoCSharp (a: @escaping (Int, SomeStruct, Bool) -> SomeOtherStruct) {
    let a_adapter = { (ret: UnsafeMutablePointer<SomeOtherStruct>, args: UnsafeMutablePointer<(Int, SomeStruct, Bool)>) in
        let (i, ss, b) = args.pointee
        ret.initialize(to: a(i, ss, b))
    }
    callToCSharpMethod (a_adapter)
}
```

In this case, the original closure gets captured by `a_adapter`. Before calling the adapter, C# obviously has to create the argument tuple, space for the return value (allocated but not initialized) and can call it because effectively the delegate signature has become `delegate void csAdapter (IntPtr ret, IntPtr args)`. A similar process is used to get C# closures into Swift.

One obvious problem here is that this only works with escaping closures. If rewritten with a non-escaping closure, the act of capturing the original closure in the new one is flagged by the Swift compiler as an error. Fortunately, Swift has a workaround to this via the function [`withoutActuallyEscaping`](https://developer.apple.com/documentation/swift/withoutactuallyescaping(_:do:)/), for which the previous code can be rewritten as this:

```swift
public func callsIntoCSharp (a: (Int, SomeStruct, Bool) -> SomeOtherStruct) {
    withoutActuallyEscaping (a) { a_escaping in
        let a_adapter = { (ret: UnsafeMutablePointer<SomeOtherStruct>, args: UnsafeMutablePointer<(Int, SomeStruct, Bool)>) in
            let (i, ss, b) = args.pointee
            ret.initialize(to: a_escaping(i, ss, b))
        }
    }
    callToCSharpMethod (a_adapter)
}
```

Please note that with runtime support, this shouldn't be necessary, but this may be important for future reference.

In running the other direction, we would need a way to convert a C# closure into something that is callable from Swift. The approach in BTfS is heavy handed because of the lack of runtime support. Given a C# closure, we create a handle to it, then call into a Swift routine which generates a swift closure that calls back into C# with a pointer to argument and return as before, but now with the handle and a `@convention (c)` function pointer to goes back into to a C# routine that unpacks the arguments, uses the handle to get the original C# closure and calls it. We should be able to do better than this.

## Idiomatic Differences

The main idiomatic difference has to do with the escaping/non-escaping varieties of closure. Obviously, C# doesn't make this distinction. As such, if a C# method gets passed a delegate from Swift that is non-escaping, it is incumbent upon the user to never store it. We can make this somewhat better by putting an attribute on such delegates that flags it as a non-escaping and create a Roslyn analyzer that looks for usage that would violate that.

## Accessibility

The main decision in presenting Swift closure types to C# programmers is how to present the types to the user. We can use the types `Func<>` and `Action<>`, but they create an artificial distinction between closures that have or lack return values and that end ups complicating adapting code. Or we can create `delegate` type declarations that match the closure definition.

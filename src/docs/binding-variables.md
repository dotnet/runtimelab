# Binding Variables

Variables in Swift come in two general forms: computed and stored.  Unlike C#, Swift allows variables to exist at the top level of a module being effectively global.

With enable-module-evolution set, all `let` variables will have a getter. Variables that are declared with `var` will have a getter as well as a setter. It is an error to make a set-only variable.

Variables can be:
- global
- static
- instance
- class (computed only)
- mutating set (value types only)
- open (instance in classes only)

## Language Parity Mismatches
- Naming - Swift can have variable names that are unpronouncable in C#
- Class - the notion of class variables doesn't exist in C#
- Swift variables can be `async` whereas C# properties can't
- willSet/didSet - while these can be conceptually modeled with events in C#, these are problematic for binding as there is no real way to override/chain these

The typical declaration of a property with observers is something like this:
```swift
public var x:SomeType = someInitializer {
    willSet {
        print(“x is being set to \(newValue)”)
    }
    didSet {
        printf(“x was just changed from \(oldValue)”)
    }
}
```
In each observer, there is an implicit parameter which in `willSet` is `newValue`, initialized to the value passed to the setter and in didSet is oldValue, initialized to the value of the variable prior to being changed.

There are some restrictions on observers. First, they are never called from the initializer. This makes sense - Swift wants initialization to proceed in an orderly manner and calling `willSet`/`didSet` from the initializer opens doors for methods to be called when the instance is not completely initialized. Second, they can only be applied to non-computed properties *unless they are in the subclass of a computed property*. Finally, Swift observers implicitly call the super class’ observer. In `willSet`, the superclass is tail chained. In `didSet`, it’s head chained.

Here's an example of non computed and overriden computed variables:
```swift
open class Circle {
    public var center:Point = Point(0, 0)
    public var radius:Float = 0 {
        willSet { // legal - this is not a computed variable
            print(“radius will be changed to \(newValue)”)
       }
   }
   public var diameter:Float {
       get { return radius * 2.0 }
       set { radius = newValue / 2.0 }
       willSet { // not legal - this *is* a computer variable
           print(“this is not allowed.”)
       }
   }
}
open class MyCircle : Circle {
    override var diameter:Float {
        willSet { // legal - this is an override
            print(“diameter will be changed to \(newValue)”)
       }
   }
}
```

Here's an example of the chaining:
```swift
import Foundation


open class Bob {
    var foo : Int = 0
    {
        willSet {
            print("Bob willSet")
        }
        didSet {
            print("Bob didSet")
        }
    }
}

open class Jane : Bob
{
    override var foo: Int
    {
        willSet {
            print("Jane willSet")
        }
        didSet {
            print("Jane didSet")
        }
    }
}
```

This produces the following output:
```
Jane willSet
Bob willSet
Bob didSet
Jane didSet
```
Under the hood, the Swift compiler generates a computed setter for Jane that looks like this:
```swift
public void set_foo(newValue: Int)
{
    let oldValue = super.get_foo(); // gets the old value
    willSet_foo(newValue);
    super.set_foo(newValue); // calls super.willSet_foo and super.didSet_foo
    didSet_foo(oldValue);
}
```

These are the options that I see here:
1 - add C# observers to all variables no matter what
2 - add C# observers to variables that have observers
3 - allow the user to configure the binding as either opt-in or opt-out for observer generation
4 - add no observers - technically, if the user subclasses the type they could add their own in the setter.

In any case, I don't know that we have any near-term use cases that require this so it's safe to wait on making this decision.

## ABI Differences
- Accessors have the same issue as member functions in that they use a self register for the instance (if any) and an error register if they can throw.
- Accessors can be async

## Runtime Differences
- When a variable setter is called, if the type is a value type, the type's value witness table should be used to destroy the old data (if being overwritten) and to copy the new data. If the type is a heap allocated type, the reference count of the old needs to be decreased before (if being overwritten) and the reference count of the new needs to be increased.

## Idiomatic Differences
Except for class variables and willSet/didSet, Swift variables map directly onto C# properties.

## Accessibility
No issues.

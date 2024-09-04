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
- willSet/didSet - while these can be conceptually modeled with events in C#, these are problematic for binding as there is no real way to override/chain these

## ABI Differences
- Accessors have the same issue as member functions in that they use a self register for the instance (if any) and an error register if they can throw.
- Accessors can be async

## Runtime Differences
- When a variable setter is called, if the type is a value type, the type's value witness table should be used to destroy the old data (if being overwritten) and to copy the new data. If the type is a heap allocated type, the reference count of the old needs to be decreased before (if being overwritten) and the reference count of the new needs to be increased.

## Idiomatic Differences
Except for class variables and willSet/didSet, Swift variables map directly onto C# properties.

## Accessibility
No issues.

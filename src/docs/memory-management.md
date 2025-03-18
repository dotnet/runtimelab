# Memory management

This section outlines the native memory management approach for projected value types, focusing on when and how to retain and release reference counters. The goal is to ensure that native memory is handled correctly across Swift and C# boundaries.

## Requirements
 - Projected types shall call the value witness table functions to retain and release reference counters when required
 - Projected types shall not invoke retain or release functions when implicit copies of value types with reference properties are created on the C# side

## Memory ownership

The memory ownership rules described below apply exclusively to Swift and don't reflect on C# practices.

To identify scenarios that require explicit memory handling, consider the following Swift example:
```swift
public class RefType {
    public init() { }

    deinit { }
}

public struct VType {
    public var refType: RefType

    public init() {
        refType = RefType()
    }

    public init(refType: RefType) {
        self.refType = refType
    }
}
```

To define memory handling rules at the Swift and C# boundary, the following scenarios are included:
 - Code blocks
 - Initializers
 - Parameters passed by value
 - Parameters passed by reference (inout)
 - Return types

### Code blocks

When a value type is initialized within a block, it is stack-allocated. At the end of the block, all reference properties must be deallocated. For example:
```swift
public func TestBlock()
{
    var vtype = VType()
}
```
```
%vtype = alloca %T6output5VTypeV, align 8
...
call swiftcc void @"output.VType.init() -> output.VType"(ptr noalias nocapture sret(%T6output5VTypeV) %vtype), !dbg !248
%0 = call ptr @"outlined destroy of output.VType"(ptr %vtype), !dbg !250

// VWT->Destroy
define linkonce_odr hidden ptr @"outlined destroy of output.VType"(ptr %0) #8 !dbg !143 {
entry:
  %.refType = getelementptr inbounds %T6output5VTypeV, ptr %0, i32 0, i32 0, !dbg !144
  %toDestroy = load ptr, ptr %.refType, align 8, !dbg !144
  call void @swift_release(ptr %toDestroy) #1, !dbg !144
  ret ptr %0, !dbg !144
}
```

### Initializers

When a reference type or a value type containing a reference property is passed as a parameter to a constructor, its reference count is retained before the call and released after:

```swift
var refType = RefType()
var vtype = VType(refType: refType)
```
```
%3 = call ptr @swift_retain(ptr returned %2) #4, !dbg !164
store ptr %2, ptr %refType, align 8, !dbg !164
call swiftcc void @"output.VType.init(refType: output.RefType) -> output.VType"(ptr noalias nocapture sret(%T6output5VTypeV) %vtype, ptr %2), !dbg !168
%toDestroy = load ptr, ptr %refType, align 8, !dbg !170
call void @swift_release(ptr %toDestroy) #1, !dbg !170
```

### Parameters passed by value

When a reference type or a value type containing a reference property is passed as a parameter, a copy is made (reference counter is retained before the call) and released after:

```swift
public func TestParameter()
{
    var vtype = VType()
    callByVal(vtype: vtype)
}

public func callByVal(vtype: VType) { }
```
```
%1 = load ptr, ptr %vtype.refType, align 8, !dbg !258
%2 = call ptr @swift_retain(ptr returned %1) #5, !dbg !258
%.refType = getelementptr inbounds %T6output5VTypeV, ptr %0, i32 0, i32 0, !dbg !260
store ptr %1, ptr %.refType, align 8, !dbg !260
call swiftcc void @"output.callByVal(vtype: output.VType) -> ()"(ptr noalias nocapture dereferenceable(8) %0), !dbg !261
%3 = call ptr @"outlined destroy of output.VType"(ptr %0), !dbg !262
```

### Parameters passed by reference (inout)

When `inout` reference type or a value type containing a reference property is passed as a parameter, it is passed by reference, so no reference counters are updated.
```swift
public func TestInOutParameter()
{
    var vtype = VType()
    callByRef(vtype: &vtype)
}

public func callByRef(vtype: inout VType) { }
```
```
%vtype = alloca %T6output5VTypeV, align 8
...
call swiftcc void @"output.callByRef(vtype: inout output.VType) -> ()"(ptr nocapture dereferenceable(8) %vtype), !dbg !280
```

### Return types

When a value type is returned from a function, the caller takes ownership of the instance and is responsible for releasing it at the end of the block. In the callee:
 - If a new instance is created, its counter is initialized to 1
 - If an existing parameter is returned, its reference count is retained before returning
 - If an inout parameter is returned, a copy is created and returned

Pass-through callee:
```swift
public func TestPassThrough()
{
    var vtype = VType()
    var result = PassThrough(vtype: vtype)
}

public func PassThrough(vtype: VType) -> VType
{
    return vtype
}
```
```
%vtype.debug = alloca ptr, align 8
%.refType = getelementptr inbounds %T6output5VTypeV, ptr %1, i32 0, i32 0, !dbg !314
%2 = load ptr, ptr %.refType, align 8, !dbg !314
store ptr %2, ptr %vtype.debug, align 8, !dbg !316
%3 = call ptr @swift_retain(ptr returned %2) #5, !dbg !317
%.refType1 = getelementptr inbounds %T6output5VTypeV, ptr %0, i32 0, i32 0, !dbg !317
store ptr %2, ptr %.refType1, align 8, !dbg !317
ret void, !dbg !318
```

New instance callee:
```swift
public func TestNewInstance()
{
    var result = NewInstance()
}

public func NewInstance() -> VType
{
    return VType()
}
```
```
call swiftcc void @"output.VType.init() -> output.VType"(ptr noalias nocapture sret(%T6output5VTypeV) %0), !dbg !332
ret void, !dbg !333
```

## Memory handling

To handle native memory in the scenarios above, the projections should use the value witness table to invoke `InitWithCopy` for copy operations and `Destroy` for finalization. These functions manage reference counts at any level of nesting.

To ensure correct memory handling:
 - Swift value types that contain reference properties should be projected as C# classes
 - When a type goes out of the block the finalizer/dispose should invoke `Destroy` function
 - When a type is marshalled to Swift as a function parameter, `InitWithCopy` should be invoked to create the copy
 - When a type is marshalled to Swift as an `inout` function parameters, an instance reference is passed. If the projected Swift instance in C# remains in C# beyond the lifetime of the callee, `InitWithCopy` should be invoked to create the copy
 - When a type is marshalled to Swift as a return parameter, `InitWithCopy` should be invoked to create the copy
 - When a type is marshalled from Swift as a return paramter, no reference counters are updated
 - When using a private "copy" constructor on the C# side for marshalling from Swift, `InitWithCopy` should be invoked

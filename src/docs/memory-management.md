# Memory management

This section outlines the native memory management approach for projected value types, focusing on when and how to retain and release ref counters. The goal is to ensure that native memory is handled correctly across Swift and C# boundaries.

## Requirements
 - Projected types shall call the value witness table functions to retain and release owned ref types when required
 - Projected types shall not invoke retain or release functions when implicit copies of ref types are created on the C# side

## Memory ownership

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

To define memory handling rules at the Swift and C# boundary, the following scenarios are included:
 - Code blocks
 - Initializers
 - Parameters
 - Return types

### Code blocks
```
When a value type is initialized within a block, it is stack-allocated. At the end of the block, all reference properties must be deallocated. For example:
```
%vtype = alloca %T6output5VTypeV, align 8
...
%0 = call ptr @"outlined destroy of output.VType"(ptr %vtype), !dbg !156

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

### Parameters

When a reference type or a value type containing a reference property is passed as a parameter, its reference count is retained before the call and released after:

```
%1 = load ptr, ptr %vtype.refType, align 8, !dbg !154
%2 = call ptr @swift_retain(ptr returned %1) #4, !dbg !154
%.refType = getelementptr inbounds %T6output5VTypeV, ptr %0, i32 0, i32 0, !dbg !156
store ptr %1, ptr %.refType, align 8, !dbg !156
call swiftcc void @"output.PassThrough(vtype: output.VType) -> ()"(ptr noalias nocapture dereferenceable(8) %0), !dbg !157
%3 = call ptr @"outlined destroy of output.VType"(ptr %0), !dbg !158
```

### Return types

When a value type is returned from a function, a new instance is created, and its reference count is retained before the return. The caller is responsible for releasing the reference:
```
%2 = load ptr, ptr %.refType, align 8, !dbg !173
store ptr %2, ptr %vtype.debug, align 8, !dbg !175
%3 = call ptr @swift_retain(ptr returned %2) #4, !dbg !176
%.refType1 = getelementptr inbounds %T6output5VTypeV, ptr %0, i32 0, i32 0, !dbg !176
store ptr %2, ptr %.refType1, align 8, !dbg !176
```

## Memory handling

To handle native memory in the scenarios above, the projections should could use the value witness table to invoke `InitWithCopy` for copy operations and `Destroy` for finalization. These functions manage reference counts at any level of nesting.

To ensure correct memory handling:
 - Value types that contain reference properties should be projected as C# classes with destructors that call `Destroy`
 - When types are marshalled into Swift, `InitWithCopy` should be invoked to create the copy/ `Destroy` should be called on the copy at the end of the block
 - When types are marshalled from Swift, `Destroy` should be called at the end of the block
 - When using the "copy" constructor on the C# side, `InitWithCopy` should be invoked

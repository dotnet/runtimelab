// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

public class RefType
{
    public var test: UnsafeMutablePointer<Int64>

    public init(test: UnsafeMutablePointer<Int64>) {
        self.test = test
    }

    deinit {
        test.pointee = 1
    }
}

// ref type at offset 0
public struct VType
{
    public var refType: RefType
    private var refTypeTestPtr: UnsafeMutablePointer<Int64>

    public init() {
        refTypeTestPtr = UnsafeMutablePointer<Int64>.allocate(capacity: 1)
        refTypeTestPtr.initialize(to: 0)

        refType = RefType(test: refTypeTestPtr)
    }

    public init(refType: RefType) {
        self.refType = refType
        refTypeTestPtr = refType.test
    }

    public var refTypeTest: Int64 {
        get { return refTypeTestPtr.pointee }
    }

    public func cleanup() {
        refTypeTestPtr.deinitialize(count: 1)
        refTypeTestPtr.deallocate()
    }
}

// ref types at offsets 0, 16
public struct NestedVType
{
    public var refType: RefType
    private var refTypeTest1Ptr: UnsafeMutablePointer<Int64>
    public var vType: VType

    public init()
    {
        refTypeTest1Ptr = UnsafeMutablePointer<Int64>.allocate(capacity: 1)
        refTypeTest1Ptr.initialize(to: 0)

        refType = RefType(test: refTypeTest1Ptr)

        let refTypeTest2Ptr = UnsafeMutablePointer<Int64>.allocate(capacity: 1)
        refTypeTest2Ptr.initialize(to: 0)

        vType = VType(refType: RefType(test: refTypeTest2Ptr))
    }

    public init(refType1: RefType, refType2: RefType)
    {
        refTypeTest1Ptr = refType1.test
        refType = refType1

        vType = VType(refType: refType2)
    }

    public var refTypeTest1: Int64 {
        get { return refTypeTest1Ptr.pointee }
    }

    public var refTypeTest2: Int64 {
        get { return vType.refTypeTest }
    }

    public func cleanup() {
        vType.cleanup()

        refTypeTest1Ptr.deinitialize(count: 1)
        refTypeTest1Ptr.deallocate()
    }
}

// ref types at offsets 0, 16, 32
public struct NestedNestedVType
{
    public var refType: RefType
    private var refTypeTest1Ptr: UnsafeMutablePointer<Int64>
    public var vType: NestedVType

    public init()
    {
        refTypeTest1Ptr = UnsafeMutablePointer<Int64>.allocate(capacity: 1)
        refTypeTest1Ptr.initialize(to: 0)

        refType = RefType(test: refTypeTest1Ptr)

        let refTypeTest2Ptr = UnsafeMutablePointer<Int64>.allocate(capacity: 1)
        refTypeTest2Ptr.initialize(to: 0)

        let refTypeTest3Ptr = UnsafeMutablePointer<Int64>.allocate(capacity: 1)
        refTypeTest2Ptr.initialize(to: 0)

        vType = NestedVType(refType1: RefType(test: refTypeTest2Ptr), refType2: RefType(test: refTypeTest3Ptr))
    }

    public var refTypeTest1: Int64 {
        get { return refTypeTest1Ptr.pointee }
    }

    public var refTypeTest2: Int64 {
        get { return vType.refTypeTest1 }
    }

    public var refTypeTest3: Int64 {
        get { return vType.refTypeTest2 }
    }

    public func cleanup() {
        vType.cleanup()

        refTypeTest1Ptr.deinitialize(count: 1)
        refTypeTest1Ptr.deallocate()
    }
}

@frozen
public struct FrozenStruct {
    public var a: Int32

    public init (testPayload1: Int32) {
        self.a = testPayload1
    }
}

@frozen
public struct FrozenStructRequiresMemoryManagement {
    public var a: RefType
    public var b: Int32

    public init (b: Int32) {
        self.a = RefType(test: UnsafeMutablePointer<Int64>.allocate(capacity: 1))
        self.b = b
    }

    public func getValue() -> Int32 {
        return b
    }

    public func callDispose(callback: @escaping () -> Void) {
        callback()
    }
}

@frozen
public struct NestedFrozenStructRequiresMemoryManagement {
    public var a: FrozenStructRequiresMemoryManagement
    public var b: Int32

    public init (b: Int32) {
        self.a = FrozenStructRequiresMemoryManagement(b: b)
        self.b = b
    }

    public func getValue() -> Int32 {
        return b
    }
}

public struct NonFrozenStruct {
    public var a: Int32

    public init (a: Int32) {
        self.a = a
    }
}

public struct NonFrozenStructRequiresMemoryManagement {
    public var a: RefType
    public var b: Int32

    public init (b: Int32) {
        self.a = RefType(test: UnsafeMutablePointer<Int64>.allocate(capacity: 1))
        self.b = b
    }

    public func getValue() -> Int32 {
        return b
    }
}

@frozen
public struct InnerStruct {
    public var x: Int32
    public var y: UInt8

    public init() {
        self.x = 1
        self.y = 2
    }
}

@frozen
public struct EmbeddedStruct {
    public var x: InnerStruct
    public var y: UInt8
    public var z: RefType // offset is 8

    public init() {
        self.x = InnerStruct()
        self.y = 3
        self.z = RefType(test: UnsafeMutablePointer<Int64>.allocate(capacity: 1))
    }
}

public func PassThroughFrozenStruct(a: FrozenStructRequiresMemoryManagement) -> FrozenStructRequiresMemoryManagement {
    return a
}

public func PassThroughNestedFrozenStruct(a: NestedFrozenStructRequiresMemoryManagement) -> NestedFrozenStructRequiresMemoryManagement {
    return a
}

public func PassThroughNonFrozenStruct(a: NonFrozenStructRequiresMemoryManagement) -> NonFrozenStructRequiresMemoryManagement {
    return a
}

public func PassThroughEmbeddedStruct(a: EmbeddedStruct) -> EmbeddedStruct {
    return a
}

public func PassThroughGeneric<T>(a: T) -> T {
    return a
}

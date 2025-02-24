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

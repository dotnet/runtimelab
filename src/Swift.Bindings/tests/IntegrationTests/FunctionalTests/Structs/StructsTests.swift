// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import Foundation

@frozen
public struct FrozenStruct
{
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int)
    {
        self.x = x
        self.y = y
    }

    public func getX() -> Int
    {
        return x
    }

    public func getY() -> Int
    {
        return y
    }

    public func sum() -> Int
    {
        return x + y
    }
}

public struct NonFrozenStruct
{
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int)
    {
        self.x = x
        self.y = y
    }

    public func getX() -> Int
    {
        return x
    }

    public func getY() -> Int
    {
        return y
    }

    public func sum() -> Int
    {
        return x + y
    }
}

@frozen
public struct FrozenStructWithNonFrozenMember
{
    public var x: FrozenStruct
    public var y: NonFrozenStruct

    public init(x: FrozenStruct, y: NonFrozenStruct)
    {
        self.x = x
        self.y = y
    }

    public func getX() -> FrozenStruct
    {
        return x
    }

    public func getY() -> NonFrozenStruct
    {
        return y
    }

    public func sum() -> Int
    {
        return x.sum() + y.sum()
    }
}

public struct NonFrozenStructWithNonFrozenMember
{
    public var x: FrozenStruct
    public var y: NonFrozenStruct

    public init(x: FrozenStruct, y: NonFrozenStruct)
    {
        self.x = x
        self.y = y
    }

    public func getX() -> FrozenStruct
    {
        return x
    }

    public func getY() -> NonFrozenStruct
    {
        return y
    }

    public func sum() -> Int
    {
        return x.sum() + y.sum()
    }
}

@frozen
public struct FrozenStructWithNonFrozenMemberDeclaredWithinTheStruct
{
    public struct InnerStruct {
        public var innerField: Int

        public init(val: Int)
        {
            self.innerField = val
        }
    }

    public var x: InnerStruct

    public init(x: InnerStruct)
    {
        self.x = x
    }

    public func getInnerFieldValue() -> Int
    {
        return x.innerField
    }
}

public struct StructBuilder
{
    public let x: Int
    public let y: Int

    public init(x: Int, y: Int)
    {
        self.x = x
        self.y = y
    }

    public func createFrozenStruct() -> FrozenStruct
    {
        return FrozenStruct(x: x, y: y)
    }

    public func createNonFrozenStruct() -> NonFrozenStruct
    {
        return NonFrozenStruct(x: x, y: y)
    }

    public static func createFrozenStruct(x: Int, y: Int) -> FrozenStruct
    {
        return FrozenStruct(x: x, y: y)
    }

    public static func createNonFrozenStruct(x: Int, y: Int) -> NonFrozenStruct
    {
        return NonFrozenStruct(x: x, y: y)
    }
}

@frozen
public struct StructWithThrowingInit
{
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int) throws
    {
        self.x = x
        self.y = y

        throw NSError()
    }
}

public struct StructWithThrowingMethods
{
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int)
    {
        self.x = x
        self.y = y
    }

    public func sum() throws -> Int
    {
        throw NSError()
    }

    public static func sum(x: Int, y: Int) throws -> Int
    {
        throw NSError()
    }
}

public func sumFrozenAndNonFrozen(a: FrozenStruct, b: NonFrozenStruct) -> Int
{
    return a.sum() + b.sum()
}

public func createFrozenStruct(a: Int, b: Int) -> FrozenStruct
{
    return FrozenStruct(x: a, y: b)
}

public func createNonFrozenStruct(a: Int, b: Int) -> NonFrozenStruct
{
    return NonFrozenStruct(x: a, y: b)
}

public struct TimerStruct {
    let returnValue: Int32

    public init(returnValue: Int32) {
        self.returnValue = returnValue
    }

    public func waitFor(seconds: UInt64) async -> Int32 {
        do {
            try await Task.sleep(nanoseconds: seconds * 1_000_000_000)
        } catch {
            print("Failed to sleep: \(error)")
            return -1
        }
        return returnValue
    }

    public func waitFor5Seconds() async -> Void {
        try? await Task.sleep(nanoseconds: 5_000_000_000)
    }

    public static func waitFor5SecondsStatic() async -> Void {
        try? await Task.sleep(nanoseconds: 5_000_000_000)
    }
}

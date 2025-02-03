// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import Foundation

@frozen
public struct FrozenStruct
{
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int) {
        self.x = x
        self.y = y
    }
}

public struct NonFrozenStruct
{
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int) {
        self.x = x
        self.y = y
    }
}

public func AcceptsGenericParametersAndThrows<T, U> (a: T, b: U) throws -> Int {
    throw NSError()
}

public func AcceptsGenericParameters<T, U> (a: T, b: U) throws -> Int {
    if a is Int && b is Double {
        return 0;
    }

    if a is FrozenStruct && b is NonFrozenStruct {
        return 0;
    }

    throw NSError()
}

public func AcceptsGenericParameterAndReturnsGeneric<T>(a: T) -> T {
    return a;
}

public func AcceptsTwoValuesOfTheSameGenericType<T>(a: T, b: T) -> T {
    return a;
}

public protocol Summable {
    func sum() -> Int
}

public protocol Subtractable {
    func subtract() -> Int
}

public protocol Multiplicable {
    func multiply() -> Int
}

public protocol Dividable {
    func divide() -> Int
}

@frozen
public struct SummableStruct: Summable {
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int) {
        self.x = x
        self.y = y
    }

    public func sum() -> Int {
        return x + y
    }
}

@frozen 
public struct AnotherSummableStruct: Summable {
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int) {
        self.x = x
        self.y = y
    }

    public func sum() -> Int {
        return x + y
    }
}

public func AcceptsSummable<T: Summable>(a: T) -> Int {
    return a.sum()
}

public func AcceptsMultipleGenericParamsOfTheSameTypeConstrainedByProtocol<T: Summable>(a: T, b: T) -> Int {
    return a.sum() + b.sum()
}

public func AcceptsMultipleGenericParamsOfDifferentTypesConstrainedByTheSameProtocol<T: Summable, U: Summable>(a: T, b: U) -> Int {
    return a.sum() + b.sum()
}

public struct StructWithMultipleProtocols: Summable, Subtractable, Multiplicable, Dividable {
    public var x: Int
    public var y: Int

    public init(x: Int, y: Int) {
        self.x = x
        self.y = y
    }

    public func sum() -> Int {
        return x + y
    }

    public func subtract() -> Int {
        return x - y
    }

    public func multiply() -> Int {
        return x * y
    }

    public func divide() -> Int {
        return x / y
    }
}

public func AcceptsMultipleProtocols<T: Summable & Subtractable & Multiplicable>(a: T) -> Int {
    return a.sum() + a.subtract() + a.multiply()
}

public func AcceptsMultipleGenericParamsWithProtocols<T: Summable & Multiplicable, U: Subtractable & Dividable>(a: T, b: U) -> Int {
    return a.sum() + a.multiply() + b.subtract() + b.divide()
}

public protocol Container {
    associatedtype Element
    func increase() -> Element
}

public struct IntContainer1: Container {
    public var value: Int

    public init(value: Int) {
        self.value = value
    }

    public func increase() -> Int {
        return value * 2
    }
}

public struct IntContainer2: Container {
    public var value: Int

    public init(value: Int) {
        self.value = value
    }

    public func increase() -> Int {
        return value * 4
    }
}

public func AcceptsIntContainer<T: Container>(a: T) -> Int where T.Element == Int {
    return a.increase()
}

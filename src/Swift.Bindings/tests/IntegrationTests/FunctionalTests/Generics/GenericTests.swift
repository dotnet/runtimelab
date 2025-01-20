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

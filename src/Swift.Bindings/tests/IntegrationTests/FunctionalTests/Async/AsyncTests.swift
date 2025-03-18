// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import Foundation

public struct AsyncStruct {
    public let storedValue: Int32

    public init(_ storedValue: Int32) {
        self.storedValue = storedValue
    }

    public func AsyncVoid() async {
        try? await Task.sleep(nanoseconds: 1_000_000_000)
    }

    public func AsyncNonVoid(seconds: UInt64) async -> UInt64 {
        try? await Task.sleep(nanoseconds: seconds * 1_000_000_000)
        return seconds
    }

    public static func AsyncVoidStatic() async {
        try? await Task.sleep(nanoseconds: 1_000_000_000)
    }

    public static func AsyncNonVoidStatic(seconds: UInt64) async -> UInt64 {
        try? await Task.sleep(nanoseconds: seconds * 1_000_000_000)
        return seconds
    }

    public func GenericUnconstrained<T>(input: T) async {
        try? await Task.sleep(nanoseconds: 1_000_000_000)
    }

    public static func GenericUnconstrainedStatic<T>(input: T) async {
        try? await Task.sleep(nanoseconds: 1_000_000_000)
    }

    public func GenericCollectionConstraint<C>(input: C) async -> Int
        where C: Collection, C.Element == String
    {
        for identifier in input {
            if identifier == "error" {
                return -1
            }
        }
        try? await Task.sleep(nanoseconds: 1_000_000_000)
        return input.count
    }

    public static func GenericCollectionConstraintStatic<C>(input: C) async -> Int
        where C: Collection, C.Element == String
    {
        for identifier in input {
            if identifier == "error" {
                return -1
            }
        }
        try? await Task.sleep(nanoseconds: 1_000_000_000)
        return input.count
    }

    public func ArrayPassThrough(input: [String]) async -> [String] {
        try? await Task.sleep(nanoseconds: 1_000_000_000)
        return input
    }

    public func StringPassThrough(input: String) async -> String {
        try? await Task.sleep(nanoseconds: 1_000_000_000)
        return input
    }
}


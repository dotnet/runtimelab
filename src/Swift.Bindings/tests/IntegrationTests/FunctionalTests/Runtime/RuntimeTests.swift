// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import Foundation

public func getArray(count: Int32) -> Array<Int32> {
    var array = Array<Int32>()
    for i in 0..<count {
        array.append(i)
    }
    return array
}

public func sumArray(array: Array<Int32>) -> Int32
{
    return array.reduce(0, +)
}

public func getString(count: Int32) -> String {
    return String(repeating: "a", count: Int(count))
}

public func verifyString(str: String) -> Int32 {
    let count = str.count
    return str.allSatisfy { $0 == "a" } ? Int32(count) : -1
}


// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import Foundation

struct HasherFNV1a {

    private var hash: UInt = 14_695_981_039_346_656_037
    private let prime: UInt = 1_099_511_628_211

    mutating func combine<T>(_ val: T) {
        for byte in withUnsafeBytes(of: val, Array.init) {
            hash ^= UInt(byte)
            hash = hash &* prime
        }
    }

    func finalize() -> Int {
        Int(truncatingIfNeeded: hash)
    }
}

@frozen
public struct F0_S0
{
    public let f0 : Double;
    public let f1 : UInt32;
    public let f2 : UInt16;

    public init(f0 : Double, f1 : UInt32, f2 : UInt16) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        hasher.combine(f2)
        return hasher.finalize()
    }
}

public func swiftFunc0(a0: Int16, a1: Int32, a2: UInt64, a3: UInt16, a4: F0_S0, a5: F0_S1, a6: UInt8, a7: F0_S2) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1);
    hasher.combine(a2);
    hasher.combine(a3);
    hasher.combine(a4.f0);
    hasher.combine(a4.f1);
    hasher.combine(a4.f2);
    hasher.combine(a5.f0);
    hasher.combine(a6);
    hasher.combine(a7.f0);
    return hasher.finalize()
}

@frozen
public struct F0_S1
{
    public let f0 : UInt64;

    public init(f0 : UInt64) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

@frozen
public struct F0_S2
{
    public let f0 : Float;

    public init(f0 : Float) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

@frozen
public struct F1_S0
{
    public let f0 : Int64;
    public let f1 : Double;
    public let f2 : Int8;
    public let f3 : Int32;
    public let f4 : UInt16;

    public init(f0 : Int64, f1 : Double, f2 : Int8, f3 : Int32, f4 : UInt16) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
        self.f3 = f3
        self.f4 = f4
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        hasher.combine(f2)
        hasher.combine(f3)
        hasher.combine(f4)
        return hasher.finalize()
    }
}

@frozen
public struct F1_S1
{
    public let f0 : UInt8;

    public init(f0: UInt8) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

@frozen
public struct F1_S2
{
    public let f0 : Int16;

    public init(f0: Int16) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

public func swiftFunc1(a0: F1_S0, a1: UInt8, a2: F1_S1, a3: F1_S2) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0.f0);
    hasher.combine(a0.f1);
    hasher.combine(a0.f2);
    hasher.combine(a0.f3);
    hasher.combine(a0.f4);
    hasher.combine(a1);
    hasher.combine(a2.f0);
    hasher.combine(a3.f0);
    return hasher.finalize()
}

@frozen
public struct F2_S0
{
    public let f0 : Int;
    public let f1 : UInt;

    public init(f0: Int, f1: UInt) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F2_S1
{
    public let f0 : Int64;
    public let f1 : Int32;
    public let f2 : Int16;
    public let f3 : Int64;
    public let f4 : UInt16;

    public init(f0: Int64, f1: Int32, f2: Int16, f3: Int64, f4: UInt16) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
        self.f3 = f3
        self.f4 = f4
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        hasher.combine(f2)
        hasher.combine(f3)
        hasher.combine(f4)
        return hasher.finalize()
    }
}

@frozen
public struct F2_S2_S0_S0
{
    public let f0 : Int;

    public init(f0: Int) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

@frozen
public struct F2_S2_S0
{
    public let f0 : F2_S2_S0_S0;

    public init(f0: F2_S2_S0_S0) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0.f0)
        return hasher.finalize()
    }
}

@frozen
public struct F2_S2
{
    public let f0 : F2_S2_S0;

    public init(f0: F2_S2_S0) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0.f0)
        return hasher.finalize()
    }
}

@frozen
public struct F2_S3
{
    public let f0 : UInt8;

    public init(f0: UInt8) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

@frozen
public struct F2_S4
{
    public let f0 : Int32;
    public let f1 : UInt;

    public init(f0: Int32, f1: UInt) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F2_S5
{
    public let f0 : Float;

    public init(f0: Float) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

public func swiftFunc2(a0: Int64, a1: Int16, a2: Int32, a3: F2_S0, a4: UInt8, a5: Int32, a6: F2_S1, a7: F2_S2, a8: UInt16, a9: Float, a10: F2_S3, a11: F2_S4, a12: F2_S5, a13: Int64) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1);
    hasher.combine(a2);
    hasher.combine(a3.f0);
    hasher.combine(a3.f1);
    hasher.combine(a4);
    hasher.combine(a5);
    hasher.combine(a6.f0);
    hasher.combine(a6.f1);
    hasher.combine(a6.f2);
    hasher.combine(a6.f3);
    hasher.combine(a6.f4);
    hasher.combine(a7.f0.f0.f0);
    hasher.combine(a8);
    hasher.combine(a9);
    hasher.combine(a10.f0);
    hasher.combine(a11.f0);
    hasher.combine(a11.f1);
    hasher.combine(a12.f0);
    hasher.combine(a13);
    return hasher.finalize()
}

@frozen
public struct F3_S0_S0
{
    public let f0 : Int;
    public let f1 : UInt32;

    public init(f0: Int, f1: UInt32) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S0
{
    public let f0 : Int8;
    public let f1 : F3_S0_S0;
    public let f2 : UInt32;

    public init(f0: Int8, f1: F3_S0_S0, f2: UInt32) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1.f0)
        hasher.combine(f1.f1)
        hasher.combine(f2)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S1
{
    public let f0 : Int64;
    public let f1 : Float;

    public init(f0: Int64, f1: Float) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S2
{
    public let f0 : Float;

    public init(f0: Float) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S3
{
    public let f0 : UInt8;
    public let f1 : Int;

    public init(f0: UInt8, f1: Int) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S4
{
    public let f0 : UInt;
    public let f1 : Float;
    public let f2 : UInt16;

    public init(f0: UInt, f1: Float, f2: UInt16) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        hasher.combine(f2)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S5
{
    public let f0 : UInt32;
    public let f1 : Int64;

    public init(f0: UInt32, f1: Int64) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S6_S0
{
    public let f0 : Int16;
    public let f1 : UInt8;

    public init(f0: Int16, f1: UInt8) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S6
{
    public let f0 : F3_S6_S0;
    public let f1 : Int8;
    public let f2 : UInt8;

    public init(f0: F3_S6_S0, f1: Int8, f2: UInt8) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0.f0)
        hasher.combine(f0.f1)
        hasher.combine(f1)
        hasher.combine(f2)
        return hasher.finalize()
    }
}

@frozen
public struct F3_S7
{
    public let f0 : UInt64;

    public init(f0: UInt64) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

public func swiftFunc3(a0: Int, a1: F3_S0, a2: F3_S1, a3: Double, a4: Int, a5: F3_S2, a6: F3_S3, a7: F3_S4, a8: F3_S5, a9: UInt16, a10: Int32, a11: F3_S6, a12: Int, a13: F3_S7) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1.f0);
    hasher.combine(a1.f1.f0);
    hasher.combine(a1.f1.f1);
    hasher.combine(a1.f2);
    hasher.combine(a2.f0);
    hasher.combine(a2.f1);
    hasher.combine(a3);
    hasher.combine(a4);
    hasher.combine(a5.f0);
    hasher.combine(a6.f0);
    hasher.combine(a6.f1);
    hasher.combine(a7.f0);
    hasher.combine(a7.f1);
    hasher.combine(a7.f2);
    hasher.combine(a8.f0);
    hasher.combine(a8.f1);
    hasher.combine(a9);
    hasher.combine(a10);
    hasher.combine(a11.f0.f0);
    hasher.combine(a11.f0.f1);
    hasher.combine(a11.f1);
    hasher.combine(a11.f2);
    hasher.combine(a12);
    hasher.combine(a13.f0);
    return hasher.finalize()
}

@frozen
public struct F4_S0
{
    public let f0 : UInt16;
    public let f1 : Int16;
    public let f2 : Int16;

    public init(f0: UInt16, f1: Int16, f2: Int16) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        hasher.combine(f2)
        return hasher.finalize()
    }
}

@frozen
public struct F4_S1_S0
{
    public let f0 : UInt32;

    public init(f0: UInt32) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

@frozen
public struct F4_S1
{
    public let f0 : F4_S1_S0;
    public let f1 : Float;

    public init(f0: F4_S1_S0, f1: Float) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0.f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F4_S2_S0
{
    public let f0 : Int;

    public init(f0: Int) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

@frozen
public struct F4_S2
{
    public let f0 : F4_S2_S0;
    public let f1 : Int;

    public init(f0: F4_S2_S0, f1: Int) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0.f0)
        hasher.combine(f1)
        return hasher.finalize()
    }
}

@frozen
public struct F4_S3
{
    public let f0 : UInt64;
    public let f1 : UInt64;
    public let f2 : Int64;

    public init(f0: UInt64, f1: UInt64, f2: Int64) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        hasher.combine(f1)
        hasher.combine(f2)
        return hasher.finalize()
    }
}

public func swiftFunc4(a0: Int, a1: F4_S0, a2: UInt, a3: UInt64, a4: Int8, a5: Double, a6: F4_S1, a7: UInt8, a8: Int32, a9: UInt32, a10: UInt64, a11: F4_S2, a12: Int16, a13: Int, a14: F4_S3, a15: UInt32) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1.f0);
    hasher.combine(a1.f1);
    hasher.combine(a1.f2);
    hasher.combine(a2);
    hasher.combine(a3);
    hasher.combine(a4);
    hasher.combine(a5);
    hasher.combine(a6.f0.f0);
    hasher.combine(a6.f1);
    hasher.combine(a7);
    hasher.combine(a8);
    hasher.combine(a9);
    hasher.combine(a10);
    hasher.combine(a11.f0.f0);
    hasher.combine(a11.f1);
    hasher.combine(a12);
    hasher.combine(a13);
    hasher.combine(a14.f0);
    hasher.combine(a14.f1);
    hasher.combine(a14.f2);
    hasher.combine(a15);
    return hasher.finalize()
}

@frozen
public struct F5_S0
{
    public let f0 : UInt;

    public init(f0: UInt) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0)
        return hasher.finalize()
    }
}

public func swiftFunc5(a0: UInt, a1: UInt64, a2: UInt8, a3: F5_S0) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1);
    hasher.combine(a2);
    hasher.combine(a3.f0);
    return hasher.finalize()
}

@frozen
public struct F6_S0
{
    public let f0 : Int32;
    public let f1 : Int;
    public let f2 : UInt8;

    public init(f0: Int32, f1: Int, f2: UInt8) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        hasher.combine(f1);
        hasher.combine(f2);
        return hasher.finalize()
    }
}

@frozen
public struct F6_S1
{
    public let f0 : Int;
    public let f1 : Float;

    public init(f0: Int, f1: Float) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        hasher.combine(f1);
        return hasher.finalize()
    }
}

@frozen
public struct F6_S2_S0
{
    public let f0 : Double;

    public init(f0: Double) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

@frozen
public struct F6_S2
{
    public let f0 : F6_S2_S0;
    public let f1 : UInt16;

    public init(f0: F6_S2_S0, f1: UInt16) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0.f0);
        hasher.combine(f1);
        return hasher.finalize()
    }
}

@frozen
public struct F6_S3
{
    public let f0 : Double;
    public let f1 : Double;
    public let f2 : UInt64;

    public init(f0: Double, f1: Double, f2: UInt64) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        hasher.combine(f1);
        hasher.combine(f2);
        return hasher.finalize()
    }
}

@frozen
public struct F6_S4
{
    public let f0 : Int8;

    public init(f0: Int8) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

@frozen
public struct F6_S5
{
    public let f0 : Int16;

    public init(f0: Int16) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

public func swiftFunc6(a0: Int64, a1: F6_S0, a2: F6_S1, a3: UInt, a4: UInt8, a5: Int32, a6: F6_S2, a7: Float, a8: Int16, a9: F6_S3, a10: UInt16, a11: Double, a12: UInt32, a13: F6_S4, a14: F6_S5) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1.f0);
    hasher.combine(a1.f1);
    hasher.combine(a1.f2);
    hasher.combine(a2.f0);
    hasher.combine(a2.f1);
    hasher.combine(a3);
    hasher.combine(a4);
    hasher.combine(a5);
    hasher.combine(a6.f0.f0);
    hasher.combine(a6.f1);
    hasher.combine(a7);
    hasher.combine(a8);
    hasher.combine(a9.f0);
    hasher.combine(a9.f1);
    hasher.combine(a9.f2);
    hasher.combine(a10);
    hasher.combine(a11);
    hasher.combine(a12);
    hasher.combine(a13.f0);
    hasher.combine(a14.f0);
    return hasher.finalize()
}

@frozen
public struct F7_S0
{
    public let f0 : Int16;
    public let f1 : Int;

    public init(f0: Int16, f1: Int) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        hasher.combine(f1);
        return hasher.finalize()
    }
}

@frozen
public struct F7_S1
{
    public let f0 : UInt8;

    public init(f0: UInt8) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

public func swiftFunc7(a0: Int64, a1: Int, a2: UInt8, a3: F7_S0, a4: F7_S1, a5: UInt32) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1);
    hasher.combine(a2);
    hasher.combine(a3.f0);
    hasher.combine(a3.f1);
    hasher.combine(a4.f0);
    hasher.combine(a5);
    return hasher.finalize()
}

@frozen
public struct F8_S0
{
    public let f0 : Int32;

    public init(f0: Int32) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

public func swiftFunc8(a0: UInt16, a1: UInt, a2: UInt16, a3: UInt64, a4: F8_S0, a5: UInt64) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1);
    hasher.combine(a2);
    hasher.combine(a3);
    hasher.combine(a4.f0);
    hasher.combine(a5);
    return hasher.finalize()
}

@frozen
public struct F9_S0
{
    public let f0 : Double;

    public init(f0: Double) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

@frozen
public struct F9_S1
{
    public let f0 : Int32;

    public init(f0: Int32) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

public func swiftFunc9(a0: Int64, a1: Float, a2: F9_S0, a3: UInt16, a4: F9_S1, a5: UInt16) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1);
    hasher.combine(a2.f0);
    hasher.combine(a3);
    hasher.combine(a4.f0);
    hasher.combine(a5);
    return hasher.finalize()
}

@frozen
public struct F10_S0
{
    public let f0 : Int64;
    public let f1 : UInt32;

    public init(f0: Int64, f1: UInt32) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        hasher.combine(f1);
        return hasher.finalize()
    }
}

@frozen
public struct F10_S1
{
    public let f0 : Float;
    public let f1 : UInt8;
    public let f2 : UInt;

    public init(f0: Float, f1: UInt8, f2: UInt) {
        self.f0 = f0
        self.f1 = f1
        self.f2 = f2
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        hasher.combine(f1);
        hasher.combine(f2);
        return hasher.finalize()
    }
}

@frozen
public struct F10_S2
{
    public let f0 : UInt;
    public let f1 : UInt64;

    public init(f0: UInt, f1: UInt64) {
        self.f0 = f0
        self.f1 = f1
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        hasher.combine(f1);
        return hasher.finalize()
    }
}

@frozen
public struct F10_S3
{
    public let f0 : Float;

    public init(f0: Float) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

@frozen
public struct F10_S4
{
    public let f0 : Int64;

    public init(f0: Int64) {
        self.f0 = f0
    }

    public func hashValue() -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(f0);
        return hasher.finalize()
    }
}

public func swiftFunc10(a0: UInt16, a1: UInt16, a2: F10_S0, a3: UInt64, a4: Float, a5: Int8, a6: Int64, a7: UInt64, a8: Int64, a9: Float, a10: Int32, a11: Int32, a12: Int64, a13: UInt64, a14: F10_S1, a15: Int64, a16: F10_S2, a17: F10_S3, a18: F10_S4) -> Int {
    var hasher = HasherFNV1a()
    hasher.combine(a0);
    hasher.combine(a1);
    hasher.combine(a2.f0);
    hasher.combine(a2.f1);
    hasher.combine(a3);
    hasher.combine(a4);
    hasher.combine(a5);
    hasher.combine(a6);
    hasher.combine(a7);
    hasher.combine(a8);
    hasher.combine(a9);
    hasher.combine(a10);
    hasher.combine(a11);
    hasher.combine(a12);
    hasher.combine(a13);
    hasher.combine(a14.f0);
    hasher.combine(a14.f1);
    hasher.combine(a14.f2);
    hasher.combine(a15);
    hasher.combine(a16.f0);
    hasher.combine(a16.f1);
    hasher.combine(a17.f0);
    hasher.combine(a18.f0);
    return hasher.finalize()
}

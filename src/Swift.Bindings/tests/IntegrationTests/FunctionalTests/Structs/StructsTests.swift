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

// @frozen 
// public struct FrozenStructWithNonFrozenMember
// {
//     public var x: FrozenStruct
//     public var y: NonFrozenStruct

//     public init(x: FrozenStruct, y: NonFrozenStruct)
//     {
//         self.x = x
//         self.y = y
//     }

//     public func getX() -> FrozenStruct
//     {
//         return x
//     }

//     public func getY() -> NonFrozenStruct
//     {
//         return y
//     }

//     public func sum(a: NonFrozenStruct, b: FrozenStruct) -> Int
//     {
//         return x.sum() + y.sum() + a.sum() + b.sum()
//     }
// }

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

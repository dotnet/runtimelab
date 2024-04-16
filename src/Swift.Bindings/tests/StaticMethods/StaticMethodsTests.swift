// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

public enum Type1 {
    public static func swiftFunc0(a0: UInt64, a1: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public class Type1Sub2 {
        public static func swiftFunc0(a0: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public class Type1Sub2Sub3 {
            public static func swiftFunc0(a0: Int16, a1: UInt, a2: UInt64, a3: Int32, a4: UInt, a5: UInt, a6: UInt64, a7: UInt8, a8: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public class Type1Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt, a1: Int, a2: Int, a3: Double, a4: Int32, a5: Int64, a6: UInt32, a7: UInt16, a8: Int, a9: Int32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    hasher.combine(a9);
                    return hasher.finalize()
                }
                public enum Type1Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int16, a1: UInt64, a2: Int64, a3: Int16, a4: UInt32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        return hasher.finalize()
                    }
                    public class Type1Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int32, a1: UInt8, a2: Int8, a3: UInt16, a4: Int, a5: Int, a6: UInt16, a7: Int32, a8: Int32, a9: Double) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            hasher.combine(a7);
                            hasher.combine(a8);
                            hasher.combine(a9);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type2 {
    public static func swiftFunc0(a0: UInt8, a1: UInt64, a2: Int64, a3: UInt64, a4: Int32, a5: Int64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public enum Type2Sub2 {
        public static func swiftFunc0(a0: Int64, a1: Int, a2: UInt) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public enum Type2Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: Int16, a2: UInt32, a3: Int64, a4: Double, a5: UInt16, a6: Int64, a7: Int8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public enum Type2Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt32, a1: UInt64, a2: Int16, a3: Int, a4: UInt, a5: UInt32, a6: UInt8, a7: UInt16, a8: UInt32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
                public struct Type2Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int32, a1: UInt8, a2: UInt16, a3: Double, a4: Int8, a5: Double, a6: UInt64, a7: Int8, a8: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        return hasher.finalize()
                    }
                    public struct Type2Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int8, a1: UInt16, a2: UInt32, a3: UInt, a4: Double, a5: UInt16, a6: Int32) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            return hasher.finalize()
                        }
                        public struct Type2Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: UInt8, a1: Double, a2: UInt, a3: UInt8, a4: Double) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                return hasher.finalize()
                            }
                            public class Type2Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: UInt, a1: Double, a2: Double, a3: UInt64, a4: Double) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    hasher.combine(a4);
                                    return hasher.finalize()
                                }
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type3 {
    public static func swiftFunc0(a0: UInt16, a1: Int64, a2: UInt16, a3: Int64, a4: UInt8, a5: UInt64, a6: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public enum Type3Sub2 {
        public static func swiftFunc0(a0: Double, a1: Int16, a2: Double, a3: UInt, a4: UInt8, a5: UInt, a6: Int64, a7: Double, a8: UInt) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public struct Type3Sub2Sub3 {
            public static func swiftFunc0(a0: UInt16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                return hasher.finalize()
            }
            public class Type3Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt8, a1: UInt8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    return hasher.finalize()
                }
                public class Type3Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int64, a1: Int8, a2: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        return hasher.finalize()
                    }
                    public class Type3Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int16, a1: UInt, a2: Double, a3: UInt64, a4: UInt8, a5: Double) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            return hasher.finalize()
                        }
                        public struct Type3Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Double) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                return hasher.finalize()
                            }
                            public struct Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Double, a1: Int32, a2: Int32, a3: UInt16, a4: Int16, a5: UInt32) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    hasher.combine(a4);
                                    hasher.combine(a5);
                                    return hasher.finalize()
                                }
                                public enum Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: UInt8, a1: Double, a2: UInt64, a3: Int8) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        hasher.combine(a2);
                                        hasher.combine(a3);
                                        return hasher.finalize()
                                    }
                                    public enum Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10 {
                                        public static func swiftFunc0(a0: Int, a1: UInt, a2: UInt64, a3: UInt32, a4: UInt64, a5: Int64, a6: UInt32) -> Int {
                                            var hasher = HasherFNV1a()
                                            hasher.combine(a0);
                                            hasher.combine(a1);
                                            hasher.combine(a2);
                                            hasher.combine(a3);
                                            hasher.combine(a4);
                                            hasher.combine(a5);
                                            hasher.combine(a6);
                                            return hasher.finalize()
                                        }
                                        public class Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11 {
                                            public static func swiftFunc0(a0: UInt) -> Int {
                                                var hasher = HasherFNV1a()
                                                hasher.combine(a0);
                                                return hasher.finalize()
                                            }
                                            public class Type3Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11Sub12 {
                                                public static func swiftFunc0(a0: UInt8, a1: Double, a2: Double, a3: UInt64, a4: UInt32) -> Int {
                                                    var hasher = HasherFNV1a()
                                                    hasher.combine(a0);
                                                    hasher.combine(a1);
                                                    hasher.combine(a2);
                                                    hasher.combine(a3);
                                                    hasher.combine(a4);
                                                    return hasher.finalize()
                                                }
                                            }
                                            
                                        }
                                        
                                    }
                                    
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type4 {
    public static func swiftFunc0(a0: UInt, a1: Int8, a2: Double, a3: Int16, a4: Double, a5: Int8, a6: Int32, a7: UInt16, a8: UInt, a9: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        hasher.combine(a9);
        return hasher.finalize()
    }
}

public enum Type5 {
    public static func swiftFunc0(a0: UInt8, a1: Double, a2: Int8, a3: Int8, a4: UInt16, a5: Int64, a6: UInt64, a7: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public struct Type5Sub2 {
        public static func swiftFunc0(a0: Int16, a1: UInt32, a2: Int32, a3: UInt64, a4: Int, a5: Int16, a6: Double, a7: UInt8, a8: Int8, a9: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            hasher.combine(a9);
            return hasher.finalize()
        }
    }
    
}

public struct Type6 {
    public static func swiftFunc0(a0: Int8, a1: Int16, a2: UInt, a3: UInt32, a4: Int, a5: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public class Type6Sub2 {
        public static func swiftFunc0(a0: Int32, a1: Int8, a2: UInt, a3: UInt8, a4: UInt8, a5: Int32, a6: UInt, a7: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            return hasher.finalize()
        }
        public class Type6Sub2Sub3 {
            public static func swiftFunc0(a0: UInt32, a1: UInt16, a2: Int32, a3: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                return hasher.finalize()
            }
            public struct Type6Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt8, a1: UInt64, a2: UInt, a3: UInt8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    return hasher.finalize()
                }
                public struct Type6Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt32, a1: UInt64, a2: UInt32, a3: UInt, a4: UInt8, a5: Double, a6: Int16, a7: Int32, a8: Int8, a9: Int) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        hasher.combine(a9);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public struct Type7 {
    public static func swiftFunc0(a0: UInt8, a1: Int64, a2: Int32, a3: UInt64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        return hasher.finalize()
    }
    public enum Type7Sub2 {
        public static func swiftFunc0(a0: Int8, a1: Int32, a2: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public enum Type7Sub2Sub3 {
            public static func swiftFunc0(a0: Int16, a1: UInt, a2: UInt8, a3: UInt8, a4: Double, a5: UInt32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
            public struct Type7Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt8, a1: Double, a2: Int16, a3: Int, a4: UInt32, a5: Int32, a6: Double, a7: UInt32, a8: Int) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
                public struct Type7Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int64, a1: UInt32, a2: Double, a3: Int16) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        return hasher.finalize()
                    }
                    public enum Type7Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int16, a1: UInt16, a2: UInt64, a3: UInt64, a4: Int64, a5: Int8, a6: Double, a7: Int, a8: Int) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            hasher.combine(a7);
                            hasher.combine(a8);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type8 {
    public static func swiftFunc0(a0: UInt, a1: Int16, a2: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        return hasher.finalize()
    }
    public enum Type8Sub2 {
        public static func swiftFunc0(a0: Int32, a1: Int32, a2: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public class Type8Sub2Sub3 {
            public static func swiftFunc0(a0: Int32, a1: UInt32, a2: UInt8, a3: Int8, a4: Int, a5: UInt, a6: UInt, a7: UInt32, a8: UInt64, a9: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                hasher.combine(a9);
                return hasher.finalize()
            }
            public enum Type8Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt, a1: UInt8, a2: UInt8, a3: Double, a4: UInt16) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    return hasher.finalize()
                }
                public struct Type8Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int, a1: Double, a2: Int, a3: Int16, a4: UInt32, a5: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public enum Type9 {
    public static func swiftFunc0(a0: Int32, a1: Int8, a2: UInt8, a3: UInt32, a4: Int, a5: UInt16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public struct Type9Sub2 {
        public static func swiftFunc0(a0: UInt32, a1: Int, a2: Double, a3: Int64, a4: UInt8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public class Type9Sub2Sub3 {
            public static func swiftFunc0(a0: UInt, a1: Int16, a2: Int32, a3: Int, a4: Int32, a5: UInt, a6: Double, a7: Int) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
        }
        
    }
    
}

public enum Type10 {
    public static func swiftFunc0(a0: UInt32, a1: Int8, a2: UInt, a3: UInt8, a4: UInt32, a5: UInt, a6: UInt64, a7: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public struct Type10Sub2 {
        public static func swiftFunc0(a0: Int, a1: UInt32, a2: UInt8, a3: Int32, a4: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public class Type10Sub2Sub3 {
            public static func swiftFunc0(a0: UInt, a1: UInt64, a2: Double, a3: UInt64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                return hasher.finalize()
            }
        }
        
    }
    
}

public class Type11 {
    public static func swiftFunc0(a0: UInt, a1: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public struct Type11Sub2 {
        public static func swiftFunc0(a0: UInt) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public struct Type11Sub2Sub3 {
            public static func swiftFunc0(a0: UInt16, a1: Int64, a2: Double, a3: Int16, a4: Double, a5: UInt16, a6: Int8, a7: Int64, a8: UInt16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public struct Type11Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt64, a1: Double, a2: UInt, a3: UInt32, a4: UInt8, a5: UInt, a6: Double, a7: Int16, a8: UInt32, a9: Int32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    hasher.combine(a9);
                    return hasher.finalize()
                }
                public struct Type11Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int, a1: Int16, a2: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        return hasher.finalize()
                    }
                    public enum Type11Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: UInt8, a1: UInt16, a2: UInt8, a3: UInt) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            return hasher.finalize()
                        }
                        public class Type11Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Double, a1: Double, a2: Int64, a3: Int8) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                return hasher.finalize()
                            }
                            public class Type11Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Double, a1: Int32, a2: Int16, a3: Int64, a4: UInt64, a5: UInt32, a6: Int8, a7: UInt, a8: UInt8, a9: Int) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    hasher.combine(a4);
                                    hasher.combine(a5);
                                    hasher.combine(a6);
                                    hasher.combine(a7);
                                    hasher.combine(a8);
                                    hasher.combine(a9);
                                    return hasher.finalize()
                                }
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type12 {
    public static func swiftFunc0(a0: Double, a1: UInt8, a2: UInt16, a3: UInt8, a4: UInt, a5: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public class Type12Sub2 {
        public static func swiftFunc0(a0: UInt8, a1: Int, a2: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
    }
    
}

public class Type13 {
    public static func swiftFunc0(a0: UInt, a1: UInt64, a2: Double, a3: Int8, a4: Int32, a5: Double, a6: UInt, a7: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public enum Type13Sub2 {
        public static func swiftFunc0(a0: Int32, a1: Int32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            return hasher.finalize()
        }
        public enum Type13Sub2Sub3 {
            public static func swiftFunc0(a0: Int16, a1: Double, a2: Int, a3: UInt32, a4: Int16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                return hasher.finalize()
            }
            public struct Type13Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int, a1: Int64, a2: UInt8, a3: Int8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    return hasher.finalize()
                }
                public enum Type13Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int32, a1: Int, a2: Double, a3: UInt8, a4: UInt8, a5: Double, a6: UInt32, a7: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        return hasher.finalize()
                    }
                    public struct Type13Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int32, a1: UInt) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            return hasher.finalize()
                        }
                        public enum Type13Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: UInt8, a1: Int8, a2: Double) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                return hasher.finalize()
                            }
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type14 {
    public static func swiftFunc0(a0: UInt32, a1: Double, a2: UInt16, a3: Int16, a4: Int32, a5: Int16, a6: Int32, a7: Int64, a8: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
}

public class Type15 {
    public static func swiftFunc0(a0: UInt, a1: Int16, a2: Int8, a3: UInt16, a4: UInt8, a5: Int32, a6: Int, a7: Double, a8: Int64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public class Type15Sub2 {
        public static func swiftFunc0(a0: Double, a1: Double, a2: UInt32, a3: UInt32, a4: UInt8, a5: Int64, a6: UInt16, a7: Int64, a8: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public enum Type15Sub2Sub3 {
            public static func swiftFunc0(a0: Int8, a1: Int32, a2: Double, a3: UInt32, a4: Int16, a5: Int, a6: Double, a7: Int16, a8: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
        }
        
    }
    
}

public enum Type16 {
    public static func swiftFunc0(a0: Int64, a1: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public enum Type16Sub2 {
        public static func swiftFunc0(a0: Double, a1: Double, a2: UInt16, a3: Double, a4: Double, a5: Int64, a6: Int64, a7: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            return hasher.finalize()
        }
    }
    
}

public enum Type17 {
    public static func swiftFunc0(a0: Int8, a1: UInt64, a2: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        return hasher.finalize()
    }
    public struct Type17Sub2 {
        public static func swiftFunc0(a0: Int64, a1: UInt64, a2: UInt32, a3: UInt8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
        public enum Type17Sub2Sub3 {
            public static func swiftFunc0(a0: UInt32, a1: Int8, a2: Int, a3: UInt64, a4: UInt32, a5: UInt, a6: UInt16, a7: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public class Type17Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int8, a1: Int, a2: Int, a3: Double) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public enum Type18 {
    public static func swiftFunc0(a0: Double, a1: UInt32, a2: UInt16, a3: Double, a4: Int8, a5: Int64, a6: Int32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public class Type18Sub2 {
        public static func swiftFunc0(a0: UInt64, a1: Int8, a2: UInt8, a3: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
    }
    
}

public enum Type19 {
    public static func swiftFunc0(a0: UInt32, a1: Int, a2: Int16, a3: UInt, a4: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public struct Type19Sub2 {
        public static func swiftFunc0(a0: Double, a1: Double, a2: Int, a3: Double, a4: UInt16, a5: UInt8, a6: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            return hasher.finalize()
        }
        public class Type19Sub2Sub3 {
            public static func swiftFunc0(a0: Double, a1: UInt, a2: UInt64, a3: Double, a4: UInt32, a5: Int16, a6: UInt32, a7: Int) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public class Type19Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: Int32, a2: Double, a3: Double, a4: Double, a5: UInt64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    return hasher.finalize()
                }
                public struct Type19Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int64, a1: UInt, a2: Double, a3: Double, a4: UInt16) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        return hasher.finalize()
                    }
                    public enum Type19Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            return hasher.finalize()
                        }
                        public enum Type19Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Int8, a1: Double, a2: UInt8, a3: Double, a4: Int64, a5: UInt8, a6: Int16, a7: Double) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                hasher.combine(a5);
                                hasher.combine(a6);
                                hasher.combine(a7);
                                return hasher.finalize()
                            }
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type20 {
    public static func swiftFunc0(a0: Int, a1: UInt8, a2: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        return hasher.finalize()
    }
    public enum Type20Sub2 {
        public static func swiftFunc0(a0: Int16, a1: UInt32, a2: Int16, a3: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
        public class Type20Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: UInt, a2: UInt32, a3: Int16, a4: UInt8, a5: UInt8, a6: Int, a7: Double, a8: Int32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public struct Type20Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt32, a1: UInt16, a2: UInt32, a3: UInt32, a4: UInt32, a5: Double, a6: Int32, a7: Int64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    return hasher.finalize()
                }
                public enum Type20Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt16) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public struct Type21 {
    public static func swiftFunc0(a0: UInt, a1: UInt, a2: UInt16, a3: UInt32, a4: Int8, a5: UInt16, a6: Int32, a7: Int32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public class Type21Sub2 {
        public static func swiftFunc0(a0: Int, a1: Int32, a2: UInt8, a3: UInt32, a4: Double, a5: Int32, a6: UInt8, a7: Int64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            return hasher.finalize()
        }
        public enum Type21Sub2Sub3 {
            public static func swiftFunc0(a0: Int8, a1: Int64, a2: UInt, a3: Double, a4: Int8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                return hasher.finalize()
            }
        }
        
    }
    
}

public enum Type22 {
    public static func swiftFunc0(a0: Double, a1: Int, a2: UInt, a3: Double, a4: Int64, a5: UInt16, a6: Int64, a7: Double, a8: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public struct Type22Sub2 {
        public static func swiftFunc0(a0: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public enum Type22Sub2Sub3 {
            public static func swiftFunc0(a0: UInt64, a1: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
        }
        
    }
    
}

public struct Type23 {
    public static func swiftFunc0(a0: Int8, a1: Double, a2: Double, a3: Int32, a4: Double, a5: UInt8, a6: Int32, a7: UInt32, a8: Int8, a9: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        hasher.combine(a9);
        return hasher.finalize()
    }
    public class Type23Sub2 {
        public static func swiftFunc0(a0: Int8, a1: UInt64, a2: Int64, a3: Int32, a4: Int8, a5: Int16, a6: UInt32, a7: Int16, a8: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public class Type23Sub2Sub3 {
            public static func swiftFunc0(a0: UInt, a1: UInt, a2: Int32, a3: Double, a4: Int16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                return hasher.finalize()
            }
        }
        
    }
    
}

public enum Type24 {
    public static func swiftFunc0(a0: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public struct Type24Sub2 {
        public static func swiftFunc0(a0: UInt16, a1: Double, a2: UInt8, a3: UInt, a4: Int32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public struct Type24Sub2Sub3 {
            public static func swiftFunc0(a0: UInt8, a1: UInt16, a2: UInt64, a3: Int32, a4: Int16, a5: UInt, a6: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                return hasher.finalize()
            }
            public struct Type24Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    return hasher.finalize()
                }
                public struct Type24Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int, a1: UInt32, a2: UInt8, a3: UInt32, a4: UInt64) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public class Type25 {
    public static func swiftFunc0(a0: UInt, a1: Double, a2: Int, a3: Double, a4: UInt64, a5: Double, a6: UInt16, a7: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public struct Type25Sub2 {
        public static func swiftFunc0(a0: Int16, a1: Int64, a2: UInt16, a3: UInt64, a4: UInt, a5: Int, a6: UInt16, a7: UInt16, a8: Int32, a9: Int32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            hasher.combine(a9);
            return hasher.finalize()
        }
        public struct Type25Sub2Sub3 {
            public static func swiftFunc0(a0: UInt64, a1: Int, a2: Int16, a3: Int) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                return hasher.finalize()
            }
            public enum Type25Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int, a1: Int, a2: UInt16) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
                public struct Type25Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int16, a1: UInt, a2: UInt32, a3: Int8, a4: UInt64, a5: Int8) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                    public class Type25Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: UInt, a1: Int8, a2: UInt16, a3: UInt8, a4: Double) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            return hasher.finalize()
                        }
                        public struct Type25Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Int8, a1: Int64, a2: UInt16, a3: Int16, a4: Double, a5: UInt16) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                hasher.combine(a5);
                                return hasher.finalize()
                            }
                            public enum Type25Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Double, a1: UInt64, a2: Int8) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    return hasher.finalize()
                                }
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type26 {
    public static func swiftFunc0(a0: UInt16, a1: Int8, a2: UInt32, a3: Int32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        return hasher.finalize()
    }
}

public struct Type27 {
    public static func swiftFunc0(a0: Int32, a1: UInt16, a2: UInt8, a3: UInt16, a4: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public class Type27Sub2 {
        public static func swiftFunc0(a0: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public class Type27Sub2Sub3 {
            public static func swiftFunc0(a0: UInt64, a1: Int64, a2: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                return hasher.finalize()
            }
            public struct Type27Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int8, a1: UInt32, a2: UInt16, a3: UInt, a4: Double, a5: UInt16, a6: UInt64, a7: Int) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    return hasher.finalize()
                }
                public class Type27Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double, a1: Double, a2: Int8, a3: Int8, a4: Int, a5: UInt, a6: Double, a7: Int, a8: UInt32, a9: UInt32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        hasher.combine(a9);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public class Type28 {
    public static func swiftFunc0(a0: Int16, a1: Int8, a2: UInt16, a3: Double, a4: Int) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public class Type28Sub2 {
        public static func swiftFunc0(a0: Int64, a1: UInt, a2: Int8, a3: Int32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
        public class Type28Sub2Sub3 {
            public static func swiftFunc0(a0: Int, a1: Int, a2: Int, a3: UInt16, a4: Int32, a5: Int16, a6: UInt32, a7: Int32, a8: UInt16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public class Type28Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int8, a1: Int64, a2: Int32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
                public enum Type28Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double, a1: Int64, a2: Int16, a3: Int64, a4: Int16, a5: UInt8, a6: UInt8, a7: Int16, a8: UInt64, a9: UInt16) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        hasher.combine(a9);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public struct Type29 {
    public static func swiftFunc0(a0: Double, a1: UInt32, a2: UInt64, a3: Int8, a4: UInt8, a5: UInt16, a6: Int8, a7: Double, a8: UInt8, a9: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        hasher.combine(a9);
        return hasher.finalize()
    }
    public class Type29Sub2 {
        public static func swiftFunc0(a0: Double, a1: UInt64, a2: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public struct Type29Sub2Sub3 {
            public static func swiftFunc0(a0: Int8, a1: Int64, a2: Int8, a3: UInt, a4: Double, a5: UInt64, a6: UInt, a7: UInt64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public struct Type29Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int, a1: Int8, a2: Double, a3: UInt8, a4: UInt32, a5: Double, a6: UInt16, a7: Int32, a8: Int) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public struct Type30 {
    public static func swiftFunc0(a0: Double, a1: Double, a2: UInt64, a3: Double, a4: Double, a5: Int, a6: Int64, a7: UInt64, a8: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public class Type30Sub2 {
        public static func swiftFunc0(a0: UInt, a1: UInt, a2: Double, a3: Int64, a4: Int32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public enum Type30Sub2Sub3 {
            public static func swiftFunc0(a0: Int16, a1: Int64, a2: UInt8, a3: Int64, a4: UInt16, a5: UInt64, a6: UInt64, a7: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
        }
        
    }
    
}

public class Type31 {
    public static func swiftFunc0(a0: UInt, a1: Double, a2: Int, a3: UInt8, a4: Double, a5: Int8, a6: Int, a7: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public struct Type31Sub2 {
        public static func swiftFunc0(a0: Int32, a1: Int16, a2: Int, a3: Int64, a4: Int64, a5: UInt, a6: UInt16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            return hasher.finalize()
        }
        public enum Type31Sub2Sub3 {
            public static func swiftFunc0(a0: Int, a1: Int16, a2: Int64, a3: Double, a4: UInt16, a5: UInt8, a6: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                return hasher.finalize()
            }
            public enum Type31Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt16, a1: Int32, a2: UInt, a3: UInt16, a4: Int8, a5: UInt16, a6: UInt, a7: Int8, a8: Int8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
                public class Type31Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double, a1: UInt) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        return hasher.finalize()
                    }
                    public struct Type31Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int8, a1: UInt, a2: Int, a3: Int64, a4: UInt64, a5: UInt, a6: Int64) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            return hasher.finalize()
                        }
                        public struct Type31Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Double) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                return hasher.finalize()
                            }
                            public struct Type31Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Int8, a1: Double, a2: Int, a3: Double, a4: Int16, a5: Int16, a6: Double, a7: Int32, a8: Int8) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    hasher.combine(a4);
                                    hasher.combine(a5);
                                    hasher.combine(a6);
                                    hasher.combine(a7);
                                    hasher.combine(a8);
                                    return hasher.finalize()
                                }
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public struct Type32 {
    public static func swiftFunc0(a0: Int32, a1: UInt16, a2: UInt64, a3: Int64, a4: Double, a5: UInt, a6: Int32, a7: Int, a8: UInt64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public struct Type32Sub2 {
        public static func swiftFunc0(a0: Double, a1: Int16, a2: Int, a3: UInt8, a4: Double, a5: Double, a6: Int16, a7: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            return hasher.finalize()
        }
    }
    
}

public class Type33 {
    public static func swiftFunc0(a0: UInt8, a1: UInt16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public class Type33Sub2 {
        public static func swiftFunc0(a0: Int64, a1: UInt32, a2: UInt32, a3: Int8, a4: UInt64, a5: Int16, a6: Int64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            return hasher.finalize()
        }
        public enum Type33Sub2Sub3 {
            public static func swiftFunc0(a0: UInt32, a1: Double, a2: UInt8, a3: Double, a4: Int, a5: UInt32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
            public enum Type33Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int32, a1: UInt64, a2: Int8, a3: Int32, a4: Int64, a5: Int64, a6: Double, a7: UInt8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public struct Type34 {
    public static func swiftFunc0(a0: UInt8, a1: UInt32, a2: UInt64, a3: Int8, a4: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public class Type34Sub2 {
        public static func swiftFunc0(a0: Double, a1: Int64, a2: Int, a3: Int, a4: UInt16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public struct Type34Sub2Sub3 {
            public static func swiftFunc0(a0: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                return hasher.finalize()
            }
            public struct Type34Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: Int64, a2: Double, a3: UInt32, a4: UInt8, a5: Int) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    return hasher.finalize()
                }
                public class Type34Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt, a1: Double, a2: Int64, a3: UInt8, a4: UInt8, a5: Int64, a6: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        return hasher.finalize()
                    }
                    public struct Type34Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int16, a1: Int64, a2: Int, a3: Double, a4: UInt64, a5: Int64, a6: UInt, a7: Int16, a8: Int16) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            hasher.combine(a7);
                            hasher.combine(a8);
                            return hasher.finalize()
                        }
                        public enum Type34Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Int8, a1: UInt64, a2: UInt32) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                return hasher.finalize()
                            }
                            public struct Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Int, a1: Double) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    return hasher.finalize()
                                }
                                public class Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: UInt64, a1: Int, a2: UInt, a3: Double, a4: UInt64, a5: UInt8, a6: Int8) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        hasher.combine(a2);
                                        hasher.combine(a3);
                                        hasher.combine(a4);
                                        hasher.combine(a5);
                                        hasher.combine(a6);
                                        return hasher.finalize()
                                    }
                                    public struct Type34Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10 {
                                        public static func swiftFunc0(a0: UInt, a1: Int8, a2: Int32, a3: UInt16, a4: UInt16, a5: Int) -> Int {
                                            var hasher = HasherFNV1a()
                                            hasher.combine(a0);
                                            hasher.combine(a1);
                                            hasher.combine(a2);
                                            hasher.combine(a3);
                                            hasher.combine(a4);
                                            hasher.combine(a5);
                                            return hasher.finalize()
                                        }
                                    }
                                    
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type35 {
    public static func swiftFunc0(a0: Int8, a1: UInt8, a2: Int8, a3: Int32, a4: Int16, a5: Int64, a6: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public class Type35Sub2 {
        public static func swiftFunc0(a0: Int8, a1: UInt32, a2: Int64, a3: Double, a4: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public class Type35Sub2Sub3 {
            public static func swiftFunc0(a0: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                return hasher.finalize()
            }
            public struct Type35Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: Double, a2: UInt, a3: UInt8, a4: Int, a5: UInt8, a6: Int16, a7: UInt64, a8: Int16) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
                public class Type35Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int16) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public struct Type36 {
    public static func swiftFunc0(a0: UInt16, a1: UInt32, a2: Double, a3: Double, a4: UInt64, a5: UInt16, a6: UInt8, a7: UInt32, a8: Int) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public class Type36Sub2 {
        public static func swiftFunc0(a0: UInt8, a1: Int32, a2: Int32, a3: Int64, a4: UInt32, a5: Double, a6: Int8, a7: Double, a8: UInt8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public enum Type36Sub2Sub3 {
            public static func swiftFunc0(a0: Int, a1: UInt16, a2: Double, a3: UInt32, a4: Int, a5: Double, a6: UInt32, a7: Int32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public class Type36Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    return hasher.finalize()
                }
                public enum Type36Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int64) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        return hasher.finalize()
                    }
                    public struct Type36Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: UInt8, a1: Double, a2: Int16, a3: Int64) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public struct Type37 {
    public static func swiftFunc0(a0: UInt64, a1: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public enum Type37Sub2 {
        public static func swiftFunc0(a0: UInt16, a1: Int, a2: Int8, a3: Int8, a4: Int64, a5: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public enum Type37Sub2Sub3 {
            public static func swiftFunc0(a0: UInt16, a1: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
            public enum Type37Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    return hasher.finalize()
                }
                public enum Type37Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int16, a1: Int32, a2: UInt8, a3: Int8, a4: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        return hasher.finalize()
                    }
                    public class Type37Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int16, a1: Double, a2: Double, a3: Int16, a4: Int, a5: Int8, a6: Int16, a7: Int32, a8: Double, a9: Double) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            hasher.combine(a7);
                            hasher.combine(a8);
                            hasher.combine(a9);
                            return hasher.finalize()
                        }
                        public struct Type37Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Int16) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                return hasher.finalize()
                            }
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type38 {
    public static func swiftFunc0(a0: UInt64, a1: Int8, a2: UInt16, a3: Int32, a4: Int64, a5: Int64, a6: Int32, a7: Double, a8: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public enum Type38Sub2 {
        public static func swiftFunc0(a0: UInt16, a1: Double, a2: UInt32, a3: Int, a4: Double, a5: UInt) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public class Type38Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: Double, a2: UInt32, a3: Double, a4: UInt8, a5: Int) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
            public enum Type38Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int8, a1: UInt32, a2: Int32, a3: UInt64, a4: UInt64, a5: Int, a6: UInt32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    return hasher.finalize()
                }
                public struct Type38Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt, a1: UInt64, a2: Int64) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public enum Type39 {
    public static func swiftFunc0(a0: UInt16, a1: UInt, a2: Int, a3: Int64, a4: Int8, a5: UInt32, a6: Int16, a7: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public enum Type39Sub2 {
        public static func swiftFunc0(a0: Double, a1: UInt32, a2: Int16, a3: UInt, a4: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public struct Type39Sub2Sub3 {
            public static func swiftFunc0(a0: Int8, a1: UInt64, a2: UInt64, a3: Int64, a4: Int8, a5: Int64, a6: Double, a7: Double, a8: Int16, a9: Int) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                hasher.combine(a9);
                return hasher.finalize()
            }
        }
        
    }
    
}

public enum Type40 {
    public static func swiftFunc0(a0: Int, a1: UInt32, a2: Double, a3: UInt, a4: UInt8, a5: UInt32, a6: UInt8, a7: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public struct Type40Sub2 {
        public static func swiftFunc0(a0: UInt16, a1: Int16, a2: Int32, a3: UInt32, a4: UInt32, a5: UInt8, a6: Int, a7: Int32, a8: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public struct Type40Sub2Sub3 {
            public static func swiftFunc0(a0: UInt8, a1: UInt16, a2: UInt16, a3: UInt16, a4: UInt16, a5: Double, a6: Int16, a7: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public class Type40Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: UInt64, a2: Int8, a3: Int) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public class Type41 {
    public static func swiftFunc0(a0: Int, a1: Int32, a2: Int64, a3: UInt, a4: Int32, a5: Double, a6: UInt16, a7: UInt8, a8: Int64, a9: Int32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        hasher.combine(a9);
        return hasher.finalize()
    }
}

public enum Type42 {
    public static func swiftFunc0(a0: Int8, a1: Int64, a2: Double, a3: UInt64, a4: Int8, a5: Int32, a6: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public struct Type42Sub2 {
        public static func swiftFunc0(a0: UInt, a1: UInt16, a2: Int16, a3: UInt64, a4: UInt64, a5: UInt32, a6: Int32, a7: Double, a8: Int32, a9: Int64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            hasher.combine(a9);
            return hasher.finalize()
        }
        public class Type42Sub2Sub3 {
            public static func swiftFunc0(a0: UInt, a1: UInt64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
            public class Type42Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt, a1: UInt8, a2: Int8, a3: Int32, a4: UInt8, a5: UInt32, a6: Int8, a7: Int32, a8: Int) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public enum Type43 {
    public static func swiftFunc0(a0: UInt64, a1: UInt8, a2: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        return hasher.finalize()
    }
    public struct Type43Sub2 {
        public static func swiftFunc0(a0: Int64, a1: UInt, a2: UInt16, a3: UInt8, a4: UInt, a5: Int32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public class Type43Sub2Sub3 {
            public static func swiftFunc0(a0: UInt, a1: Int64, a2: Double, a3: UInt8, a4: Int64, a5: Int8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
        }
        
    }
    
}

public enum Type44 {
    public static func swiftFunc0(a0: UInt64, a1: UInt32, a2: UInt, a3: UInt32, a4: Int8, a5: Int16, a6: UInt16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public struct Type44Sub2 {
        public static func swiftFunc0(a0: Int32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public enum Type44Sub2Sub3 {
            public static func swiftFunc0(a0: UInt32, a1: Int32, a2: UInt32, a3: Int32, a4: UInt32, a5: UInt, a6: Double, a7: Int16, a8: UInt16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public class Type44Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: UInt8, a2: UInt, a3: UInt, a4: Double, a5: Int8, a6: UInt, a7: UInt64, a8: Double, a9: Int8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    hasher.combine(a9);
                    return hasher.finalize()
                }
                public class Type44Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        return hasher.finalize()
                    }
                    public class Type44Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int64, a1: UInt, a2: Int32, a3: Int8, a4: UInt32, a5: Int16, a6: UInt64, a7: Double, a8: Int8) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            hasher.combine(a7);
                            hasher.combine(a8);
                            return hasher.finalize()
                        }
                        public class Type44Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: UInt16, a1: UInt8, a2: Int32, a3: UInt64, a4: Int8, a5: UInt8, a6: UInt32, a7: Double, a8: UInt) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                hasher.combine(a5);
                                hasher.combine(a6);
                                hasher.combine(a7);
                                hasher.combine(a8);
                                return hasher.finalize()
                            }
                            public enum Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: UInt16, a1: UInt, a2: UInt8, a3: UInt32, a4: UInt64, a5: Int16, a6: Double, a7: Int64, a8: UInt) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    hasher.combine(a4);
                                    hasher.combine(a5);
                                    hasher.combine(a6);
                                    hasher.combine(a7);
                                    hasher.combine(a8);
                                    return hasher.finalize()
                                }
                                public struct Type44Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: Double, a1: UInt8, a2: Int8, a3: Int, a4: UInt16, a5: UInt, a6: UInt8) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        hasher.combine(a2);
                                        hasher.combine(a3);
                                        hasher.combine(a4);
                                        hasher.combine(a5);
                                        hasher.combine(a6);
                                        return hasher.finalize()
                                    }
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type45 {
    public static func swiftFunc0(a0: Int, a1: Int64, a2: Double, a3: UInt16, a4: UInt16, a5: UInt, a6: Int16, a7: Double, a8: Double, a9: Int32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        hasher.combine(a9);
        return hasher.finalize()
    }
    public class Type45Sub2 {
        public static func swiftFunc0(a0: UInt16, a1: Double, a2: Int32, a3: UInt32, a4: Int16, a5: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public class Type45Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: Int, a2: Double, a3: UInt32, a4: UInt, a5: Double, a6: UInt) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                return hasher.finalize()
            }
        }
        
    }
    
}

public struct Type46 {
    public static func swiftFunc0(a0: Int, a1: Int16, a2: Int8, a3: UInt32, a4: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public class Type46Sub2 {
        public static func swiftFunc0(a0: UInt8, a1: Int64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            return hasher.finalize()
        }
        public struct Type46Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: UInt8, a2: UInt8, a3: Int8, a4: UInt8, a5: UInt16, a6: UInt32, a7: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public struct Type46Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int64, a1: Double, a2: UInt32, a3: UInt64, a4: Int, a5: Double) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public class Type47 {
    public static func swiftFunc0(a0: Double, a1: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public struct Type47Sub2 {
        public static func swiftFunc0(a0: UInt32, a1: Int32, a2: UInt8, a3: Int8, a4: UInt64, a5: Int64, a6: Double, a7: Double, a8: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public enum Type47Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: UInt32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
        }
        
    }
    
}

public struct Type48 {
    public static func swiftFunc0(a0: UInt64, a1: Int, a2: UInt64, a3: Int8, a4: Int8, a5: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public class Type48Sub2 {
        public static func swiftFunc0(a0: Int, a1: Int, a2: UInt16, a3: UInt8, a4: Int8, a5: UInt8, a6: Int32, a7: Int64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            return hasher.finalize()
        }
        public enum Type48Sub2Sub3 {
            public static func swiftFunc0(a0: Int16, a1: Double, a2: Int16, a3: UInt64, a4: Double, a5: Int16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
            public class Type48Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int32, a1: Int16, a2: Int64, a3: Double, a4: UInt32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    return hasher.finalize()
                }
                public struct Type48Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt, a1: UInt32, a2: UInt32, a3: Int64, a4: Int16, a5: UInt16, a6: UInt64, a7: UInt16, a8: UInt32, a9: UInt32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        hasher.combine(a9);
                        return hasher.finalize()
                    }
                    public enum Type48Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: UInt64, a1: Double) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public struct Type49 {
    public static func swiftFunc0(a0: Int64, a1: Int64, a2: UInt8, a3: Double, a4: UInt, a5: Int8, a6: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public enum Type49Sub2 {
        public static func swiftFunc0(a0: UInt64, a1: Int64, a2: Int16, a3: UInt, a4: Int64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public class Type49Sub2Sub3 {
            public static func swiftFunc0(a0: UInt, a1: Int32, a2: Int32, a3: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                return hasher.finalize()
            }
            public class Type49Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: Int, a2: UInt64, a3: Int, a4: Int16, a5: UInt32, a6: Double) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    return hasher.finalize()
                }
                public enum Type49Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int, a1: Double, a2: UInt16, a3: Double, a4: Double, a5: Int16, a6: Int8, a7: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        return hasher.finalize()
                    }
                    public enum Type49Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int32, a1: UInt16, a2: UInt16, a3: UInt32, a4: UInt32, a5: UInt16) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            return hasher.finalize()
                        }
                        public struct Type49Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: UInt16, a1: UInt32, a2: Int16) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                return hasher.finalize()
                            }
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type50 {
    public static func swiftFunc0(a0: UInt32, a1: UInt16, a2: Int32, a3: Int, a4: Double, a5: Int32, a6: UInt16, a7: UInt16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public struct Type50Sub2 {
        public static func swiftFunc0(a0: UInt, a1: Int, a2: Int64, a3: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
    }
    
}

public class Type51 {
    public static func swiftFunc0(a0: Int32, a1: UInt16, a2: UInt64, a3: UInt8, a4: UInt, a5: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public enum Type51Sub2 {
        public static func swiftFunc0(a0: UInt8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public class Type51Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: UInt, a2: Int32, a3: Int32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                return hasher.finalize()
            }
            public struct Type51Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int, a1: Int16, a2: Int64, a3: Int8, a4: UInt64, a5: UInt8, a6: Int64, a7: Int) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    return hasher.finalize()
                }
                public class Type51Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt64, a1: UInt16, a2: Int16, a3: Int8, a4: Int32, a5: UInt32, a6: Int8, a7: Int64, a8: UInt16) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public enum Type52 {
    public static func swiftFunc0(a0: Int16, a1: Int8, a2: Double, a3: UInt64, a4: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public class Type52Sub2 {
        public static func swiftFunc0(a0: UInt8, a1: Double, a2: UInt16, a3: Int32, a4: UInt, a5: Int, a6: Double, a7: UInt, a8: UInt64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public enum Type52Sub2Sub3 {
            public static func swiftFunc0(a0: Int32, a1: Int64, a2: Int, a3: Int64, a4: Int, a5: UInt, a6: Int32, a7: Int64, a8: UInt) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public enum Type52Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt64, a1: Int16, a2: Double, a3: Int, a4: Double) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public enum Type53 {
    public static func swiftFunc0(a0: Int) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public enum Type53Sub2 {
        public static func swiftFunc0(a0: Int16, a1: Double, a2: Int, a3: UInt, a4: Int64, a5: UInt64, a6: Int16, a7: UInt, a8: UInt, a9: UInt64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            hasher.combine(a9);
            return hasher.finalize()
        }
    }
    
}

public class Type54 {
    public static func swiftFunc0(a0: Int) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public class Type54Sub2 {
        public static func swiftFunc0(a0: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public enum Type54Sub2Sub3 {
            public static func swiftFunc0(a0: UInt32, a1: Double, a2: UInt16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                return hasher.finalize()
            }
            public enum Type54Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt64, a1: UInt, a2: Int8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public enum Type55 {
    public static func swiftFunc0(a0: Int64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public struct Type55Sub2 {
        public static func swiftFunc0(a0: UInt32, a1: UInt64, a2: UInt16, a3: UInt32, a4: Int, a5: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public enum Type55Sub2Sub3 {
            public static func swiftFunc0(a0: Int32, a1: Int16, a2: UInt32, a3: Int, a4: Double, a5: Int16, a6: UInt64, a7: Int16, a8: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public enum Type55Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt32, a1: Double, a2: UInt32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
                public struct Type55Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int16, a1: UInt8, a2: Int, a3: UInt, a4: UInt32, a5: Double, a6: Int16, a7: UInt, a8: Int32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        return hasher.finalize()
                    }
                    public struct Type55Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int16, a1: Int) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            return hasher.finalize()
                        }
                        public enum Type55Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: UInt16, a1: UInt16, a2: Int16, a3: Int, a4: UInt64) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                return hasher.finalize()
                            }
                            public struct Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Int64, a1: Int, a2: Double, a3: UInt8, a4: Int16, a5: UInt64, a6: Int32, a7: Int64, a8: UInt32, a9: Int64) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    hasher.combine(a4);
                                    hasher.combine(a5);
                                    hasher.combine(a6);
                                    hasher.combine(a7);
                                    hasher.combine(a8);
                                    hasher.combine(a9);
                                    return hasher.finalize()
                                }
                                public struct Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: UInt32, a1: Int32) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        return hasher.finalize()
                                    }
                                    public struct Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10 {
                                        public static func swiftFunc0(a0: Double, a1: Double, a2: UInt64, a3: Int, a4: UInt64, a5: UInt, a6: Int, a7: Double, a8: UInt, a9: UInt64) -> Int {
                                            var hasher = HasherFNV1a()
                                            hasher.combine(a0);
                                            hasher.combine(a1);
                                            hasher.combine(a2);
                                            hasher.combine(a3);
                                            hasher.combine(a4);
                                            hasher.combine(a5);
                                            hasher.combine(a6);
                                            hasher.combine(a7);
                                            hasher.combine(a8);
                                            hasher.combine(a9);
                                            return hasher.finalize()
                                        }
                                        public enum Type55Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11 {
                                            public static func swiftFunc0(a0: Int64) -> Int {
                                                var hasher = HasherFNV1a()
                                                hasher.combine(a0);
                                                return hasher.finalize()
                                            }
                                        }
                                        
                                    }
                                    
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type56 {
    public static func swiftFunc0(a0: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public enum Type56Sub2 {
        public static func swiftFunc0(a0: UInt16, a1: UInt32, a2: UInt) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public struct Type56Sub2Sub3 {
            public static func swiftFunc0(a0: Int32, a1: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
        }
        
    }
    
}

public struct Type57 {
    public static func swiftFunc0(a0: Int16, a1: Int64, a2: Int8, a3: UInt, a4: Int8, a5: Int64, a6: Double, a7: UInt, a8: Int64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public struct Type57Sub2 {
        public static func swiftFunc0(a0: Int16, a1: UInt16, a2: Int16, a3: Double, a4: UInt16, a5: Int8, a6: UInt, a7: Int64, a8: UInt16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public enum Type57Sub2Sub3 {
            public static func swiftFunc0(a0: UInt32, a1: Double, a2: UInt32, a3: Int, a4: UInt64, a5: Int16, a6: UInt16, a7: Double, a8: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public struct Type57Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int8, a1: Double) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    return hasher.finalize()
                }
                public class Type57Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt8, a1: Double, a2: UInt64, a3: UInt, a4: UInt32, a5: Double, a6: UInt32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        return hasher.finalize()
                    }
                    public class Type57Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int32) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type58 {
    public static func swiftFunc0(a0: Int16, a1: UInt8, a2: UInt64, a3: Double, a4: Int16, a5: UInt64, a6: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public class Type58Sub2 {
        public static func swiftFunc0(a0: UInt32, a1: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            return hasher.finalize()
        }
    }
    
}

public class Type59 {
    public static func swiftFunc0(a0: Int, a1: UInt, a2: UInt16, a3: UInt32, a4: UInt16, a5: Double, a6: Double, a7: Int8, a8: Double, a9: Int32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        hasher.combine(a9);
        return hasher.finalize()
    }
    public enum Type59Sub2 {
        public static func swiftFunc0(a0: UInt64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public enum Type59Sub2Sub3 {
            public static func swiftFunc0(a0: Int32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                return hasher.finalize()
            }
            public struct Type59Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int32, a1: UInt32, a2: UInt64, a3: Int, a4: Int16, a5: Int64, a6: Int32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    return hasher.finalize()
                }
                public struct Type59Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt8, a1: Int32, a2: UInt8, a3: Double, a4: Int16, a5: Int64) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                    public struct Type59Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: UInt16) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            return hasher.finalize()
                        }
                        public enum Type59Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Double, a1: UInt32, a2: UInt, a3: Int8, a4: Int64) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                return hasher.finalize()
                            }
                            public enum Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: UInt64, a1: Double, a2: UInt32) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    return hasher.finalize()
                                }
                                public enum Type59Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: Int64, a1: UInt16, a2: UInt64, a3: Double, a4: Double, a5: UInt16) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        hasher.combine(a2);
                                        hasher.combine(a3);
                                        hasher.combine(a4);
                                        hasher.combine(a5);
                                        return hasher.finalize()
                                    }
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type60 {
    public static func swiftFunc0(a0: Int64, a1: Double, a2: Int32, a3: Int8, a4: Int16, a5: Int16, a6: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public struct Type60Sub2 {
        public static func swiftFunc0(a0: Int, a1: UInt, a2: Double, a3: Int64, a4: UInt32, a5: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public struct Type60Sub2Sub3 {
            public static func swiftFunc0(a0: Int16, a1: Int32, a2: UInt, a3: Int8, a4: Int8, a5: UInt, a6: Int64, a7: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public class Type60Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt8, a1: Double, a2: Int8, a3: Int8, a4: Int64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    return hasher.finalize()
                }
                public struct Type60Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double, a1: Int32, a2: Int) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        return hasher.finalize()
                    }
                    public class Type60Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int16) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            return hasher.finalize()
                        }
                        public class Type60Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Int32, a1: UInt8) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                return hasher.finalize()
                            }
                            public class Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Double, a1: Int8, a2: UInt64, a3: UInt8, a4: Int32, a5: Int8, a6: Int64) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    hasher.combine(a4);
                                    hasher.combine(a5);
                                    hasher.combine(a6);
                                    return hasher.finalize()
                                }
                                public struct Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: UInt64, a1: Int64) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        return hasher.finalize()
                                    }
                                    public enum Type60Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10 {
                                        public static func swiftFunc0(a0: UInt32, a1: UInt8, a2: UInt8, a3: UInt, a4: Int, a5: UInt32) -> Int {
                                            var hasher = HasherFNV1a()
                                            hasher.combine(a0);
                                            hasher.combine(a1);
                                            hasher.combine(a2);
                                            hasher.combine(a3);
                                            hasher.combine(a4);
                                            hasher.combine(a5);
                                            return hasher.finalize()
                                        }
                                    }
                                    
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type61 {
    public static func swiftFunc0(a0: Int8, a1: Int64, a2: UInt, a3: Int64, a4: UInt, a5: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public enum Type61Sub2 {
        public static func swiftFunc0(a0: Int16, a1: UInt16, a2: Int64, a3: Int16, a4: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
    }
    
}

public class Type62 {
    public static func swiftFunc0(a0: Int, a1: UInt32, a2: Int16, a3: Int32, a4: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public struct Type62Sub2 {
        public static func swiftFunc0(a0: Double, a1: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            return hasher.finalize()
        }
        public enum Type62Sub2Sub3 {
            public static func swiftFunc0(a0: UInt8, a1: Int8, a2: Int8, a3: Int8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                return hasher.finalize()
            }
            public class Type62Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt, a1: Int8, a2: UInt32, a3: UInt16, a4: Int8, a5: UInt32, a6: UInt, a7: Double, a8: UInt64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
                public struct Type62Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt32, a1: Int16, a2: UInt16, a3: UInt8, a4: UInt64, a5: Int32, a6: Int8, a7: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        return hasher.finalize()
                    }
                    public enum Type62Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int32, a1: Int64, a2: Int16) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            return hasher.finalize()
                        }
                        public class Type62Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: UInt8, a1: UInt32, a2: UInt32, a3: Int16, a4: Int32, a5: UInt32, a6: UInt64) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                hasher.combine(a5);
                                hasher.combine(a6);
                                return hasher.finalize()
                            }
                            public enum Type62Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Int, a1: Int, a2: Int16, a3: Double) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    return hasher.finalize()
                                }
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public struct Type63 {
    public static func swiftFunc0(a0: UInt32, a1: Int8, a2: Int32, a3: UInt16, a4: Int8, a5: UInt16, a6: Int32, a7: UInt64, a8: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public enum Type63Sub2 {
        public static func swiftFunc0(a0: Int64, a1: Int64, a2: UInt64, a3: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
        public enum Type63Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: UInt32, a2: Int16, a3: UInt64, a4: UInt16, a5: UInt64, a6: UInt64, a7: Int16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public class Type63Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int8, a1: Int8, a2: UInt64, a3: UInt64, a4: UInt8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    return hasher.finalize()
                }
                public enum Type63Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int64, a1: Double, a2: Int16, a3: UInt64, a4: UInt64, a5: UInt) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                    public struct Type63Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int32, a1: UInt8) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type64 {
    public static func swiftFunc0(a0: UInt8, a1: Int16, a2: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        return hasher.finalize()
    }
    public struct Type64Sub2 {
        public static func swiftFunc0(a0: UInt8, a1: Double, a2: Double, a3: Int16, a4: UInt64, a5: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public class Type64Sub2Sub3 {
            public static func swiftFunc0(a0: Int) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                return hasher.finalize()
            }
            public class Type64Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt, a1: Int8, a2: Int64, a3: UInt8, a4: UInt64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    return hasher.finalize()
                }
                public enum Type64Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double, a1: UInt16, a2: Int16, a3: UInt16, a4: Int32, a5: UInt16) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                    public struct Type64Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int32, a1: Double, a2: UInt) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            return hasher.finalize()
                        }
                        public enum Type64Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: UInt16, a1: UInt64, a2: UInt32, a3: UInt16) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                return hasher.finalize()
                            }
                            public enum Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: UInt64) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    return hasher.finalize()
                                }
                                public struct Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: Int64, a1: Int16, a2: UInt32, a3: Int16, a4: Int16, a5: Int8, a6: Int, a7: Int32, a8: Int16) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        hasher.combine(a2);
                                        hasher.combine(a3);
                                        hasher.combine(a4);
                                        hasher.combine(a5);
                                        hasher.combine(a6);
                                        hasher.combine(a7);
                                        hasher.combine(a8);
                                        return hasher.finalize()
                                    }
                                    public enum Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10 {
                                        public static func swiftFunc0(a0: Int32, a1: UInt, a2: Double, a3: UInt64, a4: Int32, a5: Int64) -> Int {
                                            var hasher = HasherFNV1a()
                                            hasher.combine(a0);
                                            hasher.combine(a1);
                                            hasher.combine(a2);
                                            hasher.combine(a3);
                                            hasher.combine(a4);
                                            hasher.combine(a5);
                                            return hasher.finalize()
                                        }
                                        public class Type64Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10Sub11 {
                                            public static func swiftFunc0(a0: UInt8, a1: UInt64, a2: Double, a3: Int32, a4: Double, a5: Int16) -> Int {
                                                var hasher = HasherFNV1a()
                                                hasher.combine(a0);
                                                hasher.combine(a1);
                                                hasher.combine(a2);
                                                hasher.combine(a3);
                                                hasher.combine(a4);
                                                hasher.combine(a5);
                                                return hasher.finalize()
                                            }
                                        }
                                        
                                    }
                                    
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type65 {
    public static func swiftFunc0(a0: Double, a1: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public class Type65Sub2 {
        public static func swiftFunc0(a0: UInt, a1: UInt32, a2: UInt64, a3: UInt8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
        public enum Type65Sub2Sub3 {
            public static func swiftFunc0(a0: UInt64, a1: Int8, a2: Int8, a3: Double, a4: UInt32, a5: UInt32, a6: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                return hasher.finalize()
            }
            public struct Type65Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt16, a1: UInt32, a2: UInt64, a3: Double, a4: Int16, a5: Int64, a6: UInt16, a7: Int64, a8: Int8, a9: Int8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    hasher.combine(a9);
                    return hasher.finalize()
                }
                public struct Type65Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double, a1: UInt64) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public struct Type66 {
    public static func swiftFunc0(a0: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public class Type66Sub2 {
        public static func swiftFunc0(a0: UInt64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
    }
    
}

public struct Type67 {
    public static func swiftFunc0(a0: Int64, a1: Int32, a2: UInt, a3: Int64, a4: Int) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public struct Type67Sub2 {
        public static func swiftFunc0(a0: UInt16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public class Type67Sub2Sub3 {
            public static func swiftFunc0(a0: Int, a1: Int8, a2: Int16, a3: UInt, a4: UInt16, a5: Int32, a6: Int16, a7: Int32, a8: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public struct Type67Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int, a1: UInt64, a2: UInt32, a3: Double, a4: Int8, a5: Int64, a6: UInt32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    return hasher.finalize()
                }
                public struct Type67Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int16, a1: Int64, a2: UInt64, a3: Double, a4: Int8) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        return hasher.finalize()
                    }
                    public struct Type67Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: UInt, a1: UInt64, a2: UInt8, a3: Int8, a4: Double, a5: Double, a6: UInt, a7: Double, a8: Int8) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            hasher.combine(a7);
                            hasher.combine(a8);
                            return hasher.finalize()
                        }
                        public struct Type67Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Double, a1: Int64, a2: Double, a3: Int64, a4: UInt32, a5: UInt8, a6: Int16) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                hasher.combine(a5);
                                hasher.combine(a6);
                                return hasher.finalize()
                            }
                            public struct Type67Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Int16) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    return hasher.finalize()
                                }
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public struct Type68 {
    public static func swiftFunc0(a0: Int64, a1: Int, a2: Int32, a3: UInt32, a4: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public class Type68Sub2 {
        public static func swiftFunc0(a0: Int64, a1: UInt8, a2: UInt16, a3: UInt64, a4: UInt8, a5: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public struct Type68Sub2Sub3 {
            public static func swiftFunc0(a0: UInt32, a1: UInt32, a2: UInt64, a3: Int8, a4: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                return hasher.finalize()
            }
            public enum Type68Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt32, a1: UInt64, a2: Int64, a3: Int64, a4: UInt64, a5: Int, a6: UInt32, a7: Int64, a8: Double, a9: UInt64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    hasher.combine(a9);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public class Type69 {
    public static func swiftFunc0(a0: UInt64, a1: Int64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
}

public struct Type70 {
    public static func swiftFunc0(a0: Double, a1: UInt64, a2: Int64, a3: UInt, a4: UInt64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public class Type70Sub2 {
        public static func swiftFunc0(a0: Int16, a1: UInt8, a2: Double, a3: UInt16, a4: Int8, a5: UInt16, a6: Int64, a7: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            return hasher.finalize()
        }
        public enum Type70Sub2Sub3 {
            public static func swiftFunc0(a0: Int8, a1: Double, a2: Int16, a3: Int32, a4: UInt32, a5: Int32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
            public class Type70Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int8, a1: Double, a2: Int64, a3: UInt32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public class Type71 {
    public static func swiftFunc0(a0: Int64, a1: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public class Type71Sub2 {
        public static func swiftFunc0(a0: Int32, a1: Double, a2: UInt8, a3: UInt32, a4: Int8, a5: Int8, a6: UInt64, a7: Int8, a8: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public class Type71Sub2Sub3 {
            public static func swiftFunc0(a0: UInt8, a1: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
            public class Type71Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt8, a1: Int) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    return hasher.finalize()
                }
                public struct Type71Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt64, a1: Double, a2: Int16, a3: Double, a4: Int, a5: Int32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public enum Type72 {
    public static func swiftFunc0(a0: UInt8, a1: UInt32, a2: UInt16, a3: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        return hasher.finalize()
    }
    public struct Type72Sub2 {
        public static func swiftFunc0(a0: Int16, a1: Int16, a2: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public class Type72Sub2Sub3 {
            public static func swiftFunc0(a0: UInt16, a1: Int16, a2: UInt8, a3: Int8, a4: UInt64, a5: Int8, a6: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                return hasher.finalize()
            }
            public enum Type72Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int32, a1: Int32, a2: Int16) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
                public class Type72Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt32, a1: UInt32, a2: Int, a3: UInt8, a4: Int64, a5: UInt64, a6: Int, a7: UInt32, a8: UInt32, a9: Int32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        hasher.combine(a9);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public struct Type73 {
    public static func swiftFunc0(a0: Int64, a1: Int16, a2: UInt16, a3: Int16, a4: UInt32, a5: Double, a6: UInt8, a7: Int8, a8: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public struct Type73Sub2 {
        public static func swiftFunc0(a0: Double, a1: UInt64, a2: UInt) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public class Type73Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: Double, a2: Int) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                return hasher.finalize()
            }
        }
        
    }
    
}

public class Type74 {
    public static func swiftFunc0(a0: Int8, a1: UInt32, a2: Int8, a3: Int64, a4: UInt, a5: UInt, a6: Double, a7: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public enum Type74Sub2 {
        public static func swiftFunc0(a0: UInt32, a1: UInt8, a2: UInt32, a3: Double, a4: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
        public struct Type74Sub2Sub3 {
            public static func swiftFunc0(a0: UInt, a1: Int, a2: UInt16, a3: Double, a4: UInt, a5: UInt64, a6: Int8, a7: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public class Type74Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public struct Type75 {
    public static func swiftFunc0(a0: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public class Type75Sub2 {
        public static func swiftFunc0(a0: UInt16, a1: Int, a2: UInt, a3: UInt64, a4: UInt, a5: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public class Type75Sub2Sub3 {
            public static func swiftFunc0(a0: Int64, a1: Int64, a2: Int, a3: Int32, a4: Int16, a5: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
            public enum Type75Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int16, a1: Int16, a2: Int16) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
                public struct Type75Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double, a1: Int, a2: Double, a3: UInt16, a4: Int8, a5: Int16, a6: Int32, a7: Int32, a8: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public struct Type76 {
    public static func swiftFunc0(a0: UInt32, a1: UInt, a2: Int, a3: UInt8, a4: UInt16, a5: Double, a6: Double, a7: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public class Type76Sub2 {
        public static func swiftFunc0(a0: Int64, a1: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            return hasher.finalize()
        }
        public enum Type76Sub2Sub3 {
            public static func swiftFunc0(a0: Double, a1: Int32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
            public enum Type76Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: Int64, a2: UInt16, a3: UInt16, a4: Double, a5: UInt32, a6: UInt8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    return hasher.finalize()
                }
                public enum Type76Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int16, a1: UInt16, a2: UInt64, a3: Int32, a4: Double, a5: UInt8, a6: Double, a7: Int8, a8: Int, a9: Int64) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        hasher.combine(a9);
                        return hasher.finalize()
                    }
                    public class Type76Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int, a1: UInt16) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public struct Type77 {
    public static func swiftFunc0(a0: Int, a1: Int32, a2: Int16, a3: Double, a4: UInt64, a5: Int64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
}

public class Type78 {
    public static func swiftFunc0(a0: UInt32, a1: UInt32, a2: UInt8, a3: UInt32, a4: Int, a5: UInt64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public class Type78Sub2 {
        public static func swiftFunc0(a0: Double, a1: UInt32, a2: UInt32, a3: UInt8, a4: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            return hasher.finalize()
        }
    }
    
}

public class Type79 {
    public static func swiftFunc0(a0: Int32, a1: Double, a2: Double, a3: Int16, a4: Int64, a5: UInt64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public struct Type79Sub2 {
        public static func swiftFunc0(a0: UInt8, a1: Int8, a2: UInt16, a3: Int64, a4: Double, a5: Int32, a6: Double, a7: Int64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            return hasher.finalize()
        }
        public class Type79Sub2Sub3 {
            public static func swiftFunc0(a0: Double, a1: Int64, a2: UInt8, a3: Double, a4: UInt64, a5: UInt16, a6: UInt16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                return hasher.finalize()
            }
            public struct Type79Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt64, a1: Int8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public enum Type80 {
    public static func swiftFunc0(a0: Int32, a1: Double, a2: UInt8, a3: UInt16, a4: Int64, a5: UInt8, a6: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public struct Type80Sub2 {
        public static func swiftFunc0(a0: Int64, a1: Double, a2: UInt64, a3: UInt, a4: UInt8, a5: Int, a6: Double, a7: UInt32, a8: UInt64, a9: UInt64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            hasher.combine(a9);
            return hasher.finalize()
        }
        public struct Type80Sub2Sub3 {
            public static func swiftFunc0(a0: UInt64, a1: UInt, a2: Int16, a3: Int32, a4: UInt) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                return hasher.finalize()
            }
            public struct Type80Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int, a1: UInt16, a2: UInt8, a3: Int64, a4: UInt32, a5: Double, a6: UInt64, a7: Int32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    return hasher.finalize()
                }
                public struct Type80Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Double, a1: Int32, a2: Int64, a3: UInt32, a4: Double, a5: UInt32, a6: UInt32, a7: UInt16, a8: Int8, a9: UInt64) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        hasher.combine(a9);
                        return hasher.finalize()
                    }
                    public struct Type80Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Double, a1: Double, a2: Double, a3: Int32, a4: UInt, a5: UInt8, a6: Int64) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public struct Type81 {
    public static func swiftFunc0(a0: Int, a1: Int8, a2: Int16, a3: Int64, a4: Int8, a5: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        return hasher.finalize()
    }
    public enum Type81Sub2 {
        public static func swiftFunc0(a0: Double, a1: Int64, a2: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public class Type81Sub2Sub3 {
            public static func swiftFunc0(a0: Int16, a1: Int32, a2: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                return hasher.finalize()
            }
            public struct Type81Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: UInt16, a2: Int64, a3: UInt64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public class Type82 {
    public static func swiftFunc0(a0: Int32, a1: Int, a2: Int32, a3: Int16, a4: UInt64, a5: Int16, a6: Int8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
}

public struct Type83 {
    public static func swiftFunc0(a0: UInt32, a1: Int32, a2: Int64, a3: UInt64, a4: Double, a5: Double, a6: Double, a7: UInt32, a8: Int64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public enum Type83Sub2 {
        public static func swiftFunc0(a0: Double, a1: UInt64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            return hasher.finalize()
        }
        public class Type83Sub2Sub3 {
            public static func swiftFunc0(a0: Double, a1: Double, a2: Int32, a3: UInt16, a4: UInt64, a5: Double, a6: Int64, a7: UInt32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                return hasher.finalize()
            }
            public struct Type83Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int32, a1: Double, a2: Int, a3: UInt64, a4: Int32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public enum Type84 {
    public static func swiftFunc0(a0: UInt64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
}

public class Type85 {
    public static func swiftFunc0(a0: UInt8, a1: Int16, a2: Int, a3: UInt64, a4: Int16, a5: Double, a6: Int, a7: Int, a8: Int32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public struct Type85Sub2 {
        public static func swiftFunc0(a0: UInt64, a1: UInt32, a2: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            return hasher.finalize()
        }
        public struct Type85Sub2Sub3 {
            public static func swiftFunc0(a0: UInt16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                return hasher.finalize()
            }
            public enum Type85Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt16, a1: UInt, a2: Double, a3: UInt, a4: UInt64, a5: Int8, a6: Int8, a7: UInt16, a8: Double, a9: Int64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    hasher.combine(a9);
                    return hasher.finalize()
                }
            }
            
        }
        
    }
    
}

public struct Type86 {
    public static func swiftFunc0(a0: UInt64, a1: Int, a2: Int16, a3: UInt, a4: UInt64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
}

public enum Type87 {
    public static func swiftFunc0(a0: UInt16, a1: Double, a2: Double, a3: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        return hasher.finalize()
    }
    public enum Type87Sub2 {
        public static func swiftFunc0(a0: Int16, a1: Double, a2: Double, a3: Int64, a4: UInt64, a5: UInt, a6: Int, a7: UInt16, a8: UInt, a9: Double) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            hasher.combine(a9);
            return hasher.finalize()
        }
        public struct Type87Sub2Sub3 {
            public static func swiftFunc0(a0: UInt64, a1: Int32, a2: UInt8, a3: Int16, a4: UInt, a5: UInt32, a6: Int32, a7: Int64, a8: Double, a9: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                hasher.combine(a9);
                return hasher.finalize()
            }
            public struct Type87Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int16, a1: UInt16) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    return hasher.finalize()
                }
                public class Type87Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt16, a1: Int32, a2: Double, a3: Int16, a4: Int16, a5: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                    public struct Type87Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int64, a1: Int16, a2: UInt32, a3: UInt8, a4: UInt32, a5: Int16, a6: UInt16) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            return hasher.finalize()
                        }
                        public class Type87Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Int8, a1: UInt, a2: UInt, a3: UInt64, a4: UInt16, a5: Double, a6: UInt64, a7: Int32, a8: UInt16) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                hasher.combine(a5);
                                hasher.combine(a6);
                                hasher.combine(a7);
                                hasher.combine(a8);
                                return hasher.finalize()
                            }
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type88 {
    public static func swiftFunc0(a0: Int8, a1: UInt8, a2: Int8, a3: UInt, a4: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public struct Type88Sub2 {
        public static func swiftFunc0(a0: Int, a1: Int8, a2: Double, a3: Int, a4: Int, a5: Int, a6: UInt8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            return hasher.finalize()
        }
        public class Type88Sub2Sub3 {
            public static func swiftFunc0(a0: UInt16, a1: Double, a2: Int32, a3: UInt8, a4: Int, a5: UInt64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
            public enum Type88Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int, a1: UInt, a2: Int64, a3: Int32, a4: UInt, a5: UInt16, a6: UInt8, a7: Double, a8: UInt32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
                public struct Type88Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int64, a1: Double, a2: UInt32, a3: UInt32, a4: Double, a5: Int8) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public class Type89 {
    public static func swiftFunc0(a0: Double, a1: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        return hasher.finalize()
    }
    public enum Type89Sub2 {
        public static func swiftFunc0(a0: Int8, a1: UInt, a2: Int64, a3: Int8, a4: Double, a5: Int8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            return hasher.finalize()
        }
        public class Type89Sub2Sub3 {
            public static func swiftFunc0(a0: Int64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                return hasher.finalize()
            }
        }
        
    }
    
}

public class Type90 {
    public static func swiftFunc0(a0: Int32, a1: Int64, a2: UInt64, a3: UInt64, a4: Int8, a5: Int32, a6: UInt8) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public class Type90Sub2 {
        public static func swiftFunc0(a0: Int8, a1: UInt64, a2: UInt, a3: UInt16, a4: Int16, a5: Double, a6: UInt32, a7: UInt32, a8: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public class Type90Sub2Sub3 {
            public static func swiftFunc0(a0: UInt16, a1: Int, a2: Double, a3: UInt32, a4: Int, a5: Int64, a6: Double, a7: Double, a8: UInt64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public class Type90Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: Int64, a2: Int16) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
                public enum Type90Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int8, a1: UInt8, a2: Int64, a3: Double, a4: Int32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public class Type91 {
    public static func swiftFunc0(a0: UInt16, a1: Int32, a2: Int64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        return hasher.finalize()
    }
    public struct Type91Sub2 {
        public static func swiftFunc0(a0: UInt32, a1: Double, a2: UInt64, a3: Int64) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
        public struct Type91Sub2Sub3 {
            public static func swiftFunc0(a0: UInt32, a1: Int, a2: Double, a3: UInt16, a4: Int16, a5: Int32, a6: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                return hasher.finalize()
            }
            public enum Type91Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt8, a1: UInt16, a2: UInt) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
                public class Type91Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int64) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public struct Type92 {
    public static func swiftFunc0(a0: Double, a1: UInt64, a2: Int64, a3: Int16, a4: Int16, a5: UInt32, a6: UInt16, a7: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        return hasher.finalize()
    }
    public struct Type92Sub2 {
        public static func swiftFunc0(a0: UInt64, a1: Int8, a2: UInt64, a3: Int64, a4: Double, a5: UInt, a6: Double, a7: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            return hasher.finalize()
        }
        public struct Type92Sub2Sub3 {
            public static func swiftFunc0(a0: Int32, a1: Int16) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
            public class Type92Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int8, a1: UInt32, a2: UInt64, a3: Int, a4: Int, a5: UInt8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    return hasher.finalize()
                }
                public class Type92Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int, a1: UInt16, a2: Int, a3: UInt8, a4: Int32, a5: UInt32, a6: Int64, a7: UInt8, a8: Double) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        return hasher.finalize()
                    }
                    public class Type92Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int8, a1: Int32, a2: UInt16, a3: Int8, a4: Int32, a5: Double, a6: Int32) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type93 {
    public static func swiftFunc0(a0: Int8, a1: Int, a2: UInt16, a3: Double, a4: Int, a5: UInt16, a6: Int16, a7: UInt, a8: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public class Type93Sub2 {
        public static func swiftFunc0(a0: Double, a1: UInt, a2: UInt32, a3: Int8, a4: Int, a5: Int32, a6: Double, a7: UInt8, a8: UInt16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            return hasher.finalize()
        }
        public class Type93Sub2Sub3 {
            public static func swiftFunc0(a0: UInt8, a1: UInt16, a2: UInt8, a3: UInt64) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                return hasher.finalize()
            }
        }
        
    }
    
}

public class Type94 {
    public static func swiftFunc0(a0: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public enum Type94Sub2 {
        public static func swiftFunc0(a0: UInt8) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            return hasher.finalize()
        }
        public class Type94Sub2Sub3 {
            public static func swiftFunc0(a0: Double, a1: Int32, a2: Double, a3: Double, a4: Double, a5: Int32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                return hasher.finalize()
            }
            public struct Type94Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int16, a1: Int32, a2: UInt, a3: UInt64, a4: UInt32, a5: UInt64, a6: Double, a7: Double, a8: Int16, a9: UInt64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    hasher.combine(a9);
                    return hasher.finalize()
                }
                public class Type94Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        return hasher.finalize()
                    }
                }
                
            }
            
        }
        
    }
    
}

public class Type95 {
    public static func swiftFunc0(a0: UInt8, a1: UInt16, a2: Double, a3: Int64, a4: UInt16, a5: UInt8, a6: UInt64) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        return hasher.finalize()
    }
    public class Type95Sub2 {
        public static func swiftFunc0(a0: UInt16, a1: Int32, a2: Int64, a3: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            return hasher.finalize()
        }
        public class Type95Sub2Sub3 {
            public static func swiftFunc0(a0: Int16, a1: Int) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                return hasher.finalize()
            }
            public enum Type95Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt16, a1: UInt16, a2: Int32, a3: Int32) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    return hasher.finalize()
                }
                public struct Type95Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt32, a1: Int8, a2: UInt16, a3: Int) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        return hasher.finalize()
                    }
                    public class Type95Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: UInt32, a1: UInt32, a2: UInt8, a3: UInt32, a4: Int64, a5: UInt16, a6: Int32, a7: Int8, a8: Int16) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            hasher.combine(a7);
                            hasher.combine(a8);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type96 {
    public static func swiftFunc0(a0: Int, a1: UInt, a2: UInt, a3: Double, a4: UInt) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        return hasher.finalize()
    }
    public enum Type96Sub2 {
        public static func swiftFunc0(a0: Int, a1: UInt32, a2: UInt8, a3: Int, a4: UInt32, a5: Int, a6: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            return hasher.finalize()
        }
        public struct Type96Sub2Sub3 {
            public static func swiftFunc0(a0: UInt64, a1: UInt, a2: Int32, a3: Double, a4: UInt, a5: UInt, a6: Int, a7: UInt, a8: UInt16, a9: UInt8) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                hasher.combine(a9);
                return hasher.finalize()
            }
            public class Type96Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Int32, a1: Double, a2: UInt, a3: Int64, a4: UInt16, a5: Int8, a6: UInt8, a7: Int8) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    return hasher.finalize()
                }
                public class Type96Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: UInt, a1: UInt32, a2: UInt64, a3: Int16, a4: UInt32, a5: Int8, a6: UInt8, a7: Double, a8: Int16) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        hasher.combine(a8);
                        return hasher.finalize()
                    }
                    public enum Type96Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Double) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            return hasher.finalize()
                        }
                        public enum Type96Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: UInt, a1: Int32, a2: UInt8, a3: Double, a4: UInt16, a5: UInt32, a6: Int64, a7: Int64, a8: UInt, a9: Int) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                hasher.combine(a4);
                                hasher.combine(a5);
                                hasher.combine(a6);
                                hasher.combine(a7);
                                hasher.combine(a8);
                                hasher.combine(a9);
                                return hasher.finalize()
                            }
                            public class Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: Int32, a1: Int8, a2: Int8, a3: UInt8) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    return hasher.finalize()
                                }
                                public class Type96Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: UInt64, a1: Int16) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        return hasher.finalize()
                                    }
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public class Type97 {
    public static func swiftFunc0(a0: UInt64, a1: Double, a2: Double) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        return hasher.finalize()
    }
}

public class Type98 {
    public static func swiftFunc0(a0: Int, a1: Int16, a2: UInt32, a3: UInt, a4: Double, a5: Int16, a6: UInt, a7: Int64, a8: UInt32) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        hasher.combine(a1);
        hasher.combine(a2);
        hasher.combine(a3);
        hasher.combine(a4);
        hasher.combine(a5);
        hasher.combine(a6);
        hasher.combine(a7);
        hasher.combine(a8);
        return hasher.finalize()
    }
    public enum Type98Sub2 {
        public static func swiftFunc0(a0: UInt, a1: UInt32, a2: Int, a3: Double, a4: Int, a5: UInt8, a6: Int16) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            return hasher.finalize()
        }
    }
    
}

public class Type99 {
    public static func swiftFunc0(a0: Int) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public enum Type99Sub2 {
        public static func swiftFunc0(a0: Int64, a1: UInt32) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            return hasher.finalize()
        }
        public struct Type99Sub2Sub3 {
            public static func swiftFunc0(a0: UInt16, a1: Int32, a2: UInt, a3: Int, a4: UInt8, a5: Double, a6: UInt32) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                return hasher.finalize()
            }
            public class Type99Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: UInt32, a1: UInt8, a2: UInt16, a3: Int8, a4: UInt16, a5: Double, a6: Double, a7: UInt8, a8: Double) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    hasher.combine(a3);
                    hasher.combine(a4);
                    hasher.combine(a5);
                    hasher.combine(a6);
                    hasher.combine(a7);
                    hasher.combine(a8);
                    return hasher.finalize()
                }
                public class Type99Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int8, a1: UInt8, a2: Double, a3: Double, a4: Int16, a5: Int) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        return hasher.finalize()
                    }
                    public enum Type99Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int16, a1: Int64, a2: UInt16, a3: Int8, a4: UInt64, a5: Int32, a6: Int32) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            hasher.combine(a2);
                            hasher.combine(a3);
                            hasher.combine(a4);
                            hasher.combine(a5);
                            hasher.combine(a6);
                            return hasher.finalize()
                        }
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

public enum Type100 {
    public static func swiftFunc0(a0: Int16) -> Int {
        var hasher = HasherFNV1a()
        hasher.combine(a0);
        return hasher.finalize()
    }
    public struct Type100Sub2 {
        public static func swiftFunc0(a0: UInt8, a1: Int16, a2: Int8, a3: UInt, a4: UInt16, a5: UInt8, a6: UInt8, a7: Double, a8: Int32, a9: Int) -> Int {
            var hasher = HasherFNV1a()
            hasher.combine(a0);
            hasher.combine(a1);
            hasher.combine(a2);
            hasher.combine(a3);
            hasher.combine(a4);
            hasher.combine(a5);
            hasher.combine(a6);
            hasher.combine(a7);
            hasher.combine(a8);
            hasher.combine(a9);
            return hasher.finalize()
        }
        public struct Type100Sub2Sub3 {
            public static func swiftFunc0(a0: Double, a1: UInt64, a2: Int64, a3: UInt16, a4: Double, a5: Int8, a6: UInt32, a7: Int8, a8: Double) -> Int {
                var hasher = HasherFNV1a()
                hasher.combine(a0);
                hasher.combine(a1);
                hasher.combine(a2);
                hasher.combine(a3);
                hasher.combine(a4);
                hasher.combine(a5);
                hasher.combine(a6);
                hasher.combine(a7);
                hasher.combine(a8);
                return hasher.finalize()
            }
            public struct Type100Sub2Sub3Sub4 {
                public static func swiftFunc0(a0: Double, a1: UInt64, a2: UInt64) -> Int {
                    var hasher = HasherFNV1a()
                    hasher.combine(a0);
                    hasher.combine(a1);
                    hasher.combine(a2);
                    return hasher.finalize()
                }
                public struct Type100Sub2Sub3Sub4Sub5 {
                    public static func swiftFunc0(a0: Int8, a1: UInt8, a2: UInt, a3: Double, a4: Int32, a5: Int32, a6: Int32, a7: UInt32) -> Int {
                        var hasher = HasherFNV1a()
                        hasher.combine(a0);
                        hasher.combine(a1);
                        hasher.combine(a2);
                        hasher.combine(a3);
                        hasher.combine(a4);
                        hasher.combine(a5);
                        hasher.combine(a6);
                        hasher.combine(a7);
                        return hasher.finalize()
                    }
                    public enum Type100Sub2Sub3Sub4Sub5Sub6 {
                        public static func swiftFunc0(a0: Int16, a1: Int32) -> Int {
                            var hasher = HasherFNV1a()
                            hasher.combine(a0);
                            hasher.combine(a1);
                            return hasher.finalize()
                        }
                        public enum Type100Sub2Sub3Sub4Sub5Sub6Sub7 {
                            public static func swiftFunc0(a0: Int32, a1: UInt, a2: UInt8, a3: Double) -> Int {
                                var hasher = HasherFNV1a()
                                hasher.combine(a0);
                                hasher.combine(a1);
                                hasher.combine(a2);
                                hasher.combine(a3);
                                return hasher.finalize()
                            }
                            public class Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8 {
                                public static func swiftFunc0(a0: UInt64, a1: UInt32, a2: UInt, a3: UInt32, a4: Int, a5: UInt8, a6: Int64, a7: Int8) -> Int {
                                    var hasher = HasherFNV1a()
                                    hasher.combine(a0);
                                    hasher.combine(a1);
                                    hasher.combine(a2);
                                    hasher.combine(a3);
                                    hasher.combine(a4);
                                    hasher.combine(a5);
                                    hasher.combine(a6);
                                    hasher.combine(a7);
                                    return hasher.finalize()
                                }
                                public enum Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9 {
                                    public static func swiftFunc0(a0: Double, a1: UInt16, a2: UInt32, a3: UInt16) -> Int {
                                        var hasher = HasherFNV1a()
                                        hasher.combine(a0);
                                        hasher.combine(a1);
                                        hasher.combine(a2);
                                        hasher.combine(a3);
                                        return hasher.finalize()
                                    }
                                    public class Type100Sub2Sub3Sub4Sub5Sub6Sub7Sub8Sub9Sub10 {
                                        public static func swiftFunc0(a0: Int8, a1: Double, a2: Double, a3: Int8, a4: Double, a5: UInt8, a6: Int64, a7: UInt8, a8: UInt, a9: UInt16) -> Int {
                                            var hasher = HasherFNV1a()
                                            hasher.combine(a0);
                                            hasher.combine(a1);
                                            hasher.combine(a2);
                                            hasher.combine(a3);
                                            hasher.combine(a4);
                                            hasher.combine(a5);
                                            hasher.combine(a6);
                                            hasher.combine(a7);
                                            hasher.combine(a8);
                                            hasher.combine(a9);
                                            return hasher.finalize()
                                        }
                                    }
                                    
                                }
                                
                            }
                            
                        }
                        
                    }
                    
                }
                
            }
            
        }
        
    }
    
}

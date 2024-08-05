## Demangling
# Background
In reflecting on a swift module through the `abi.json` files, there are a number
of mangled entry points that can be used for calling functions and methods. The
problem is that there are a number of symbols that are needed for interoperation that are not present in the json. In order to get these symbols, there are a few options: demangling the symbols in the binary to find what is needed or mangling
ourselves to generate the needed symbols.
While certain symbol types are straightforward to mangle, other such a type
metadata, other like functions and thunks to functions are complicated in a
way that grows with the complexity of the signature of the function.
Because of this and because Binding Tools for Swift has a well-tested
demangler, it makes sense to port this instead.

# How This Fits Into the Process of Binding
The general process should be:
- Read the corresponding dylib file using MachO.cs, specifying the target architecture
- Extract the symbols from the file
- Demangle the symbols into a collection of objects implementing a common interface
- Separate the demangled symbols into groupings needed for binding

This can be done either prior to reading the `abi.json` or in parallel to reading
it, but before the binding process begins. There are advantages to both: reading it prior allows the abi.json parser to refer to the demangled symbols in the
process of parsing and fill in needed blanks. Reading it in parallel allows the
obvious gains in parallel processing but requires the either another pass to
put the needed symbols where they belong or it requires the binding code to
look up the required symbols.

# Overview of the Demangling Code
The ported Apple demangler works by parsing the little language of the mangled
symbol and builds a tree of Nodes via a stack machine and a recursive descent
parser.

Our demangling code will take a tree of Nodes and use a pattern matching
mechanism to reduce the tree of nodes to easier to manipulate structures.

Nodes are simple discriminated unions that have 3 varieties of payloads:
- None
- Integral
- String
In addition Nodes contain an array of child Nodes

Proposed output type
```csharp
public interface ISwiftSymbol {
    string Symbol { get; }
}
```
Obviously this could be done as an abstract class instead of interface,
but the actual implementation is immaterial.

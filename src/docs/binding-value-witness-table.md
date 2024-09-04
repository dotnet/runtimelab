# Value Witness Table

Every value type in Swift has a compiler-defined value witness table. This is a common set of methods that are used by the compiler to work with.

The layout is:

- initializeBufferWithCopyOfBuffer `T *(*initializeBufferWithCopyOfBuffer)(T *dest, T* src, SwiftMetadata *metadata)` - initializes a pointer to blank buffer memory (dest) with a copy of a pointer to a buffered struct (src) with the provided type metadata.
- destroy `void (*destroy)(T *src, SwiftMetadata *metadata)` - destroys the contents of the struct (src) with the provided type metadata
- initializeWithCopy `T *(*initializeWithCopy)(T *dest, T* src, SwiftMetadata *metadata)`- initializes a pointer to memory (dest) with a copy of a struct pointer to by src using the provided type metadata
- assignWithCopy `T *(*assignWithCopy)(T *dest, T *src, SwiftMetadata *metadata)` - destroys the contents of dest before copying the contents of src on top of it using the provided type metadata
- initializeWithTake `T *(*initializeWithTake)(T *dest, T *src, SwiftMetadata *metadata)` - initializes the contents of fest with the contents of src, destroying src using the provided type metadata.
- assignWithTake `T *(*assignWithTake)(T *dest, T *src, SwiftMetadata *metadata)` - destroys the contents of dest before copying the contants of src on top of it, destroying src using the provided type metadata.
- getEnumTagSinglePayload - `unsigned int (*getEnumTagSinglePayload)(T* enumInst, unsigned int numEmptyCases, SwiftMetadata *metadata)` - gets the current discriminator for an enum with a single payload using the supplied type metadata.
- storeEnumTagSinglePayload - `void (*storeEnumTagSinglePayload)(T *enumInst, unsigned int whichCase, int numEmptyCases, SwiftMetadata *metadata)` - sets the current discriminator for an enum with a signle payload using the supplied type metadata.
- Size - machine word representing the size of the type in bytes
- Stride - machine word representing the stride of the type in bytes
- Flags - 32 bit unsigned int - contains flags that describe how to work with the time including the memory alignment
- ExtraInhabitantCount - 32 bit unsigned int - the number of extra inhabitants (free bits) in the type

## Getting the value witness table

There are two ways to get the value witness table for a type. The first is to get the address of memory using `dlsym` with the entry point for the value witness table. The second is get it relative to the type metadata. In Swift there is extended metadata information that always precedes the type metadata. One machine word back from the type metadata is a pointer to the ValueWitnessTable.

Probably the most efficient way to get the value witness table is to always go from the Swift Type Metadata.

It should also be noted that in the case of a heap allocated type, there will be a pointer to the deallocating deinit for the type two machine words behind back from the type metadata. This field is present in value types but is always 0.

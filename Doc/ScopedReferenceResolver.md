# Scoped reference resolver context

`Newtonsoft.Json.Serialization.ScopedReferenceResolver` gives `$id`/`$ref` reference handling an
**explicit, per-call context**: every `$id` definition, every `$ref` binding and every generated
reference Id belongs to the Serialize/Deserialize call that is currently running. The resolver
itself holds no state — the scope lives on the per-call internal serializer and is discarded with
it.

Guarantees:

- **Shared objects are shared, not copied.** Two `$ref` values pointing at the same `$id` resolve
  to the same object instance (`ReferenceEquals` is `true`).
- **No residue between calls.** The next `Deserialize` starts with an empty scope even when the
  same resolver instance is reused, so the same `$id` in a second payload defines a new,
  independent object.
- **No half-objects on failure.** If a call throws, its scope (including partially populated
  objects) is thrown away with the failed call's internal serializer; nothing leaks into the
  resolver or into the next call.
- **Explicit diagnostics.**
  - Duplicate `$id` in one payload → `JsonSerializationException`:
    `Duplicate object Id '2'. The same Id has already been defined in the current deserialization scope.`
    (surfaced as the inner exception of the reader's `Error reading object reference '2'.` error)
  - Unknown / forward `$ref` (an `$id` defined later in the document) → `JsonSerializationException`:
    `Could not resolve JSON reference '2'. ... Forward references to an Id defined later in the document are not supported.`
- **Cycles within the supported scope.** Backward references (a `$ref` to an already-defined
  `$id`) work, so object cycles such as A → B → C → A round-trip. Forward references are outside
  the supported scope and are diagnosed as above.
- **Default serializer preserved.** `DefaultReferenceResolver` and the default `JsonSerializer`
  behavior are untouched; the scoped resolver is strictly opt-in via
  `JsonSerializerSettings.ReferenceResolverProvider`.

## No arbitrary type loading

Pair the resolver with `Newtonsoft.Json.Serialization.AllowedTypesSerializationBinder` when
`TypeNameHandling` is enabled. The binder only binds `$type` names to an explicit allow-list of
`Type` instances and never calls `Type.GetType`/`Assembly.Load` for names coming from the payload,
so a malicious type name is rejected without loading anything:

```csharp
var settings = new JsonSerializerSettings
{
    ReferenceResolverProvider = () => new ScopedReferenceResolver(),
    TypeNameHandling = TypeNameHandling.Auto,
    SerializationBinder = new AllowedTypesSerializationBinder(typeof(SharedChild))
};
// {"$type":"System.IO.FileInfo, System.IO.FileSystem", ...} ->
// JsonSerializationException: Error resolving type specified in JSON '...'.
//   inner: Type '...' is not in the list of allowed types and will not be loaded.
```

## Three-node example (A → B → C → A)

```csharp
public class Node
{
    public string Name { get; set; }
    public Node Next { get; set; }
}

Node a = new Node { Name = "A" };
Node b = new Node { Name = "B" };
Node c = new Node { Name = "C" };
a.Next = b; b.Next = c; c.Next = a;   // cycle

var settings = new JsonSerializerSettings
{
    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
    ReferenceResolverProvider = () => new ScopedReferenceResolver()
};

string json = JsonConvert.SerializeObject(a, Formatting.Indented, settings);
// {
//   "$id": "1",
//   "Name": "A",
//   "Next": {
//     "$id": "2",
//     "Name": "B",
//     "Next": {
//       "$id": "3",
//       "Name": "C",
//       "Next": { "$ref": "1" }
//     }
//   }
// }

Node a2 = JsonConvert.DeserializeObject<Node>(json, settings);
Debug.Assert(object.ReferenceEquals(a2, a2.Next.Next.Next)); // cycle closes on the same instance
```

## dotnet commands

Run everything from the `Src` directory (SDK 10.0.300, see `Src/global.json`):

```bash
# build just the library (net10.0)
dotnet build Newtonsoft.Json/Newtonsoft.Json.csproj -p:LibraryFrameworks=net10.0

# run the scoped-resolver acceptance tests
dotnet test Newtonsoft.Json.Tests/Newtonsoft.Json.Tests.csproj \
  -p:TestFrameworks=net10.0 -p:LibraryFrameworks=net10.0 \
  --filter "FullyQualifiedName~ScopedReferenceResolverTests"

# run the related pre-existing reference/type-name suites (regression check)
dotnet test Newtonsoft.Json.Tests/Newtonsoft.Json.Tests.csproj \
  -p:TestFrameworks=net10.0 -p:LibraryFrameworks=net10.0 \
  --filter "FullyQualifiedName~PreserveReferencesHandlingTests|FullyQualifiedName~ReferenceLoopHandlingTests|FullyQualifiedName~TypeNameHandlingTests|FullyQualifiedName~MetadataPropertyHandlingTests"
```

Acceptance coverage in `ScopedReferenceResolverTests`:

| Requirement | Test |
| --- | --- |
| Two `$ref`s point to the same object | `TwoRefsResolveToSameSharedObject` |
| Duplicate `$id` rejected | `DuplicateIdIsRejected` |
| Same `$id` in a second call is an independent object | `SameIdsInSecondDeserializeAreIndependentObjects` |
| Failed call leaves no residual Ids / half-objects | `FailedDeserializeLeavesNoResidualState` |
| Unknown forward `$ref` diagnosed | `UnknownForwardReferenceIsDiagnosed` |
| Malicious `$type` name not loaded | `MaliciousTypeNameIsNotLoaded` |
| Three-node cycle round-trips | `ThreeNodeCycleRoundTrips` |
| Default serializer behavior preserved | `DefaultSerializerBehaviorIsUnchanged` |


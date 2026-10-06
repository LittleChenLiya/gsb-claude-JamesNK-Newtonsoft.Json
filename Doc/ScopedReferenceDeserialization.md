# Scoped Reference Deserialization (`JsonDeserializeScope`)

`Newtonsoft.Json.JsonDeserializeScope` is an explicit deserialization context in which
`$id` definitions, `$ref` resolution and `$type` binding all belong to a single
`Deserialize` call. It is an opt-in wrapper; the default `JsonSerializer`/`JsonConvert`
behavior is unchanged.

## Guarantees

| Concern | Behavior |
| --- | --- |
| Shared objects | Two `$ref` values naming the same `$id` resolve to the **same** object instance — never two copies. |
| Duplicate `$id` | Rejected with `JsonSerializationException` ("Duplicate reference Id ..."). |
| Unknown / forward `$ref` | Rejected with `JsonSerializationException` ("Cannot resolve reference ..."). Forward references to `$id`s defined later in the JSON are not supported. |
| Arbitrary type loading | `TypeNameHandling.None` by default, so `$type` is ignored. Enabling `TypeNameHandling` requires an explicit `ISerializationBinder` (e.g. `KnownTypesSerializationBinder` whitelist), otherwise the scope throws `InvalidOperationException`. |
| Failure isolation | A failed call discards its per-call `ScopedReferenceResolver`; half-registered objects are never stored in external state. |
| Call isolation | The next `Deserialize` call starts with no residual IDs; the same `$id` may be defined again and produces independent objects. |
| Cycles | Supported within `PreserveReferencesHandling.Objects`: object-to-object cycles (e.g. `Next` pointing back to an earlier node) round-trip. Cycles through collection items or constructor arguments are outside the supported scope. |

## Three-node example

One shared node defined with `$id`, two `$ref` values pointing at it:

```json
{
  "shared": { "$id": "1", "name": "shared" },
  "left":   { "$ref": "1" },
  "right":  { "$ref": "1" }
}
```

```csharp
using Newtonsoft.Json;

public class SharedNode
{
    public string Name { get; set; }
}

public class NodeGraph
{
    public SharedNode Shared { get; set; }
    public SharedNode Left { get; set; }
    public SharedNode Right { get; set; }
}

var scope = new JsonDeserializeScope();
NodeGraph graph = scope.Deserialize<NodeGraph>(json);

// graph.Left and graph.Right are the SAME instance as graph.Shared:
Console.WriteLine(ReferenceEquals(graph.Shared, graph.Left));   // True
Console.WriteLine(ReferenceEquals(graph.Shared, graph.Right));  // True

// A second call with the same JSON is independent — no residual IDs:
NodeGraph again = scope.Deserialize<NodeGraph>(json);
Console.WriteLine(ReferenceEquals(graph.Shared, again.Shared)); // False
```

A three-node cycle (`A -> B -> C -> A`) is within the supported scope because each
`$ref` points back to an already-defined `$id`:

```json
{
  "$id": "1", "name": "A",
  "next": { "$id": "2", "name": "B",
    "next": { "$id": "3", "name": "C",
      "next": { "$ref": "1" } } } }
```

## dotnet commands

Run only the scoped tests (net8.0 target, NUnit):

```bash
dotnet test Src/Newtonsoft.Json.Tests/Newtonsoft.Json.Tests.csproj \
  -f net8.0 -p:TestFrameworks=net8.0 \
  --filter "FullyQualifiedName~JsonDeserializeScopeTests"
```

Build just the library for one target framework:

```bash
dotnet build Src/Newtonsoft.Json/Newtonsoft.Json.csproj -f net8.0 -p:LibraryFrameworks=net8.0
```

## API overview

- `JsonDeserializeScope` — per-call scoped deserialization entry point.
- `Newtonsoft.Json.Serialization.ScopedReferenceResolver` — single-use `IReferenceResolver`
  with explicit duplicate-`$id` / unknown-`$ref` diagnostics; can also be used directly via
  `JsonSerializerSettings.ReferenceResolver` for one deserialize call.
- `Newtonsoft.Json.Serialization.KnownTypesSerializationBinder` — `ISerializationBinder`
  whitelist; any `$type` not in the known-types list is rejected before the runtime
  attempts to load it.

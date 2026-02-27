# Code practices

## Immutability

Make sure to default to immutability, unless object mutations are absolutely necessary. This includes not having public setters on class / record properties (default should be `init`, not `set`). Same goes for exposing mutable collections as public properties / fields - the default choice should be readonly versions of collections (or immutable). This also applies for method parameters. For example - avoid passing `List` to a method if there's no need for the list to be mutated, otherwise caller doesn't have a guarantee the collection will remain intact after method call ends

## Interal by default

Classes should be internal by default, unless they're intentionally expected to be used outside of assembly scope. Especially this goes for dependency injection pattern - interfaces should be `public` while all concrete implementations should be `internal` and they should be injected to the container within the same assembly.

## Sealed by default

If a class has no intention of being extended - it should be `sealed`

## Testing

When writing tests and using mocks from `Moq` library - default to using `Strict` mock behavior (unless mocking object is very generic, like `HttpClient` or `ILogger`). All mock setups should be verifiable.

During mock setups - avoid using `It.IsAny<...>`, except for `CancellationToken`s. Instead - use `It.Is`, since it allows to provide built-in validation for mock parameters.

## Avoid global static mutable state

Try not to have global static properties / collections that are mutable. In most cases it can be avoided by injecting corresponding service abstraction. It's ok to have global immutable thread-safe state exposed, otherwise consider alternatives.

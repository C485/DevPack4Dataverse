---
applyTo: '**/*.cs'
description: 'C# 14 best practices, formatting guidelines, and Dataverse documentation requirements for AI code generation.'
---

# C# 14 Best Practices for AI Code Generation Instructions

## Dataverse Documentation (Required)

When working with Dataverse code (SDK usage, Organization service, Web API, plug-ins, metadata, queries, solutions, etc.), always consult the Microsoft Power Apps documentation source before implementing or changing behavior:

- **Documentation repository:** https://github.com/MicrosoftDocs/powerapps-docs/tree/main
- **Dataverse developer docs:** https://github.com/MicrosoftDocs/powerapps-docs/tree/main/powerapps-docs/developer/data-platform
- **Published documentation:** https://learn.microsoft.com/power-apps/developer/data-platform/
- **Repository reference:** `docs/dataverse-documentation.md`

Use the relevant page under `powerapps-docs/developer/data-platform/` as the source of truth. Reference the documentation path in code comments or PR descriptions when the behavior is non-obvious.

### Always sync the documentation first

Before starting any Dataverse-related task, update the local documentation checkout so you are working from the latest docs:

- **Preferred:** run `pwsh ./docs/sync-dataverse-docs.ps1` (or `powershell -File ./docs/sync-dataverse-docs.ps1`).
- **Manual:**
  ```bash
  # First time only
  git clone --depth 1 --filter=blob:none --sparse https://github.com/MicrosoftDocs/powerapps-docs.git docs/powerapps-docs
  git -C docs/powerapps-docs sparse-checkout set powerapps-docs/developer/data-platform

  # Every subsequent Dataverse task
  git -C docs/powerapps-docs pull --ff-only
  ```

Read the relevant files under `docs/powerapps-docs/powerapps-docs/developer/data-platform/` before changing Dataverse code. Never rely on a stale local copy. The local clone is intentionally ignored by git.

## Core Language Features

### Use Primary Constructors
- Use primary constructors for classes and structs when appropriate
- Leverage parameter validation attributes directly in primary constructor parameters
- Prefer primary constructors over traditional constructors when the constructor only assigns parameters to fields/properties

### Collection Expressions
- Use collection expressions `[]` for array, list, and span initialization
- Prefer `[1, 2, 3]` over `new[] { 1, 2, 3 }` or `new List<int> { 1, 2, 3 }`
- Use spread operators `..` for combining collections

### Pattern Matching Enhancements
- Use list patterns for matching against collections
- Leverage slice patterns with ranges for partial collection matching
- Prefer switch expressions over switch statements when returning values

## Code Style and Formatting

### File-Scoped Namespaces
- Always use file-scoped namespace declarations (`namespace MyNamespace;`)
- Never use traditional block-scoped namespaces

### Access Modifiers
- Explicitly declare access modifiers for all members
- Use `readonly` for fields that are only assigned in constructors
- Use `required` keyword for properties that must be initialized

### Type Declarations
- Use `var` for local variables when the type is obvious from the right side
- Use explicit types for method parameters, return types, and field declarations
- Prefer target-typed `new()` expressions when the type is clear from context

### Method and Property Formatting
- Use expression-bodied members for simple one-line implementations
- Place opening braces on new lines for methods, classes, and namespaces
- Use lambda expressions with modern syntax: `x => x.Property`

## Modern C# Patterns

### Null Safety
- Enable nullable reference types with `#nullable enable`
- Use null-conditional operators (`?.`, `??`, `??=`) appropriately
- Prefer `ArgumentNullException.ThrowIfNull()` for parameter validation

### String Handling
- Use raw string literals for multi-line strings and strings containing quotes
- Use string interpolation `$""` instead of `String.Format()` or concatenation
- Use `string.IsNullOrEmpty()` and `string.IsNullOrWhiteSpace()` for validation

### Exception Handling
- Use specific exception types rather than generic `Exception`
- Implement proper exception filtering with `when` clauses
- Use `ThrowHelper` patterns for commonly thrown exceptions

## Performance Optimizations

### Memory Efficiency
- Use `Span<T>` and `ReadOnlySpan<T>` for high-performance scenarios
- Prefer `stackalloc` for small, short-lived arrays
- Use `ref` and `in` parameters to avoid unnecessary copying

### Collections
- Use appropriate collection types: `List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`
- Prefer `IReadOnlyList<T>` and `IReadOnlyCollection<T>` for immutable data exposure
- Use `CollectionsMarshal` for high-performance collection operations when needed

## Async/Await Best Practices

### Async Methods
- Use `async Task` for void-returning async methods
- Use `async Task<T>` for value-returning async methods
- Use `async ValueTask<T>` for high-performance scenarios where allocation matters
- Always use `ConfigureAwait(false)` in library code

### Cancellation
- Accept `CancellationToken` parameters in long-running async methods
- Use `CancellationToken.ThrowIfCancellationRequested()` for cooperative cancellation
- Pass cancellation tokens through the call chain

## XML Documentation
- Provide XML documentation comments for all public APIs
- Use `<summary>`, `<param>`, `<returns>`, and `<exception>` tags appropriately
- Include `<example>` sections for complex methods

## Testing Patterns
- Use modern test frameworks like xUnit, NUnit, or MSTest
- Prefer `Assert.Throws<T>()` over try-catch blocks in tests
- Use meaningful test method names that describe the scenario

## File Organization
- One public type per file
- File name should match the primary type name
- Organize using statements: System namespaces first, then third-party, then project namespaces
- Remove unused using statements

## Code Quality Rules
- Follow Microsoft's .NET coding conventions
- Use EditorConfig files for consistent formatting across teams
- Enable and address code analysis warnings
- Use code contracts and defensive programming practices

## Post-Generation Actions
- **ALWAYS trim trailing whitespace from all lines after any code changes**
- Ensure consistent line endings (LF on Unix, CRLF on Windows)
- Remove any extra blank lines at the end of files
- Ensure proper indentation (4 spaces for C#, no tabs)

## Security Considerations
- Validate all input parameters
- Use secure string handling practices
- Implement proper error handling without exposing sensitive information
- Follow OWASP guidelines for secure coding practices

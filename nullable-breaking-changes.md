# Nullable reference type changes

All projects enable nullable reference types (`<Nullable>enable</Nullable>`) and treat
nullable warnings as errors. These annotations affect consumers that have NRTs enabled;
the runtime behavior is unchanged.

## Return types that may be null

| API | Change |
| --- | --- |
| `Connection.Execute<T>` / `SdkProxy.Execute<T>` / `IDataverseConnectionLayer.Execute<T>` | Returns `T?`. The implementation casts the response with `as T`, so a wrong response type yields null. |
| `Connection.ExecuteAsync<T>` / `SdkProxy.ExecuteAsync<T>` / `IDataverseConnectionLayer.ExecuteAsync<T>` | Returns `Task<T?>`. |
| `SdkProxy.StringToSecureString` | Accepts `string?` and returns `SecureString?` (null input returns null). |
| `EntityKey.FromEntityReference` | Accepts `EntityReference?` and returns `EntityKey?`. |
| `EntityKey.FromEntity` | Accepts `Entity?` and returns `EntityKey?`. |
| `IEntityCache.GetAsync` (and implementations) | Returns `Task<CachedEntity?>`; a cache miss returns null. |
| `SequentialStepResult.RetrievedEntity` | Returns `Entity?`; null when the step did not retrieve a record. |
| `MappedRecord<T>.Reference` | Returns `EntityReference?`; null when no record was saved. |
| `MappedRecord<T>.SavedEntity` | Returns `Entity?`; null when no record was saved. |
| `MappedRecord<T>.Source` | Returns `object?`. |

## Parameters that accept null

- `RequestSettings?` on connection, transaction, execute-multiple, and sequential APIs.
- `MappingRunOptions?` on `IMapper` and `MapperWrapper.ExecuteAsync`.
- `MapperOptions.PreSave` and `MapperOptions.Validator` are `?` delegates.
- `MapperWrapperOptions.Validator` is a `?` delegate.
- `MapperExecution.RunOptions` is `MappingRunOptions?`.
- `MapperWrapper.TryGet(string, out IMapper?)`.
- `MapperWrapper` constructor `cache` and `entityLock` are optional nullable references.
- `EntityKey.Equals(EntityKey?)` and `Equals(object?)` match the standard equality contract.
- `ExecuteMultipleRequestSettings.ErrorReport` and `ReportProgress` are nullable delegates;
  their default no-op delegates are unchanged.

## Nullable data on results

- `MappingError.Source`, `Target`, `Field`, and `Exception` are nullable.
- `MappingValidationContext.MapperName`, `Source`, and `Target` are nullable.
- `MapperWrapperOptions` validation and cache callbacks may be absent.

Consumers that compile with NRTs enabled can see new warnings where these APIs are used
without a null check. Add the appropriate check or `!` where the value is guaranteed by the
call site.

# DevPack4Dataverse audit task list

Audit date: 2026-11-21
Completion: all items below are implemented. Final Release build: 0 warnings, 0 errors. With live Redis (`DEVPACK_REDIS_CONNECTION=127.0.0.1:6390,allowAdmin=true`), final tests are 366 passed, 0 failed, 0 skipped on each of `net8.0`, `net11.0`, and `net48`. Without the variable, the 10 Redis integration tests skip. Live Dataverse also passed: connection, CRUD, ExecuteMultiple, adaptive, transaction, sequential, bulk, mapper row version, money/choice, FieldDrill, file and image operations. Test records were cleaned up.
Scope: whole repository (`DevPack4Dataverse`, tests, Redis adapter, Playground).
Assumed load: background integration/batch jobs moving thousands to millions of records through a few connections, possibly several app instances sharing Redis.
Baseline: Release build succeeded with 0 warnings; `net8.0` tests: 306 passed, 0 failed, 10 skipped (Redis tests need `DEVPACK_REDIS_CONNECTION`).
Reference: local Microsoft Power Apps docs checkout (`docs/powerapps-docs`, last commit 2026-11-20).

Line numbers are from the audit date and will drift. Tick a box only when the fix and its test are in.

## Must fix

- [x] **M1. Stop an `ExecuteMultiple` split after a fault and keep successful part responses**
  - File: `DevPack4Dataverse/ExecuteMultiple/ExecuteMultipleLogic.cs:352-385`
  - Problem: the `MaxBatchSize` split loop sends later parts even when `continueOnError=false` already saw a fault; if a later part throws a transport error, earlier successful responses are discarded and all requests are reported as failed. A retry duplicates committed rows.
  - Fix: run parts in order; stop on the first fault for the simple overload; preserve already-collected responses and report only the failed part for the continue-on-error overload.
  - Test: `MaxBatchSize=2`, six requests, fault in part 2; assert no part 3 is sent and the partial result is correct.
  - Docs: `developer/data-platform/org-service/execute-multiple-requests.md`.

- [x] **M2. Stop the .NET Framework parallel loop when a worker throws**
  - File: `DevPack4Dataverse/Utils/AsyncParallel.cs:43-92`
  - Problem: the `net462`/`net472`/`net48` fallback keeps taking items after one worker faults, so `BulkOperation` commits every remaining chunk after a failure.
  - Fix: linked `CancellationTokenSource`; cancel on a worker fault; check the token before each `MoveNext`/body.
  - Test: net48 test where item 1 throws and items 2..n must not run.

- [x] **M3. Report which bulk chunks committed after a failure**
  - File: `DevPack4Dataverse/BulkOperations/BulkOperationLogic.cs:72-102,268-300`
  - Problem: chunks in flight can commit after the failed chunk, but the exception names only the failed chunk and the partial ids/results are discarded. Resuming from `FailedChunkStartIndexDataKey` duplicates rows.
  - Fix: attach completed chunk indices and collected ids/results to the exception, or return a partial result object; document idempotent retry.
  - Docs: `developer/data-platform/bulk-operations.md` ("On Error behavior").

- [x] **M4. Apply batch `RequestSettings` to the inner messages, not only the outer batch**
  - Files: `DevPack4Dataverse/ExecuteMultiple/ExecuteMultipleLogic.cs:408-414`, `DevPack4Dataverse/SdkProxy.cs:543-548`
  - Problem: `ConcurrencyBehavior`, `SkipPluginExecution`, `SuppressDuplicateDetection`, `tag`, and `PartitionId` on the batch settings are added to the outer request. Dataverse reads them from each inner message; only caller id is batch-wide. Plug-ins still run and concurrency protection is lost.
  - Fix: apply non-impersonation settings to each inner request without overwriting per-message settings, or reject them on the batch overload and require builder-level settings.
  - Docs: `developer/data-platform/optional-parameters.md`, `org-service/execute-multiple-requests.md:24`.

- [x] **M5. Do not auto-page `TopCount` queries; do not mutate the caller's `QueryExpression`**
  - File: `DevPack4Dataverse/Connection.cs:690-705`
  - Problem: `TopCount` plus `PageInfo` is rejected by Dataverse. A top-5 query throws. The code also writes `PageInfo` into a shared query, so concurrent calls can return only page 1 or duplicate pages.
  - Fix: skip auto-paging when `TopCount` has a value; page on a private copy of the query.
  - Test: `TopCount=5` returns five rows; two concurrent calls with the same query do not lose or duplicate rows.
  - Docs: `developer/data-platform/org-service/queryexpression/page-results.md:23`.

- [x] **M6. Make `DisposeAsync` wait longer than the service timeout, or skip busy clients**
  - File: `DevPack4Dataverse/SdkProxy.cs:396-417`
  - Problem: Dispose waits only 30 seconds per connection, then disposes the `ServiceClient`. A valid Dataverse call can run 4 minutes, so a shutdown can abort it after a partial commit.
  - Fix: use at least `ServiceClient.MaxConnectionTimeout` plus margin (5 minutes is safe), or log and skip a client whose lease is still held.
  - Test: Dispose blocks while a lease is held and disposes each client once after release.
  - Docs: `developer/data-platform/bulk-operations.md:688`.

- [x] **M7. Normalize mapper change detection across wrapper and CLR values**
  - File: `DevPack4Dataverse/Mapper/Internal/ValueComparer.cs:43-63`
  - Problem: `decimal` vs `Money`, `int` vs `OptionSetValue`, and empty-id alternate-key `EntityReference`s compare unequal or falsely equal. Unchanged values are re-sent and can overwrite concurrent changes; a changed lookup can be skipped. `DateTime` ticks comparison can also re-send or skip the wrong change.
  - Fix: normalize `Money.Value`, `OptionSetValue.Value`, numerics; compare key attributes when both ids are empty; compare `DateTime` instants or the column's date behaviour. Add tests for each pair.

- [x] **M8. Send `RowVersion` with mapper updates when `IfRowVersionMatches` is used**
  - File: `DevPack4Dataverse/Mapper/Mapper.cs:275-281`
  - Problem: the mapper builds the update from changed attributes only and does not copy `current.RowVersion`, so `ConcurrencyBehavior.IfRowVersionMatches` fails with `ConcurrencyVersionNotProvided`.
  - Fix: `Retrieve` and `RetrieveMultiple` populate `Entity.RowVersion` automatically; do not add a `rowversion` column (the live service rejects it). Copy `current.RowVersion` to the update and preserve it in the in-memory and Redis cache entries.
  - Test: the mapper copies the row version; a live `IfRowVersionMatches` update passed. A live race test is still the caller's responsibility.
  - Docs: `developer/data-platform/optimistic-concurrency.md`, `org-service/entity-operations-update-delete.md`.

- [x] **M9. Scope the mapper cache by impersonated user, or disable it while impersonating**
  - Files: `DevPack4Dataverse/Mapper/Internal/RecordStore.cs:74-135,225-247`, `DevPack4Dataverse/Mapper/MapperWrapper.cs:47-49`
  - Problem: cache keys are `EntityKey` only. A record cached while running as user A can be served to user B, exposing fields or records across security profiles or tenants.
  - Fix: include the impersonation identity in the cache key, or bypass read/write cache when `RequestSettings.HasImpersonation` is true.
  - Test: two identities with different access read the same key; the second must not receive the first user's cached record.
  - Docs: `developer/data-platform/impersonate-another-user.md`.

## Should fix

- [x] **S1. Count only attempted requests in `ExecuteMultipleLogicResult.RecordsProcessed`**
  - File: `DevPack4Dataverse/ExecuteMultiple/ExecuteMultipleLogic.cs:301-305`
  - Problem: the faulting chunk adds its whole length even when it stopped at request 0. A resume from `RecordsProcessed` skips unsent work.
  - Fix: add `firstFault.RequestIndex + 1` for the faulting chunk, or count stored responses; document "attempted".

- [x] **S2. Restore alternate-key columns for cache and validation after a save**
  - Files: `DevPack4Dataverse/Mapper/Mapper.cs:295-303,355-356,451-466`, `DevPack4Dataverse/Mapper/Internal/RecordStore.cs:238-247`
  - Problem: the key column is removed from `Attributes` before the upsert and then marked loaded in the cache; validation fields omit it too. The next cached run sees a false change and validation cannot read the saved key.
  - Fix: copy the key values back into `Attributes` after the save, or include `KeyAttributes.Keys` in validation fields and do not mark them loaded in the cache.
  - Test: create by alternate key, rerun from cache with `UseCacheForChangeDetection=true`, validate the key column.

- [x] **S3. Reject duplicate identities before bulk chunking**
  - Files: `DevPack4Dataverse/BulkOperations/BulkOperationLogic.cs:122-142,162-194`, `DevPack4Dataverse/BulkOperations/BulkOperationTargets.cs:60-90`
  - Problem: duplicates in different chunks run in parallel, so `UpdateMultiple` becomes last-writer-wins and `UpsertMultiple` can create twice and fault.
  - Fix: when `requireIdentity`, build a `HashSet` of ids or sorted key values and throw before chunking.
  - Docs: `developer/data-platform/bulk-operations.md` duplicate-record sections.

- [x] **S4. Keep completed results when a sequential step or mapper batch fails**
  - Files: `DevPack4Dataverse/Sequential/SequentialRequestBuilder.cs:374-444`, `DevPack4Dataverse/Mapper/Mapper.cs:111-162`, `DevPack4Dataverse/Mapper/MapperWrapper.cs:171-198`
  - Problem: a later failure throws away created ids and saved records held in local lists. A retry duplicates completed work.
  - Fix: attach the completed context/results to `exception.Data`, or return a partial result object.
  - Test: create step succeeds, next step fails, the created id is available.

- [x] **S5. Require every part of an alternate key to be non-empty**
  - File: `DevPack4Dataverse/Mapper/EntityKey.cs:126-145`
  - Problem: a two-column key with one null passes `IsEmpty`; the query then contains `column Equal null`, which errors or matches the wrong rows.
  - Fix: treat the key as empty if any alternate-key value is null or empty; reject nulls in `ByAlternateKey`.
  - Test: composite key with a null throws the clear "key is empty" error before any read.

- [x] **S6. Verify caller-id can be applied and add a restore test**
  - Files: `DevPack4Dataverse/Connection.cs:289-359`, `DevPack4Dataverse/Utils/ReplaceAndRestoreCallerId.cs`
  - Problem: no test covers set/restore. On an AD/on-prem `ServiceClient` the caller-id setter can be a no-op, so the request runs as the connection user while the code believes it is impersonating.
  - Fix: verify the connection supports impersonation (or restrict it to OAuth) and throw when it does not; test both ids restored after a throwing execute.
  - Docs: `developer/data-platform/impersonate-another-user.md`, `xrm-tooling/use-connection-strings-xrm-tooling-connect.md`.

- [x] **S7. Make image `Delete(record, field)` delete or fail explicitly**
  - File: `DevPack4Dataverse/FieldMethods/FileOrImageFieldTypeMethods.cs:44-58`
  - Problem: an image column returns a `byte[]`, so the method reads no `Guid`, returns `false`, and leaves the image in place.
  - Fix: for `byte[]`, set the attribute to null and update the record, or throw `NotSupportedException` pointing to `Delete(fileId)`.
  - Docs: `developer/data-platform/image-column-data.md:28,595-601`.

- [x] **S8. Fall back to the base-language option label, not the user's language**
  - File: `DevPack4Dataverse/FieldMethods/OptionSetFieldTypeMethods.cs:111,196-220,336-358`
  - Problem: a missing translation returns `UserLocalizedLabel` (possibly the wrong language) or null; `MapStringToOptionSet` has no fallback at all.
  - Fix: skip null/empty labels, fall back to base language 1033, and use the same fallback in `MapStringToOptionSet`.
  - Docs: `developer/data-platform/customize-labels-support-multiple-languages.md:40`.

- [x] **S9. Prefer `EntityReference.Id` over stale key attributes**
  - File: `DevPack4Dataverse/Mapper/EntityKey.cs:233-246`
  - Problem: `FromEntityReference` checks `KeyAttributes` before a valid `Id`, unlike `FromEntity` and the SDK. The mapper can query an old key or create a duplicate.
  - Fix: return `ById` when `reference.Id != Guid.Empty`; use key attributes only when the id is empty.
  - Docs: `developer/data-platform/use-alternate-key-reference-record.md:108-110`.

- [x] **S10. Order preload and alternate-key queries before paging**
  - File: `DevPack4Dataverse/Mapper/Internal/RecordStore.cs:140-295`
  - Problem: unordered paging can return the same row on two pages. `LoadAllAsync` treats the duplicate alternate key as a multi-match error; `RetrieveAsync` can do the same.
  - Fix: add an `OrderExpression` on the primary id attribute before paging, using `PrimaryKeyAttributeProvider`.
  - Docs: `developer/data-platform/includes/cc-ordering-paging.md:71`, `org-service/queryexpression/page-results.md`.

- [x] **S11. Detect a lost Redis lock during renewal**
  - File: `DevPack4Dataverse.Mapper.Redis/RedisEntityLock.cs:171-194`
  - Problem: renewal failures and a `0` result from the compare-and-expire script are ignored. The handle still believes it holds the lock, so another instance can enter and both updates run.
  - Fix: capture the renewal result; if it is `0`, mark the handle lost and throw or cancel before the update. Use a fencing token for work that can outlive the TTL; size the TTL above the worst-case save.

## Backlog

- [x] **B1. Fix the check-then-act race on the 1,000-request transaction cap**
  - File: `DevPack4Dataverse/ExecuteTransaction/ExecuteTransactionRequestBuilder.cs:132-141`
  - Fix: reserve a slot with a lock or `Interlocked.Increment` before enqueueing.

- [x] **B2. Delete dead code**
  - Files: `DevPack4Dataverse/Models/OrganizationServiceContextSettings.cs`, `DevPack4Dataverse/ConnectionLease.cs:48-53`, `DevPack4Dataverse/Models/Statistic.cs:28-36`
  - Fix: delete the unused settings class, the two internal `GetOrganizationServiceContext` methods, and `IsFromLastNMinutes`. Update the stale README note. About 45 lines.

- [x] **B3. Deep-copy cached values and preserve formatted values**
  - File: `DevPack4Dataverse/Mapper/Internal/EntityCopy.cs:23-45`
  - Problem: `EntityCopy.Clone` copies `Attributes` by reference and drops `FormattedValues`; a caller can mutate the cached `EntityReference`/`byte[]`.
  - Fix: copy formatted values, clone `byte[]` and `EntityReference`.

- [x] **B4. Avoid a full Redis keyspace scan on table cache clears**
  - File: `DevPack4Dataverse.Mapper.Redis/RedisEntityCache.cs:185-217`
  - Fix: keep a per-table key set so `ClearAsync` is one set lookup.

- [x] **B5. Update the test/Playground dependency with the vulnerable transitive package**
  - Files: `DevPack4Dataverse.Tests/DevPack4Dataverse.Tests.csproj`, `DevPack4Dataverse.Playground/DevPack4Dataverse.Playground.csproj`
  - Problem: `System.Text.RegularExpressions 4.3.0` (GHSA-cmhx-cq75-c4mj, High) is restored for tests and Playground. Shipped projects are clean.
  - Fix: bump the test SDK/xunit or pin a patched version.

- [x] **B6. Remove an existing cache entry when the TTL is zero**
  - Files: `DevPack4Dataverse/Mapper/Cache/InMemoryEntityCache.cs:73-77`, `DevPack4Dataverse.Mapper.Redis/RedisEntityCache.cs:70-74`
  - Problem: a zero TTL skips the write but leaves an entry written earlier with a positive TTL.
  - Fix: delete the key when `timeToLive <= TimeSpan.Zero`.

- [x] **B7. Support enums whose underlying type is not `int`**
  - File: `DevPack4Dataverse/FieldMethods/OptionSetFieldTypeMethods.cs:162`
  - Problem: `(T)(object)int` throws for `enum E : short`/`byte`/`long`.
  - Fix: use `(T)Enum.ToObject(typeof(T), value)`.

- [x] **B8. Test Redis serializer round-trips for common primitives**
  - File: `DevPack4Dataverse.Tests/RedisEntityCacheSerializerTests.cs`
  - Fix: add a `[Theory]` over `int`, `long`, `bool`, `float`, `double`, `Guid`, and an `Assert.Throws<MapperException>` for an unsupported type.

- [x] **B9. Allow a documented `ExecuteTransactionRequest` inside an `ExecuteMultipleRequest`**
  - File: `DevPack4Dataverse/ExecuteMultiple/ExecuteMultipleRequestBuilder.cs:107-113`
  - Problem: the builder rejects both nested types, but the docs allow a transaction inside a batch; only `ExecuteMultiple` inside `ExecuteMultiple` is forbidden.
  - Fix: reject only `ExecuteMultipleRequest` here.
  - Docs: `developer/data-platform/org-service/use-executetransaction.md:25`.

- [x] **B10. Normalize numeric values in `EntityKey`**
  - File: `DevPack4Dataverse/Mapper/EntityKey.cs:397-398`
  - Problem: `1.0m` and `1.00m` produce different cache/lock keys, so concurrent creates do not share a lock.
  - Fix: format decimals with a canonical format such as `"G29"`.

## Notes

- Live Redis: passed on 2026-11-21 against `127.0.0.1:6390` (18 Redis tests; 366/366 per target framework).
- The `net48` fallback is executed by the `net48` test run, not only by a copied probe.
- The Redis integration tests need `DEVPACK_REDIS_CONNECTION`; without it, the lock/cache correctness tests are skipped.
- Live Dataverse: passed on 2026-11-21. Covered connection, TopCount, CRUD, ExecuteMultiple create/delete, adaptive execution, transaction, sequential, bulk create/update, mapper row version, money, choice, FieldDrill (lookup and self-lookup), file upload/download/delete, and image upload/delete. The account test plugin rewrote account names, so the self-lookup check used the retrieved name. Test records were deleted.
- Not checked: generated API reference pages. The live Dataverse run used a temporary harness outside the repo; no credentials were committed.

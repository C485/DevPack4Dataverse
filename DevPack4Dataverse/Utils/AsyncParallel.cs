/*
Copyright 2022 Kamil Skoracki / C485@GitHub

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace DevPack4Dataverse.Utils;

internal static class AsyncParallel
{
    public static Task ForEachAsync<T>(
        IEnumerable<T> source,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken,
        Func<T, CancellationToken, Task> body
    )
    {
#if NET6_0_OR_GREATER
        return Parallel.ForEachAsync(
            source,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            (item, token) => new ValueTask(body(item, token))
        );
#else
        return ForEachWithSemaphoreAsync(source, maxDegreeOfParallelism, cancellationToken, body);
#endif
    }

#if !NET6_0_OR_GREATER
    private static async Task ForEachWithSemaphoreAsync<T>(
        IEnumerable<T> source,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken,
        Func<T, CancellationToken, Task> body
    )
    {
        if (maxDegreeOfParallelism == -1)
        {
            maxDegreeOfParallelism = Environment.ProcessorCount;
        }
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        }

        int workerCount = Math.Min(maxDegreeOfParallelism, 1024);
        using IEnumerator<T> enumerator = source.GetEnumerator();
        object gate = new();

        Task Worker()
        {
            return Task.Run(
                async () =>
                {
                    while (true)
                    {
                        T item;
                        lock (gate)
                        {
                            if (!enumerator.MoveNext())
                            {
                                return;
                            }

                            item = enumerator.Current;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        await body(item, cancellationToken).ConfigureAwait(false);
                    }
                },
                cancellationToken
            );
        }

        Task[] workers = Enumerable.Range(0, workerCount).Select(_ => Worker()).ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);
    }
#endif
}

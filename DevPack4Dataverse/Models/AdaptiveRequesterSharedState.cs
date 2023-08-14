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

using DevPack4Dataverse.New;

namespace DevPack4Dataverse.Models;

/// <summary>
///     Represents the shared state used by the <see cref="AdaptiveRequester" /> class to store response time and request
///     count data.
/// </summary>
public class AdaptiveRequesterSharedState
{
    /// <summary>
    ///     Gets or sets the buffer used to store the response times of previous requests.
    /// </summary>
    public TimeSpan[] ResponseTimeBuffer { get; set; }

    /// <summary>
    ///     Gets or sets the buffer used to store the request counts of previous requests.
    /// </summary>
    public int[] RequestCountBuffer { get; set; }

    /// <summary>
    ///     Gets or sets the index of the buffer, which is used to determine where to store the latest data.
    /// </summary>
    public int BufferIndex { get; set; }

    /// <summary>
    ///     Gets or sets the size of the buffer, which determines the number of previous requests to store.
    /// </summary>
    public int BufferSize { get; set; }

    /// <summary>
    ///     Calculates the exponential moving average (EMA) of response times and request counts, taking into account dynamic
    ///     weights.
    /// </summary>
    /// <returns>
    ///     A tuple containing the EMA of response times, the EMA of request counts, and the calculated requests per
    ///     second.
    /// </returns>
    public (TimeSpan EmaResponseTime, double EmaRequestCount, double requestsPerSecond) CalculateEmaWithDynamicWeight()
    {
        double emaResponseTime = ResponseTimeBuffer[BufferIndex].TotalMilliseconds;
        double emaRequestCount = RequestCountBuffer[BufferIndex];
        double requestsPer = CalculateRequestsPerSecond(ResponseTimeBuffer[BufferIndex],
            RequestCountBuffer[BufferIndex]);

        for (int i = BufferSize - 1; i >= 0; i--)
        {
            int index = (BufferIndex + i) % BufferSize;
            double smoothingFactor = i / (double)BufferSize;
            TimeSpan timeSpanAtIndex = ResponseTimeBuffer[index];
            int requestCountAtIndex = RequestCountBuffer[index];

            if (requestCountAtIndex == 0 || timeSpanAtIndex == TimeSpan.Zero)
            {
                continue;
            }

            requestsPer =
                CalculateRequestsPerSecond(ResponseTimeBuffer[BufferIndex], RequestCountBuffer[BufferIndex])
                * smoothingFactor + requestsPer * (1 - smoothingFactor);

            emaResponseTime =
                timeSpanAtIndex.TotalMilliseconds * smoothingFactor + emaResponseTime * (1 - smoothingFactor);

            emaRequestCount = requestCountAtIndex * smoothingFactor + emaRequestCount * (1 - smoothingFactor);
        }

        return (TimeSpan.FromMilliseconds(emaResponseTime), emaRequestCount, requestsPer);
    }

    /// <summary>
    ///     Calculates the requests per second based on the total time and total count of requests.
    /// </summary>
    /// <param name="totalTime">The total time spent on requests.</param>
    /// <param name="totalCount">The total number of requests.</param>
    /// <returns>The calculated requests per second.</returns>
    private static double CalculateRequestsPerSecond(TimeSpan totalTime, double totalCount)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount),
                "Total count of requests must be a non-negative value.");
        }

        if (totalTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTime), "Total time must be a non-negative value.");
        }

        return totalCount / totalTime.TotalSeconds;
    }
}

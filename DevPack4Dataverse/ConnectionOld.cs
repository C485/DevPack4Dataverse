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

using DevPack4Dataverse.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

//namespace DevPack4Dataverse;
//
//public sealed class ConnectionOld : IConnection
//{
//    private readonly ILogger _logger;
//    private readonly SemaphoreSlim _semaphoreSlim;
//    private readonly Statistics _usageStatistics = new();
//
//    public ConnectionOld(ServiceClient connection, ILogger logger, int maximumConcurrentlyUsage = 1)
//    {
//        using EntryExitLogger logGuard = new(logger);
//
//        Guard.Against.NegativeOrZero(maximumConcurrentlyUsage);
//
//        _logger = Guard.Against.Null(logger);
//
//        _semaphoreSlim = new SemaphoreSlim(maximumConcurrentlyUsage, maximumConcurrentlyUsage);
//
//        PureServiceClient = Guard.Against.Null(connection);
//
//        PureServiceClient.DisableCrossThreadSafeties = true;
//
//        PureServiceClient.MaxRetryCount = 10;
//
//        PureServiceClient.RetryPauseTime = TimeSpan.FromSeconds(2);
//    }
//
//    public ServiceClient PureServiceClient { get; }
//
//    public IStatistics Statistics => _usageStatistics;
//
//    public void ApplyConnectionOptimization()
//    {
//        PureServiceClient.EnableAffinityCookie = false;
//    }
//
//    public ulong GetConnectionWeight()
//    {
//        return _usageStatistics.UsageWeightFromLastMinutes(2);
//    }
//}

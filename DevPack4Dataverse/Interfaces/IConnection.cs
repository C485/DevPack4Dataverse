﻿/*
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

using Microsoft.PowerPlatform.Dataverse.Client;

namespace DevPack4Dataverse.Interfaces;

public interface IConnection : IDataverseConnectionLayer
{
    ServiceClient PureServiceClient { get; }

    IStatistics Statistics { get; }

    void ApplyConnectionOptimalization();

    ulong GetConnectionWeight();

    void ReleaseLock();

    bool Test();

    Task<bool> TestAsync();

    bool TryLock();

    Task<bool> TryLockAsync();
}

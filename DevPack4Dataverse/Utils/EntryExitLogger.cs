///*
//Copyright 2022 Kamil Skoracki / C485@GitHub
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
//*/
//
//using Ardalis.GuardClauses;
//using Microsoft.Extensions.Logging;
//using System.Diagnostics;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//
//namespace DevPack4Dataverse.Utils;
//
//public sealed class EntryExitLogger : IDisposable
//{
//    private readonly string _callerMethod;
//    private readonly string _callerObjectName;
//    private readonly int _entrenceThreadId;
//    private readonly ILogger _logger;
//    private readonly Stopwatch _stopwatch;
//
//    public EntryExitLogger(
//        ILogger logger = null,
//        [CallerFilePath] string callerFilePath = null,
//        [CallerMemberName] string caller = null
//    )
//    {
//        _logger = Guard.Against.Null(logger);
//        _entrenceThreadId = Environment.CurrentManagedThreadId;
//        _callerObjectName = Path.GetFileNameWithoutExtension(callerFilePath);
//        _stopwatch = Stopwatch.StartNew();
//        _callerMethod = caller;
//    }
//
//    public void Dispose()
//    {
//        _stopwatch.Stop();
//        bool exceptionOccurred = Marshal.GetExceptionPointers() != IntPtr.Zero;
//        if (exceptionOccurred)
//        {
//            _logger.LogError(
//                "{LibName}: Method {MethodName} in object {ObjectName} exited with exception. Entered with thread {StartThreadId}, exited with thread {ExitThreadId}.",
//                nameof(DevPack4Dataverse),
//                _callerMethod,
//                _callerObjectName,
//                _entrenceThreadId,
//                Environment.CurrentManagedThreadId
//            );
//            _logger.LogDebug(
//                "{LibName}: Method {MethodName} in object {ObjectName} exited with exception, execution time was {TimeElapsed}.  Entered with thread {StartThreadId}, exited with thread {ExitThreadId}.",
//                nameof(DevPack4Dataverse),
//                _callerMethod,
//                _callerObjectName,
//                _stopwatch.Elapsed,
//                _entrenceThreadId,
//                Environment.CurrentManagedThreadId
//            );
//
//            return;
//        }
//        _logger.LogDebug(
//            "{LibName}: Method {MethodName} in object {ObjectName} exited successfully, execution time was {TimeElapsed}.  Entered with thread {StartThreadId}, exited with thread {ExitThreadId}.",
//            nameof(DevPack4Dataverse),
//            _callerMethod,
//            _callerObjectName,
//            _stopwatch.Elapsed,
//            _entrenceThreadId,
//            Environment.CurrentManagedThreadId
//        );
//    }
//}

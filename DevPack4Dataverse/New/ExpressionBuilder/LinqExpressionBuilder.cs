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

using System.Collections;
using System.Linq.Expressions;
using CommunityToolkit.Diagnostics;
using DevPack4Dataverse.New.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.New.ExpressionBuilder;

public static class LinqExpressionBuilder
{
    /// <summary>
    ///     Creates a instance of expression builder.<br />
    ///     Result can be used in early-bound and late-bound, on <see cref="IQueryable" /> and <see cref="IEnumerable" />
    ///     interfaces in Where method.
    /// </summary>
    /// <typeparam name="U">Can be <see cref="Entity" /> or any class that inherits from <see cref="Entity" />.</typeparam>
    /// <returns></returns>
    public static ILinqExpressionBuilder<U> Create<U>(ILogger logger) where U : Entity, new()
    {
        return new LinqExpressionBuilderInner<U>(logger);
    }

    private sealed class LinqExpressionBuilderInner<U> : ILinqExpressionBuilder<U> where U : Entity, new()
    {
        private readonly ILogger _logger;
        private readonly int _maximumExpressions = 500;

        public LinqExpressionBuilderInner(ILogger logger)
        {
            Result = ExpressionCombiner.Empty<U>(logger);
            _logger = logger;
        }

        public uint ExpressionsAdded { get; private set; }

        public Expression<Func<U, bool>> Result { get; private set; }

        public void AddAnd(Expression<Func<U, bool>> expressionToAdd)
        {
            Guard.IsNotNull(expressionToAdd);
            Guard.IsTrue(ExpressionsAdded + 1 <= _maximumExpressions,
                "Expressions added",
                $"Maximum expressions limit exceeded, limit is {_maximumExpressions}");

            Result = Result.And(expressionToAdd, _logger);
            ExpressionsAdded++;
        }

        public void AddAnd(ILinqExpressionBuilder<U> expressionToAdd)
        {
            Guard.IsNotNull(expressionToAdd);
            Guard.IsNotDefault(expressionToAdd.ExpressionsAdded);
            Guard.IsTrue(ExpressionsAdded + expressionToAdd.ExpressionsAdded <= _maximumExpressions,
                "Expressions added",
                $"Maximum expressions limit exceeded, limit is {_maximumExpressions}");

            Result = Result.And(expressionToAdd.Result, _logger);
            ExpressionsAdded += expressionToAdd.ExpressionsAdded;
        }

        public void AddOr(Expression<Func<U, bool>> expressionToAdd)
        {
            Guard.IsNotNull(expressionToAdd);
            Guard.IsTrue(ExpressionsAdded + 1 <= _maximumExpressions,
                "Expressions added",
                $"Maximum expressions limit exceeded, limit is {_maximumExpressions}");

            Result = Result.Or(expressionToAdd, _logger);
            ExpressionsAdded++;
        }

        public void AddOr(ILinqExpressionBuilder<U> expressionToAdd)
        {
            Guard.IsNotNull(expressionToAdd);
            Guard.IsNotDefault(expressionToAdd.ExpressionsAdded);
            Guard.IsTrue(ExpressionsAdded + expressionToAdd.ExpressionsAdded <= _maximumExpressions,
                "Expressions added",
                $"Maximum expressions limit exceeded, limit is {_maximumExpressions}");

            Result = Result.Or(expressionToAdd.Result, _logger);
            ExpressionsAdded += expressionToAdd.ExpressionsAdded;
        }
    }
}

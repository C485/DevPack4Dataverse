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

using Ardalis.GuardClauses;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using System.Linq.Expressions;

namespace DevPack4Dataverse.ExpressionBuilder;

public static class LinqExpressionBuilder
{
    /// <summary>
    /// Creates a instance of expression builder.<br />
    /// Result can be used in early-bound and late-bound, on <see cref="IQueryable"/> and <see cref="IEnumerable"/> interfaces in Where method.
    /// </summary>
    /// <typeparam name="U">Can be <see cref="Entity"/> or any class that inherits from <see cref="Entity"/>.</typeparam>
    /// <returns></returns>
    public static ILinqExpressionBuilder<U> Create<U>(ILogger logger) where U : Entity, new()
    {
        using EntryExitLogger logGuard = new(logger);
        return new LinqExpressionBuilderInner<U>(logger);
    }

    private sealed class LinqExpressionBuilderInner<U> : ILinqExpressionBuilder<U> where U : Entity, new()
    {
        private readonly ILogger _logger;
        private readonly int _maximumExpressions = 500;
        private Expression<Func<U, bool>> _expression;
        private uint _expressionsAdded = 0;

        public LinqExpressionBuilderInner(ILogger logger)
        {
            using EntryExitLogger logGuard = new(logger);
            _expression = ExpressionCombiner.Empty<U>(logger);
            _logger = logger;
        }

        public uint ExpressionsAdded => _expressionsAdded;
        public Expression<Func<U, bool>> Result => _expression;

        public void AddAnd(Expression<Func<U, bool>> expressionToAdd)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.Null(expressionToAdd);
            Guard.Against.AgainstExpression(
                p => p + 1 <= _maximumExpressions,
                _expressionsAdded,
                $"Maximum expressions limit exceeded, limit is {_maximumExpressions}"
            );
            _expression = _expression.And(expressionToAdd, _logger);
            _expressionsAdded++;
        }

        public void AddAnd(ILinqExpressionBuilder<U> expressionToAdd)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.Null(expressionToAdd);
            Guard.Against.Zero(expressionToAdd.ExpressionsAdded);
            Guard.Against.AgainstExpression(
                p => p + expressionToAdd.ExpressionsAdded <= _maximumExpressions,
                _expressionsAdded,
                $"Maximum expressions limit exceeded, limit is {_maximumExpressions}"
            );
            _expression = _expression.And(expressionToAdd.Result, _logger);
            _expressionsAdded += expressionToAdd.ExpressionsAdded;
        }

        public void AddOr(Expression<Func<U, bool>> expressionToAdd)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.Null(expressionToAdd);
            Guard.Against.AgainstExpression(
                p => p + 1 <= _maximumExpressions,
                _expressionsAdded,
                $"Maximum expressions limit exceeded, limit is {_maximumExpressions}"
            );
            _expression = _expression.Or(expressionToAdd, _logger);
            _expressionsAdded++;
        }

        public void AddOr(ILinqExpressionBuilder<U> expressionToAdd)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.Null(expressionToAdd);
            Guard.Against.Zero(expressionToAdd.ExpressionsAdded);
            Guard.Against.AgainstExpression(
                p => p + expressionToAdd.ExpressionsAdded <= _maximumExpressions,
                _expressionsAdded,
                $"Maximum expressions limit exceeded, limit is {_maximumExpressions}"
            );
            _expression = _expression.Or(expressionToAdd.Result, _logger);
            _expressionsAdded += expressionToAdd.ExpressionsAdded;
        }
    }
}

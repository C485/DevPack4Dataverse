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

using System.Linq.Expressions;
using Ardalis.GuardClauses;
using DevPack4Dataverse.Interfaces;
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.ExpressionBuilder;

public static class LinqExpressionBuilder
{
    /// <summary>
    /// Creates a new mutable expression builder for <typeparamref name="U"/>.
    /// </summary>
    /// <remarks>
    /// The returned builder is not thread-safe. Create one builder per query and use it from a single thread.
    /// The resulting expression is intended for the Dataverse LINQ provider, which supports a limited set of
    /// expression shapes; unsupported expressions fail when the query is translated.
    /// </remarks>
    /// <typeparam name="U">An <see cref="Entity"/> or a class derived from it.</typeparam>
    /// <param name="logger">Logger used by the builder. Cannot be null.</param>
    /// <returns>A new, empty expression builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
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
            _logger = logger;
        }

        public uint ExpressionsAdded => _expressionsAdded;

        /// <inheritdoc />
        public Expression<Func<U, bool>> Result =>
            _expression
            ?? throw new InvalidOperationException(
                "Cannot build a result from an empty expression builder. Add at least one expression or check ExpressionsAdded first."
            );

        /// <inheritdoc />
        public void AddAnd(Expression<Func<U, bool>> expressionToAdd)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.Null(expressionToAdd);

            if (TryApplyBooleanIdentity(expressionToAdd, isAnd: true, nameof(expressionToAdd)))
            {
                return;
            }

            EnsureExpressionLimit(1, nameof(expressionToAdd));
            _expression = _expressionsAdded == 0 ? expressionToAdd : _expression.And(expressionToAdd, _logger);
            _expressionsAdded++;
        }

        /// <inheritdoc />
        public void AddAnd(ILinqExpressionBuilder<U> expressionToAdd)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.Null(expressionToAdd);

            if (ReferenceEquals(expressionToAdd, this))
            {
                throw new ArgumentException("A builder cannot be added to itself.", nameof(expressionToAdd));
            }

            uint expressionsToAdd = expressionToAdd.ExpressionsAdded;
            Guard.Against.Zero(expressionsToAdd);
            Expression<Func<U, bool>> innerExpression = Guard.Against.Null(expressionToAdd.Result);
            EnsureExpressionLimit(expressionsToAdd, nameof(expressionToAdd));

            if (TryApplyBooleanIdentity(innerExpression, isAnd: true, nameof(expressionToAdd)))
            {
                return;
            }

            _expression = _expressionsAdded == 0 ? innerExpression : _expression.And(innerExpression, _logger);
            _expressionsAdded += expressionsToAdd;
        }

        /// <inheritdoc />
        public void AddOr(Expression<Func<U, bool>> expressionToAdd)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.Null(expressionToAdd);

            if (TryApplyBooleanIdentity(expressionToAdd, isAnd: false, nameof(expressionToAdd)))
            {
                return;
            }

            EnsureExpressionLimit(1, nameof(expressionToAdd));
            _expression = _expressionsAdded == 0 ? expressionToAdd : _expression.Or(expressionToAdd, _logger);
            _expressionsAdded++;
        }

        /// <inheritdoc />
        public void AddOr(ILinqExpressionBuilder<U> expressionToAdd)
        {
            using EntryExitLogger logGuard = new(_logger);
            Guard.Against.Null(expressionToAdd);

            if (ReferenceEquals(expressionToAdd, this))
            {
                throw new ArgumentException("A builder cannot be added to itself.", nameof(expressionToAdd));
            }

            uint expressionsToAdd = expressionToAdd.ExpressionsAdded;
            Guard.Against.Zero(expressionsToAdd);
            Expression<Func<U, bool>> innerExpression = Guard.Against.Null(expressionToAdd.Result);
            EnsureExpressionLimit(expressionsToAdd, nameof(expressionToAdd));

            if (TryApplyBooleanIdentity(innerExpression, isAnd: false, nameof(expressionToAdd)))
            {
                return;
            }

            _expression = _expressionsAdded == 0 ? innerExpression : _expression.Or(innerExpression, _logger);
            _expressionsAdded += expressionsToAdd;
        }

        private static bool TryApplyBooleanIdentity(
            Expression<Func<U, bool>> expression,
            bool isAnd,
            string parameterName
        )
        {
            if (!TryGetConstantBoolean(expression.Body, out bool value))
            {
                return false;
            }

            if (isAnd)
            {
                if (value)
                {
                    return true;
                }

                throw new ArgumentException(
                    "A constant false predicate cannot be added with AddAnd because the Dataverse LINQ provider cannot translate a generic always-false filter.",
                    parameterName
                );
            }

            if (!value)
            {
                return true;
            }

            throw new ArgumentException(
                "A constant true predicate cannot be added with AddOr because the Dataverse LINQ provider cannot translate a generic always-true filter.",
                parameterName
            );
        }

        private static bool TryGetConstantBoolean(Expression expression, out bool value)
        {
            bool negated = false;
            while (
                expression is UnaryExpression unaryExpression
                && (
                    unaryExpression.NodeType == ExpressionType.Convert
                    || unaryExpression.NodeType == ExpressionType.Not
                )
            )
            {
                if (unaryExpression.NodeType == ExpressionType.Not)
                {
                    negated = !negated;
                }

                expression = unaryExpression.Operand;
            }

            if (expression is ConstantExpression { Value: bool constantValue })
            {
                value = negated ? !constantValue : constantValue;
                return true;
            }

            value = false;
            return false;
        }

        private void EnsureExpressionLimit(uint expressionsToAdd, string parameterName)
        {
            if ((long)_expressionsAdded + expressionsToAdd > _maximumExpressions)
            {
                throw new ArgumentException(
                    $"Maximum expressions limit exceeded, limit is {_maximumExpressions}.",
                    parameterName
                );
            }
        }
    }
}

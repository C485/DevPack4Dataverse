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

using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace DevPack4Dataverse.ExpressionBuilder;

internal static class ExpressionCombiner
{
    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> mainExpression,
        Expression<Func<T, bool>> expressionToAdd,
        ILogger logger
    )
    {
        using EntryExitLogger logGuard = new(logger);
        ParameterExpression parameter = Expression.Parameter(typeof(T));

        ReplaceExpressionVisitor mainExpressionVisitor = new(mainExpression.Parameters[0], parameter, logger);
        Expression visitedMainExpression = mainExpressionVisitor.Visit(mainExpression.Body);

        ReplaceExpressionVisitor expressionToAddVisitor = new(expressionToAdd.Parameters[0], parameter, logger);
        Expression visitedExpressionToAdd = expressionToAddVisitor.Visit(expressionToAdd.Body);

        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(visitedMainExpression, visitedExpressionToAdd),
            parameter
        );
    }

    public static Expression<Func<T, bool>> Empty<T>(ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        return _ => false;
    }

    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> mainExpression,
        Expression<Func<T, bool>> expressionToAdd,
        ILogger logger
    )
    {
        using EntryExitLogger logGuard = new(logger);

        ParameterExpression parameter = Expression.Parameter(typeof(T));

        ReplaceExpressionVisitor mainExpressionVisitor = new(mainExpression.Parameters[0], parameter, logger);
        Expression visitedMainExpression = mainExpressionVisitor.Visit(mainExpression.Body);

        ReplaceExpressionVisitor expressionToAddVisitor = new(expressionToAdd.Parameters[0], parameter, logger);
        Expression visitedExpressionToAdd = expressionToAddVisitor.Visit(expressionToAdd.Body);

        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(visitedMainExpression, visitedExpressionToAdd),
            parameter
        );
    }

    private sealed class ReplaceExpressionVisitor : ExpressionVisitor
    {
        private readonly ILogger _logger;
        private readonly Expression _newValue;
        private readonly Expression _oldValue;

        public ReplaceExpressionVisitor(Expression oldValue, Expression newValue, ILogger logger)
        {
            using EntryExitLogger logGuard = new(logger);
            _oldValue = oldValue;
            _newValue = newValue;
            _logger = logger;
        }

        public override Expression Visit(Expression node)
        {
            using EntryExitLogger logGuard = new(_logger);
            return node == _oldValue ? _newValue : base.Visit(node);
        }
    }
}

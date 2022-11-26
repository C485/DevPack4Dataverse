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
using Microsoft.Xrm.Sdk;
using System.Linq.Expressions;

namespace DevPack4Dataverse.ExpressionBuilder;

public static class LinqExpressionBuilder
{
    public static ILinqExpressionBuilder<U> Create<U>() where U : Entity
    {
        return new LinqExpressionBuilderInner<U>();
    }

    private sealed class LinqExpressionBuilderInner<U> : ILinqExpressionBuilder<U> where U : Entity
    {
        private Expression<Func<U, bool>> _expression;

        public LinqExpressionBuilderInner()
        {
            _expression = ExpressionCombiner.Empty<U>();
        }

        public Expression<Func<U, bool>> Result => _expression;

        public void AddAnd(Expression<Func<U, bool>> expressionToAdd)
        {
            _expression = _expression.And(expressionToAdd);
        }

        public void AddAnd(ILinqExpressionBuilder<U> expressionToAdd)
        {
            _expression = _expression.And(expressionToAdd.Result);
        }

        public void AddOr(Expression<Func<U, bool>> expressionToAdd)
        {
            _expression = _expression.Or(expressionToAdd);
        }

        public void AddOr(ILinqExpressionBuilder<U> expressionToAdd)
        {
            _expression = _expression.Or(expressionToAdd.Result);
        }
    }
}
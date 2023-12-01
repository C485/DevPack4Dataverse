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
using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Interfaces;

public interface ILinqExpressionBuilder<U>
    where U : Entity, new()
{
    /// <summary>
    /// Returns how many expressions were added, counts inner expressions as well.
    /// </summary>
    uint ExpressionsAdded { get; }

    /// <summary>
    /// Final expression that can be used in Where LINQ method.
    /// </summary>
    Expression<Func<U, bool>> Result { get; }

    /// <summary>
    /// Adds <see cref="ILinqExpressionBuilder{U}"/> expression as new AND block to top of <see cref="ILinqExpressionBuilder{U}"/>.
    /// </summary>
    /// <param name="expressionToAdd">Instance of <see cref="ILinqExpressionBuilder{U}"/>, cannot be empty or null.</param>
    /// <exception cref="ArgumentNullException">Exception thrown when <paramref name="expressionToAdd"/> is null.</exception>
    /// <exception cref="ArgumentException">Exception thrown when maximum limit is exceeded or <paramref name="expressionToAdd"/> is empty.</exception>
    void AddAnd(ILinqExpressionBuilder<U> expressionToAdd);

    /// <summary>
    /// Adds <see cref="Expression"/> as new AND block to top of <see cref="ILinqExpressionBuilder{U}"/>.
    /// </summary>
    /// <param name="expressionToAdd">Instance of <see cref="Expression"/>, cannot be null.</param>
    /// <exception cref="ArgumentNullException">Exception thrown when <paramref name="expressionToAdd"/> is null.</exception>
    /// <exception cref="ArgumentException">Exception thrown when maximum limit is exceeded.</exception>
    void AddAnd(Expression<Func<U, bool>> expressionToAdd);

    /// <summary>
    /// Adds <see cref="Expression"/> as new OR block to top of <see cref="ILinqExpressionBuilder{U}"/>.
    /// </summary>
    /// <param name="expressionToAdd">Instance of <see cref="Expression"/>, cannot be null.</param>
    /// <exception cref="ArgumentNullException">Exception thrown when <paramref name="expressionToAdd"/> is null.</exception>
    /// <exception cref="ArgumentException">Exception thrown when maximum limit is exceeded.</exception>
    void AddOr(Expression<Func<U, bool>> expressionToAdd);

    /// <summary>
    /// Adds <see cref="ILinqExpressionBuilder{U}"/> expression as new OR block to top of <see cref="ILinqExpressionBuilder{U}"/>.
    /// </summary>
    /// <param name="expressionToAdd">Instance of <see cref="ILinqExpressionBuilder{U}"/>, cannot be empty or null.</param>
    /// <exception cref="ArgumentNullException">Exception thrown when <paramref name="expressionToAdd"/> is null.</exception>
    /// <exception cref="ArgumentException">Exception thrown when maximum limit is exceeded or <paramref name="expressionToAdd"/> is empty.</exception>
    void AddOr(ILinqExpressionBuilder<U> expressionToAdd);
}

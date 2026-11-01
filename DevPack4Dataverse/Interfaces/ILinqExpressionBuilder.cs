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

/// <summary>
/// Combines predicate expressions into a single predicate with AND/OR blocks.
/// </summary>
/// <remarks>
/// Builders are mutable and not thread-safe. Create one builder per query and use it from a single thread.
/// The result is intended for the Dataverse LINQ provider, which supports a limited set of expression shapes;
/// unsupported expressions fail when the query is translated.
/// </remarks>
/// <typeparam name="U">An <see cref="Entity"/> or a class derived from it.</typeparam>
public interface ILinqExpressionBuilder<U> where U : Entity, new()
{
    /// <summary>
    /// Number of real filter expressions added. A top-level constant <c>true</c> added with
    /// <c>AddAnd</c> and a top-level constant <c>false</c> added with <c>AddOr</c> are
    /// boolean identities and are not counted.
    /// </summary>
    uint ExpressionsAdded { get; }

    /// <summary>
    /// Final expression that can be used in the Where method.
    /// </summary>
    /// <exception cref="InvalidOperationException">No expressions have been added to the builder.</exception>
    Expression<Func<U, bool>> Result { get; }

    /// <summary>
    /// Adds an inner builder as a new AND block.
    /// </summary>
    /// <param name="expressionToAdd">
    /// Builder whose expressions are added. Cannot be null, empty, or the current builder.
    /// The builder is snapshotted; changes made to it after this call do not affect this builder.
    /// </param>
    /// <remarks>
    /// A top-level constant <c>true</c> is the AND identity and is ignored.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="expressionToAdd"/> is null or its <see cref="Result"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="expressionToAdd"/> is empty, is the current builder, exceeds the maximum
    /// expression count, or contains a top-level constant <c>false</c> predicate.
    /// </exception>
    void AddAnd(ILinqExpressionBuilder<U> expressionToAdd);

    /// <summary>
    /// Adds an expression as a new AND block.
    /// </summary>
    /// <param name="expressionToAdd">Expression to add. Cannot be null.</param>
    /// <remarks>
    /// A top-level constant <c>true</c> is the AND identity and is ignored.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="expressionToAdd"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The maximum expression count is exceeded, or the expression is a top-level constant <c>false</c>.
    /// </exception>
    void AddAnd(Expression<Func<U, bool>> expressionToAdd);

    /// <summary>
    /// Adds an inner builder as a new OR block.
    /// </summary>
    /// <param name="expressionToAdd">
    /// Builder whose expressions are added. Cannot be null, empty, or the current builder.
    /// The builder is snapshotted; changes made to it after this call do not affect this builder.
    /// </param>
    /// <remarks>
    /// A top-level constant <c>false</c> is the OR identity and is ignored.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="expressionToAdd"/> is null or its <see cref="Result"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="expressionToAdd"/> is empty, is the current builder, exceeds the maximum
    /// expression count, or contains a top-level constant <c>true</c> predicate.
    /// </exception>
    void AddOr(ILinqExpressionBuilder<U> expressionToAdd);

    /// <summary>
    /// Adds an expression as a new OR block.
    /// </summary>
    /// <param name="expressionToAdd">Expression to add. Cannot be null.</param>
    /// <remarks>
    /// A top-level constant <c>false</c> is the OR identity and is ignored.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="expressionToAdd"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The maximum expression count is exceeded, or the expression is a top-level constant <c>true</c>.
    /// </exception>
    void AddOr(Expression<Func<U, bool>> expressionToAdd);
}

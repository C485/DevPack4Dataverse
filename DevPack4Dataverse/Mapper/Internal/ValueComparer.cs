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

using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Mapper.Internal;

internal static class ValueComparer
{
    public static bool AreEqual(object? left, object? right)
    {
        left = Unwrap(left);
        right = Unwrap(right);

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        if (left is byte[] leftBytes && right is byte[] rightBytes)
        {
            return leftBytes.SequenceEqual(rightBytes);
        }

        if (left is EntityReference leftReference && right is EntityReference rightReference)
        {
            return string.Equals(
                    leftReference.LogicalName,
                    rightReference.LogicalName,
                    StringComparison.OrdinalIgnoreCase
                )
                && leftReference.Id == rightReference.Id;
        }

        if (left is OptionSetValue leftOption && right is OptionSetValue rightOption)
        {
            return leftOption.Value == rightOption.Value;
        }

        if (left is Money leftMoney && right is Money rightMoney)
        {
            return leftMoney.Value == rightMoney.Value;
        }

        return left.Equals(right);
    }

    private static object? Unwrap(object? value) =>
        value is AliasedValue aliased ? aliased.Value : value;
}

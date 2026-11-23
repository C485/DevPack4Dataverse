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

using System.Globalization;
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
            return AreReferencesEqual(leftReference, rightReference);
        }

        if (left is OptionSetValue leftOption && right is OptionSetValue rightOption)
        {
            return leftOption.Value == rightOption.Value;
        }

        if (left is OptionSetValue leftOptionValue && TryGetInt32(right, out int rightOptionValue))
        {
            return leftOptionValue.Value == rightOptionValue;
        }

        if (right is OptionSetValue rightOptionValue2 && TryGetInt32(left, out int leftOptionValue2))
        {
            return rightOptionValue2.Value == leftOptionValue2;
        }

        if (left is OptionSetValueCollection leftOptionCollection
            && right is OptionSetValueCollection rightOptionCollection)
        {
            // A multi-select column is a set. Dataverse may return a different order than
            // the source, so compare sorted values instead of the original sequence.
            return leftOptionCollection
                .Select(option => option.Value)
                .OrderBy(value => value)
                .SequenceEqual(
                    rightOptionCollection.Select(option => option.Value).OrderBy(value => value)
                );
        }

        if (left is Money leftMoney && right is Money rightMoney)
        {
            return leftMoney.Value == rightMoney.Value;
        }

        if (left is Money leftMoneyValue && TryGetDecimal(right, out decimal rightMoneyValue))
        {
            return leftMoneyValue.Value == rightMoneyValue;
        }

        if (right is Money rightMoneyValue2 && TryGetDecimal(left, out decimal leftMoneyValue2))
        {
            return rightMoneyValue2.Value == leftMoneyValue2;
        }

        if (left is DateTime leftDateTime && right is DateTime rightDateTime)
        {
            // Dataverse returns date-only and time-zone-independent columns with
            // Kind = Unspecified. Comparing them as UTC instants makes a UTC-kind source look
            // different on a non-UTC host, which turns every run into an update. Compare
            // wall-clock time when either side has no zone information.
            if (
                leftDateTime.Kind == DateTimeKind.Unspecified
                || rightDateTime.Kind == DateTimeKind.Unspecified
            )
            {
                return leftDateTime.Ticks == rightDateTime.Ticks;
            }

            return leftDateTime.ToUniversalTime().Ticks == rightDateTime.ToUniversalTime().Ticks;
        }

        if (left is DateTimeOffset leftOffset && right is DateTimeOffset rightOffset)
        {
            return leftOffset.UtcDateTime.Ticks == rightOffset.UtcDateTime.Ticks;
        }

        if (left is DateTimeOffset leftOffsetValue && right is DateTime rightDateTimeValue)
        {
            return leftOffsetValue.UtcDateTime.Ticks == rightDateTimeValue.ToUniversalTime().Ticks;
        }

        if (left is DateTime leftDateTimeValue && right is DateTimeOffset rightOffsetValue)
        {
            return leftDateTimeValue.ToUniversalTime().Ticks == rightOffsetValue.UtcDateTime.Ticks;
        }

        if (
            IsNumeric(left)
            && IsNumeric(right)
            && TryGetDecimal(left, out decimal leftNumber)
            && TryGetDecimal(right, out decimal rightNumber)
        )
        {
            return leftNumber == rightNumber;
        }

        return left.Equals(right);
    }

    private static bool AreReferencesEqual(EntityReference left, EntityReference right)
    {
        if (
            !string.Equals(
                left.LogicalName,
                right.LogicalName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        if (left.Id != Guid.Empty || right.Id != Guid.Empty)
        {
            return left.Id == right.Id;
        }

        int leftKeyCount = left.KeyAttributes?.Count ?? 0;
        int rightKeyCount = right.KeyAttributes?.Count ?? 0;
        if (leftKeyCount == 0 && rightKeyCount == 0)
        {
            return true;
        }

        if (leftKeyCount == 0 || rightKeyCount == 0)
        {
            return false;
        }

        EntityKey leftKey = EntityKey.ByAlternateKey(left.LogicalName, left.KeyAttributes!);
        EntityKey rightKey = EntityKey.ByAlternateKey(right.LogicalName, right.KeyAttributes!);
        return leftKey.Equals(rightKey);
    }

    private static bool IsNumeric(object value) =>
        value is byte
            or sbyte
            or short
            or ushort
            or int
            or uint
            or long
            or ulong
            or float
            or double
            or decimal;

    private static bool TryGetDecimal(object value, out decimal result)
    {
        if (value is Money money)
        {
            result = money.Value;
            return true;
        }

        if (value is decimal decimalValue)
        {
            result = decimalValue;
            return true;
        }

        if (!IsNumeric(value))
        {
            result = 0;
            return false;
        }

        try
        {
            result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidCastException or FormatException or OverflowException
        )
        {
            result = 0;
            return false;
        }
    }

    private static bool TryGetInt32(object value, out int result)
    {
        if (!IsNumeric(value))
        {
            result = 0;
            return false;
        }

        try
        {
            result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidCastException or FormatException or OverflowException
        )
        {
            result = 0;
            return false;
        }
    }

    private static object? Unwrap(object? value) =>
        value is AliasedValue aliased ? aliased.Value : value;
}

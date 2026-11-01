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
using DevPack4Dataverse.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DevPack4Dataverse.FieldMethods;

/// <summary>
/// Helpers for Dataverse Money (currency) fields and transaction-currency conversion.
/// </summary>
/// <remarks>
/// Dataverse stores the transaction amount in a Money field and the base amount in the read-only
/// <c>&lt;field&gt;_base</c> Money field. The base value is calculated as
/// <c>transaction amount / exchange rate</c>. <see cref="SetValue"/> sets only the transaction
/// field; Dataverse calculates the base field when the record is saved.
/// </remarks>
public sealed class MoneyFieldTypeMethods
{
    private const string BaseFieldSuffix = "_base";
    private const string ExchangeRateAttribute = "exchangerate";
    private const string TransactionCurrencyAttribute = "transactioncurrencyid";
    private const string TransactionCurrencyLogicalName = "transactioncurrency";

    private readonly ILogger _logger;
    private readonly SdkProxy _sdkProxy;

    /// <summary>
    /// Creates a new Money field helper.
    /// </summary>
    /// <param name="sdkProxy">Connection layer used to retrieve missing exchange rates. Cannot be null.</param>
    /// <param name="logger">Logger used by the helper. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public MoneyFieldTypeMethods(SdkProxy sdkProxy, ILogger logger)
    {
        using EntryExitLogger logGuard = new(logger);
        _sdkProxy = Guard.Against.Null(sdkProxy);
        _logger = Guard.Against.Null(logger);
    }

    /// <summary>
    /// Reads the transaction amount from a Money field.
    /// </summary>
    /// <param name="entity">Entity containing the field. Cannot be null.</param>
    /// <param name="fieldName">Logical name of the Money field. Cannot be null or whitespace.</param>
    /// <returns>The transaction amount, or null when the attribute is present with a null value.</returns>
    /// <exception cref="ArgumentException">The entity does not contain the field, or the field name is invalid.</exception>
    /// <exception cref="InvalidOperationException">The attribute is not a Money or decimal value.</exception>
    public decimal? GetValue(Entity entity, string fieldName)
    {
        using EntryExitLogger logGuard = new(_logger);
        return ReadMoneyValue(GetPresentAttribute(entity, fieldName), fieldName);
    }

    /// <summary>
    /// Reads the base-currency amount from the read-only <c>&lt;field&gt;_base</c> field.
    /// </summary>
    /// <param name="entity">Entity containing the field. Cannot be null.</param>
    /// <param name="fieldName">Logical name of the transaction Money field, without the <c>_base</c> suffix.</param>
    /// <returns>The base amount, or null when the base attribute is present with a null value.</returns>
    /// <exception cref="ArgumentException">The entity does not contain the base field, or the field name is invalid.</exception>
    /// <exception cref="InvalidOperationException">The attribute is not a Money or decimal value.</exception>
    public decimal? GetBaseValue(Entity entity, string fieldName)
    {
        using EntryExitLogger logGuard = new(_logger);
        Guard.Against.NullOrWhiteSpace(fieldName, nameof(fieldName));
        return GetValue(entity, $"{fieldName}{BaseFieldSuffix}");
    }

    /// <summary>
    /// Sets a Money field to <paramref name="value"/>. The base field is calculated by Dataverse.
    /// </summary>
    /// <param name="entity">Entity to update. Cannot be null.</param>
    /// <param name="fieldName">Logical name of the Money field. Cannot be null or whitespace.</param>
    /// <param name="value">Transaction amount.</param>
    /// <returns>The same entity for chaining.</returns>
    public Entity SetValue(Entity entity, string fieldName, decimal value)
    {
        using EntryExitLogger logGuard = new(_logger);
        Guard.Against.Null(entity, nameof(entity));
        Guard.Against.NullOrWhiteSpace(fieldName, nameof(fieldName));
        entity[fieldName] = new Money(value);
        return entity;
    }

    /// <summary>
    /// Sets a Money field to <paramref name="value"/>. The base field is calculated by Dataverse.
    /// </summary>
    /// <param name="entity">Entity to update. Cannot be null.</param>
    /// <param name="fieldName">Logical name of the Money field. Cannot be null or whitespace.</param>
    /// <param name="value">Money value. Cannot be null.</param>
    /// <returns>The same entity for chaining.</returns>
    public Entity SetValue(Entity entity, string fieldName, Money value)
    {
        using EntryExitLogger logGuard = new(_logger);
        Guard.Against.Null(entity, nameof(entity));
        Guard.Against.NullOrWhiteSpace(fieldName, nameof(fieldName));
        entity[fieldName] = Guard.Against.Null(value);
        return entity;
    }

    /// <summary>
    /// Sets the transaction currency lookup on the entity.
    /// </summary>
    /// <param name="entity">Entity to update. Cannot be null.</param>
    /// <param name="transactionCurrency">Transaction currency reference. Cannot be null and must have a non-empty ID.</param>
    /// <returns>The same entity for chaining.</returns>
    public Entity SetTransactionCurrency(Entity entity, EntityReference transactionCurrency)
    {
        using EntryExitLogger logGuard = new(_logger);
        Guard.Against.Null(entity, nameof(entity));
        string entityName = GetCurrencyEntityName(transactionCurrency);
        EntityReference currency = string.IsNullOrWhiteSpace(transactionCurrency.LogicalName)
            ? new EntityReference(entityName, transactionCurrency.Id)
            : transactionCurrency;

        entity[TransactionCurrencyAttribute] = currency;
        return entity;
    }

    /// <summary>
    /// Reads the transaction currency lookup from the entity.
    /// </summary>
    /// <param name="entity">Entity containing the lookup. Cannot be null.</param>
    /// <returns>The transaction currency reference.</returns>
    /// <exception cref="ArgumentException">The entity does not contain the lookup.</exception>
    /// <exception cref="InvalidOperationException">The lookup is not an EntityReference or Guid.</exception>
    public EntityReference GetTransactionCurrency(Entity entity)
    {
        using EntryExitLogger logGuard = new(_logger);
        object rawValue = UnwrapAliased(GetPresentAttribute(entity, TransactionCurrencyAttribute));

        if (rawValue is EntityReference reference && reference.Id != Guid.Empty)
        {
            return reference;
        }

        if (rawValue is Guid id && id != Guid.Empty)
        {
            return new EntityReference(TransactionCurrencyLogicalName, id);
        }

        throw new InvalidOperationException(
            $"Field '{TransactionCurrencyAttribute}' is not a valid transaction currency reference."
        );
    }

    /// <summary>
    /// Gets the applicable exchange rate for the entity. Uses the entity's <c>exchangerate</c>
    /// when present, otherwise retrieves it from the transaction currency record.
    /// </summary>
    /// <param name="entity">Entity with a transaction currency or exchange rate. Cannot be null.</param>
    /// <returns>Exchange rate greater than zero.</returns>
    /// <exception cref="InvalidOperationException">
    /// The exchange rate is missing/invalid or the transaction currency is not available.
    /// </exception>
    public decimal GetExchangeRate(Entity entity)
    {
        using EntryExitLogger logGuard = new(_logger);
        Guard.Against.Null(entity, nameof(entity));

        if (entity.Contains(ExchangeRateAttribute))
        {
            return ReadExchangeRate(entity, ExchangeRateAttribute);
        }

        return GetExchangeRate(GetTransactionCurrency(entity));
    }

    /// <summary>
    /// Retrieves the exchange rate for a transaction currency.
    /// </summary>
    /// <param name="transactionCurrency">Transaction currency reference. Cannot be null and must have a non-empty ID.</param>
    /// <returns>Exchange rate greater than zero.</returns>
    public decimal GetExchangeRate(EntityReference transactionCurrency)
    {
        using EntryExitLogger logGuard = new(_logger);
        string entityName = GetCurrencyEntityName(transactionCurrency);
        Entity currency = Guard.Against.Null(
            _sdkProxy.Retrieve(
                entityName,
                transactionCurrency.Id,
                new ColumnSet(ExchangeRateAttribute)
            ),
            message: "The transaction currency record could not be retrieved."
        );

        return ReadExchangeRate(currency, ExchangeRateAttribute);
    }

    /// <summary>
    /// Asynchronously gets the applicable exchange rate for the entity.
    /// </summary>
    /// <param name="entity">Entity with a transaction currency or exchange rate. Cannot be null.</param>
    /// <param name="cancellationToken">Token checked before and after the currency retrieval.</param>
    /// <returns>Exchange rate greater than zero.</returns>
    public async Task<decimal> GetExchangeRateAsync(
        Entity entity,
        CancellationToken cancellationToken = default
    )
    {
        using EntryExitLogger logGuard = new(_logger);
        Guard.Against.Null(entity, nameof(entity));

        if (entity.Contains(ExchangeRateAttribute))
        {
            return ReadExchangeRate(entity, ExchangeRateAttribute);
        }

        return await GetExchangeRateAsync(GetTransactionCurrency(entity), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously retrieves the exchange rate for a transaction currency.
    /// </summary>
    /// <param name="transactionCurrency">Transaction currency reference. Cannot be null and must have a non-empty ID.</param>
    /// <param name="cancellationToken">Token checked before and after the currency retrieval.</param>
    /// <returns>Exchange rate greater than zero.</returns>
    public async Task<decimal> GetExchangeRateAsync(
        EntityReference transactionCurrency,
        CancellationToken cancellationToken = default
    )
    {
        using EntryExitLogger logGuard = new(_logger);
        string entityName = GetCurrencyEntityName(transactionCurrency);
        cancellationToken.ThrowIfCancellationRequested();

        Entity currency = Guard.Against.Null(
            await _sdkProxy
                .RetrieveAsync(
                    entityName,
                    transactionCurrency.Id,
                    new ColumnSet(ExchangeRateAttribute)
                )
                .ConfigureAwait(false),
            message: "The transaction currency record could not be retrieved."
        );

        cancellationToken.ThrowIfCancellationRequested();
        return ReadExchangeRate(currency, ExchangeRateAttribute);
    }

    /// <summary>
    /// Converts a transaction-currency amount to the base currency.
    /// </summary>
    /// <param name="transactionValue">Amount in the transaction currency.</param>
    /// <param name="exchangeRate">Positive exchange rate.</param>
    /// <returns>Amount in the base currency.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchangeRate"/> is not positive.</exception>
    public decimal ConvertToBase(decimal transactionValue, decimal exchangeRate)
    {
        Guard.Against.Expression(
            p => p <= 0m,
            exchangeRate,
            "Exchange rate must be greater than zero.",
            nameof(exchangeRate)
        );
        return transactionValue / exchangeRate;
    }

    /// <summary>
    /// Converts a base-currency amount to the transaction currency.
    /// </summary>
    /// <param name="baseValue">Amount in the base currency.</param>
    /// <param name="exchangeRate">Positive exchange rate.</param>
    /// <returns>Amount in the transaction currency.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchangeRate"/> is not positive.</exception>
    public decimal ConvertFromBase(decimal baseValue, decimal exchangeRate)
    {
        Guard.Against.Expression(
            p => p <= 0m,
            exchangeRate,
            "Exchange rate must be greater than zero.",
            nameof(exchangeRate)
        );
        return baseValue * exchangeRate;
    }

    /// <summary>
    /// Reads the formatted value (for example, a currency-formatted string) of a field.
    /// </summary>
    /// <param name="entity">Entity containing formatted values. Cannot be null.</param>
    /// <param name="fieldName">Logical field name. Cannot be null or whitespace.</param>
    /// <returns>The formatted value.</returns>
    /// <exception cref="ArgumentException">The entity does not contain a formatted value for the field.</exception>
    public string GetFormattedValue(Entity entity, string fieldName)
    {
        using EntryExitLogger logGuard = new(_logger);
        Guard.Against.Null(entity, nameof(entity));
        Guard.Against.NullOrWhiteSpace(fieldName, nameof(fieldName));

        if (!entity.FormattedValues.Contains(fieldName))
        {
            throw new ArgumentException(
                $"Entity does not contain a formatted value for field '{fieldName}'.",
                nameof(fieldName)
            );
        }

        return entity.FormattedValues[fieldName];
    }

    private static object GetPresentAttribute(Entity entity, string fieldName)
    {
        Guard.Against.Null(entity, nameof(entity));
        Guard.Against.NullOrWhiteSpace(fieldName, nameof(fieldName));

        if (!entity.Contains(fieldName))
        {
            throw new ArgumentException(
                $"Entity does not contain field '{fieldName}'.",
                nameof(fieldName)
            );
        }

        return entity[fieldName];
    }

    private static decimal? ReadMoneyValue(object rawValue, string fieldName)
    {
        object value = UnwrapAliased(rawValue);
        return value switch
        {
            null => null,
            Money money => money.Value,
            decimal decimalValue => decimalValue,
            _ => throw new InvalidOperationException(
                $"Field '{fieldName}' is {value.GetType().Name}, not a Money or decimal value."
            ),
        };
    }

    private static decimal ReadExchangeRate(Entity entity, string fieldName)
    {
        object value = UnwrapAliased(GetPresentAttribute(entity, fieldName));
        decimal exchangeRate = value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue),
            int intValue => intValue,
            _ => throw new InvalidOperationException(
                $"Field '{fieldName}' is not a numeric exchange rate."
            ),
        };

        if (exchangeRate <= 0m)
        {
            throw new InvalidOperationException("Exchange rate must be greater than zero.");
        }

        return exchangeRate;
    }

    private static object UnwrapAliased(object value) =>
        value is AliasedValue aliasedValue ? aliasedValue.Value : value;

    private static string GetCurrencyEntityName(EntityReference transactionCurrency)
    {
        Guard.Against.Null(transactionCurrency, nameof(transactionCurrency));
        Guard.Against.Default(transactionCurrency.Id, nameof(transactionCurrency));
        return string.IsNullOrWhiteSpace(transactionCurrency.LogicalName)
            ? TransactionCurrencyLogicalName
            : transactionCurrency.LogicalName;
    }
}

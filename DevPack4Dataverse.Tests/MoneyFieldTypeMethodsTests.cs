using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Xunit;
using DevPack4Dataverse.ExecuteMultiple;
using DevPack4Dataverse.FieldMethods;
using DevPack4Dataverse.Tests.TestDoubles;

namespace DevPack4Dataverse.Tests;

/// <summary>
/// Black-box tests for Money field helpers.
/// </summary>
public class MoneyFieldTypeMethodsTests
{
    private static (SdkProxy Proxy, MoneyFieldTypeMethods Methods) Create(
        Func<string, Guid, ColumnSet, Entity> retrieve = null
    )
    {
        var connection = new FakeConnection { RetrieveHandler = retrieve };
        var proxy = new SdkProxy(NullLogger.Instance, false, new FakeConnectionCreator(connection));
        return (proxy, new MoneyFieldTypeMethods(proxy, NullLogger.Instance));
    }

    [Fact]
    public void GetValue_ReadsMoneyDecimalAliasedAndNullValues()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account")
        {
            ["money"] = new Money(12.5m),
            ["decimal"] = 7.25m,
            ["alias"] = new AliasedValue("account", "money", new Money(3.5m)),
            ["empty"] = null,
        };

        using (proxy)
        {
            Assert.Equal(12.5m, methods.GetValue(entity, "money"));
            Assert.Equal(7.25m, methods.GetValue(entity, "decimal"));
            Assert.Equal(3.5m, methods.GetValue(entity, "alias"));
            Assert.Null(methods.GetValue(entity, "empty"));
        }
    }

    [Fact]
    public void GetValue_MissingOrInvalidField_Throws()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account") { ["text"] = "not money" };

        using (proxy)
        {
            Assert.Throws<ArgumentException>(() => methods.GetValue(entity, "missing"));
            Assert.Throws<InvalidOperationException>(() => methods.GetValue(entity, "text"));
            Assert.ThrowsAny<ArgumentException>(() => methods.GetValue(entity, " "));
        }
    }

    [Fact]
    public void GetBaseValue_ReadsBaseField()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account")
        {
            ["creditlimit"] = new Money(100m),
            ["creditlimit_base"] = new Money(80m),
        };

        using (proxy)
        {
            Assert.Equal(80m, methods.GetBaseValue(entity, "creditlimit"));
        }
    }

    [Fact]
    public void GetBaseValue_MissingBaseField_Throws()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account") { ["creditlimit"] = new Money(100m) };

        using (proxy)
        {
            Assert.Throws<ArgumentException>(() => methods.GetBaseValue(entity, "creditlimit"));
        }
    }

    [Fact]
    public void SetValue_SetsMoneyFieldAndReturnsEntity()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account");

        using (proxy)
        {
            Entity returned = methods.SetValue(entity, "creditlimit", 1500m);

            Assert.Same(entity, returned);
            Assert.IsType<Money>(entity["creditlimit"]);
            Assert.Equal(1500m, ((Money)entity["creditlimit"]).Value);
            Assert.False(entity.Contains("creditlimit_base"));
        }
    }

    [Fact]
    public void SetValue_MoneyOverload_SetsMoneyField()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account");
        var money = new Money(99.95m);

        using (proxy)
        {
            methods.SetValue(entity, "creditlimit", money);

            Assert.Same(money, entity["creditlimit"]);
        }
    }

    [Fact]
    public void ConvertToBaseAndFromBase_UseExchangeRate()
    {
        var (proxy, methods) = Create();

        using (proxy)
        {
            Assert.Equal(50m, methods.ConvertToBase(100m, 2m));
            Assert.Equal(100m, methods.ConvertFromBase(50m, 2m));
            Assert.ThrowsAny<ArgumentException>(() => methods.ConvertToBase(100m, 0m));
            Assert.ThrowsAny<ArgumentException>(() => methods.ConvertFromBase(100m, -1m));
        }
    }

    [Fact]
    public void GetTransactionCurrency_ReadsEntityReferenceOrGuid()
    {
        var (proxy, methods) = Create();
        var currencyId = Guid.NewGuid();
        var currencyReference = new EntityReference("transactioncurrency", currencyId);
        var entity = new Entity("account")
        {
            ["transactioncurrencyid"] = currencyReference,
        };
        var guidEntity = new Entity("account") { ["transactioncurrencyid"] = currencyId };

        using (proxy)
        {
            Assert.Same(currencyReference, methods.GetTransactionCurrency(entity));
            Assert.Equal(currencyId, methods.GetTransactionCurrency(guidEntity).Id);
            Assert.Equal(
                "transactioncurrency",
                methods.GetTransactionCurrency(guidEntity).LogicalName
            );
        }
    }

    [Fact]
    public void GetTransactionCurrency_MissingOrInvalid_Throws()
    {
        var (proxy, methods) = Create();
        var missing = new Entity("account");
        var invalid = new Entity("account") { ["transactioncurrencyid"] = "bad" };

        using (proxy)
        {
            Assert.Throws<ArgumentException>(() => methods.GetTransactionCurrency(missing));
            Assert.Throws<InvalidOperationException>(
                () => methods.GetTransactionCurrency(invalid)
            );
        }
    }

    [Fact]
    public void SetTransactionCurrency_SetsLookupAndReturnsEntity()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account");
        var currencyId = Guid.NewGuid();
        var currencyReference = new EntityReference("transactioncurrency", currencyId);

        using (proxy)
        {
            Entity returned = methods.SetTransactionCurrency(entity, currencyReference);

            Assert.Same(entity, returned);
            Assert.Same(currencyReference, entity["transactioncurrencyid"]);
        }
    }

    [Fact]
    public void GetExchangeRate_UsesEntityRateWhenPresent()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account") { ["exchangerate"] = 2.5m };

        using (proxy)
        {
            Assert.Equal(2.5m, methods.GetExchangeRate(entity));
            Assert.Equal(2.5m, methods.GetExchangeRateAsync(entity).GetAwaiter().GetResult());
        }
    }

    [Fact]
    public void GetExchangeRate_InvalidEntityRate_Throws()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account") { ["exchangerate"] = 0m };

        using (proxy)
        {
            Assert.Throws<InvalidOperationException>(() => methods.GetExchangeRate(entity));
        }
    }

    [Fact]
    public void GetExchangeRate_MissingRateAndCurrency_Throws()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account");

        using (proxy)
        {
            Assert.Throws<ArgumentException>(() => methods.GetExchangeRate(entity));
        }
    }

    [Fact]
    public void GetExchangeRate_RetrievesTransactionCurrencyRecord()
    {
        var currencyId = Guid.NewGuid();
        var currencyRecord = new Entity("transactioncurrency")
        {
            Id = currencyId,
            ["exchangerate"] = 1.75m,
        };
        var (proxy, methods) = Create(
            (entityName, id, columns) =>
            {
                Assert.Equal("transactioncurrency", entityName);
                Assert.Equal(currencyId, id);
                Assert.Contains("exchangerate", columns.Columns);
                return currencyRecord;
            }
        );
        var entity = new Entity("account")
        {
            ["transactioncurrencyid"] = new EntityReference("transactioncurrency", currencyId),
        };

        using (proxy)
        {
            Assert.Equal(1.75m, methods.GetExchangeRate(entity));
        }
    }

    [Fact]
    public async Task GetExchangeRateAsync_RetrievesTransactionCurrencyRecord()
    {
        var currencyId = Guid.NewGuid();
        var currencyRecord = new Entity("transactioncurrency")
        {
            Id = currencyId,
            ["exchangerate"] = 1.25m,
        };
        var (proxy, methods) = Create((_, _, _) => currencyRecord);
        var entity = new Entity("account")
        {
            ["transactioncurrencyid"] = new EntityReference("transactioncurrency", currencyId),
        };

        using (proxy)
        {
            Assert.Equal(1.25m, await methods.GetExchangeRateAsync(entity));
        }
    }

    [Fact]
    public async Task GetExchangeRateAsync_CanBeCanceled()
    {
        var (proxy, methods) = Create((_, _, _) => throw new InvalidOperationException());
        var entity = new Entity("account")
        {
            ["transactioncurrencyid"] = new EntityReference(
                "transactioncurrency",
                Guid.NewGuid()
            ),
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        using (proxy)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => methods.GetExchangeRateAsync(entity, cancellationTokenSource.Token)
            );
        }
    }

    [Fact]
    public void GetFormattedValue_ReturnsFormattedMoney()
    {
        var (proxy, methods) = Create();
        var entity = new Entity("account");
        entity.FormattedValues["creditlimit"] = "$1,500.00";

        using (proxy)
        {
            Assert.Equal("$1,500.00", methods.GetFormattedValue(entity, "creditlimit"));
            Assert.ThrowsAny<ArgumentException>(
                () => methods.GetFormattedValue(entity, "missing")
            );
        }
    }

    [Fact]
    public void FieldMethodsPack_ExposesMoneyMethods()
    {
        using var proxy = new SdkProxy(NullLogger.Instance, false);
        var pack = new FieldMethodsPack(
            proxy,
            new ExecuteMultipleLogic(proxy, NullLogger.Instance),
            NullLogger.Instance
        );

        Assert.NotNull(pack.MoneyFieldTypeMethods);
    }
}

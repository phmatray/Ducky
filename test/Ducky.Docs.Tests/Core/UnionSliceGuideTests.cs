using Ducky.Testing;

namespace Ducky.Docs.Tests.Core;

// The snippets of docs/guides/core/unions.md (#323 acceptance 5, closing #313 acceptance 1).
public sealed partial class UnionSliceGuideTests
{
    // A union slice and the types around it are partial, for its generated RegisterCases. Public: the generated
    // registration adds the slice to every store, and PersistenceGuideTests' UseJson context lists its state.
    public static partial class App
    {
        #region union-slice
        public sealed record Deposit(int Amount);

        public sealed record Withdraw(int Amount);

        public sealed record CloseAccount;

        public union AccountAction(Deposit, Withdraw, CloseAccount);

        public sealed record Account(int Balance, bool Closed);

        // partial: the generator adds one On<Case> per case of the union, each calling Reduce.
        public sealed partial class AccountSlice : Slice<Account, AccountAction>
        {
            protected override Account Initial => new(0, false);

            // One exhaustive switch: a case missing here is a CS8509 warning, an error with warnings as errors.
            protected override Account Reduce(Account state, AccountAction action) => action switch
            {
                Deposit d => state with { Balance = state.Balance + d.Amount },
                Withdraw w => state with { Balance = state.Balance - w.Amount },
                CloseAccount => state with { Closed = true },
            };
        }
        #endregion
    }

    [Fact]
    public async Task UnionSlice_ReducesACaseAndTheUnionValueThroughOneReduce()
    {
        #region union-slice-test
        await using var store = TestStore.Create(d => d.AddSlice<App.AccountSlice>());

        await store.DispatchAsync(new App.Deposit(50)); // a case
        await store.DispatchAsync(new App.AccountAction(new App.Withdraw(20))); // the union value: unwrapped to its case
        await store.DispatchAsync(new App.CloseAccount());
        await store.Settled();

        store.State.Get<App.Account>().ShouldBe(new App.Account(30, true));
        #endregion
    }
}

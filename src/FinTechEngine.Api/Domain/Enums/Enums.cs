namespace FinTechEngine.Api.Domain.Enums;

public enum TransactionType
{
    Deposit,
    Withdrawal,
    Transfer
}

public enum TransactionStatus
{
    Pending,
    Completed,
    Failed,
    RolledBack
}

public enum EntryDirection
{
    Debit,
    Credit
}

public enum AccountType
{
    Customer,
    SystemVault,
    Reserve
}

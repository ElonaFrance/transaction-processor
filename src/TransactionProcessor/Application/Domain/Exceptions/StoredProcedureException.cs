namespace TransactionProcessor.Application.Domain.Exceptions;

public class StoredProcedureException : Exception
{
    public int ErrorCode { get; }

    public StoredProcedureException(string procedureName, int errorCode, string? errorMessage)
        : base($"{procedureName} failed: [{errorCode}] {errorMessage}")
    {
        ErrorCode = errorCode;
    }
}

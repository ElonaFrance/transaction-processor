using FluentAssertions;
using NSubstitute;
using Xunit;
using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Domain.Exceptions;
using TransactionProcessor.Application.Repositories.Contracts;
using TransactionProcessor.Application.Services;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Tests.Services;

public class TransactionProcessingServiceTests
{
    private readonly ICategoryService _categoryService = Substitute.For<ICategoryService>();
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private TransactionProcessingService CreateSut() =>
        new(_categoryService, _transactionRepository, _logger);

    [Fact]
    public async Task ProcessBatchAsync_ShouldPersistEachTransaction()
    {
        _categoryService.Categorise(Arg.Any<TransactionEvent>()).Returns((1, "Groceries"));

        var events = new List<TransactionEvent>
        {
            new() { TransactionId = Guid.NewGuid().ToString(), ClientId = "CLIENT-001", Amount = 250, TransactionType = "Debit", TransactionDate = DateTime.UtcNow },
            new() { TransactionId = Guid.NewGuid().ToString(), ClientId = "CLIENT-002", Amount = 1500, TransactionType = "Credit", TransactionDate = DateTime.UtcNow }
        };

        var failed = await CreateSut().ProcessBatchAsync(events, CancellationToken.None);

        failed.Should().BeEmpty();
        await _transactionRepository.Received(2).InsertAsync(Arg.Any<Application.Domain.Entities.TransactionEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenOneFailsPermanently_ShouldReturnItAndContinueProcessingRest()
    {
        _categoryService.Categorise(Arg.Any<TransactionEvent>()).Returns((1, "Groceries"));
        _transactionRepository
            .InsertAsync(Arg.Is<Application.Domain.Entities.TransactionEntity>(t => t.ClientId == "CLIENT-FAIL"), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("DB error")));

        var events = new List<TransactionEvent>
        {
            new() { TransactionId = Guid.NewGuid().ToString(), ClientId = "CLIENT-FAIL", Amount = 100, TransactionType = "Debit", TransactionDate = DateTime.UtcNow },
            new() { TransactionId = Guid.NewGuid().ToString(), ClientId = "CLIENT-001", Amount = 200, TransactionType = "Debit", TransactionDate = DateTime.UtcNow }
        };

        var failed = await CreateSut().ProcessBatchAsync(events, CancellationToken.None);

        failed.Should().ContainSingle().Which.ClientId.Should().Be("CLIENT-FAIL");
        await _transactionRepository.Received(2).InsertAsync(Arg.Any<Application.Domain.Entities.TransactionEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenTransientErrorOutlastsRetries_ShouldThrowInsteadOfDeadLettering()
    {
        _categoryService.Categorise(Arg.Any<TransactionEvent>()).Returns((1, "Groceries"));
        _transactionRepository
            .InsertAsync(Arg.Any<Application.Domain.Entities.TransactionEntity>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new StoredProcedureException("usp_InsertTransaction", 1205, "Lock wait timeout")));

        var events = new List<TransactionEvent>
        {
            new() { TransactionId = Guid.NewGuid().ToString(), ClientId = "CLIENT-001", Amount = 100, TransactionType = "Debit", TransactionDate = DateTime.UtcNow }
        };

        var act = async () => await CreateSut().ProcessBatchAsync(events, CancellationToken.None);

        await act.Should().ThrowAsync<StoredProcedureException>();
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenTransactionIdMissing_ShouldFailWithoutPersisting()
    {
        var events = new List<TransactionEvent>
        {
            new() { TransactionId = "", ClientId = "CLIENT-001", Amount = 100, TransactionType = "Debit", TransactionDate = DateTime.UtcNow }
        };

        var failed = await CreateSut().ProcessBatchAsync(events, CancellationToken.None);

        failed.Should().ContainSingle();
        await _transactionRepository.DidNotReceive().InsertAsync(Arg.Any<Application.Domain.Entities.TransactionEntity>(), Arg.Any<CancellationToken>());
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Application.Services;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Integration;

/// <summary>
/// IdempotentCommandExecutor sözleşmesi: aynı Idempotency-Key tekrar geldiğinde action bir daha
/// çalışmamalı, önceki response aynen dönmeli (custom status code dâhil), bozuk cache güvenli
/// fallback'e düşmeli.
/// </summary>
public sealed class IdempotentCommandExecutorTests : IAsyncDisposable
{
    private readonly List<SqliteConnection> _connections = new();

    private AppDbContext CreateDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        _connections.Add(conn);

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static (IdempotentCommandExecutor exec, CommandExecutionService cmd) CreateSut(AppDbContext db)
    {
        var cmd = new CommandExecutionService(db);
        var exec = new IdempotentCommandExecutor(cmd);
        return (exec, cmd);
    }

    private static int StatusOf(IResult result) =>
        result is IStatusCodeHttpResult sc && sc.StatusCode is int code ? code : 200;

    private static object? ValueOf(IResult result) =>
        result is IValueHttpResult vr ? vr.Value : null;

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _connections)
        {
            await c.DisposeAsync();
        }
    }

    private sealed record Payload(string Message, int Code);

    [Fact]
    public async Task Execute_WithoutKey_RunsActionEveryTime()
    {
        await using var db = CreateDb();
        var (sut, _) = CreateSut(db);
        var ctx = new DefaultHttpContext();
        var calls = 0;

        Task<Payload> Action(CancellationToken ct)
        {
            calls++;
            return Task.FromResult(new Payload("ok", calls));
        }

        var r1 = await sut.ExecuteAsync(ctx, userId: 1, "op", idempotencyKey: null,
            Action, p => Results.Ok(p), NullLogger.Instance, CancellationToken.None);
        var r2 = await sut.ExecuteAsync(ctx, userId: 1, "op", idempotencyKey: null,
            Action, p => Results.Ok(p), NullLogger.Instance, CancellationToken.None);

        calls.Should().Be(2);
        StatusOf(r1).Should().Be(StatusCodes.Status200OK);
        StatusOf(r2).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Replay_WithSameKey_DoesNotRunActionAgain_AndReturns200()
    {
        await using var db = CreateDb();
        var (sut, _) = CreateSut(db);
        var ctx = new DefaultHttpContext();
        var calls = 0;

        Task<Payload> Action(CancellationToken ct)
        {
            calls++;
            return Task.FromResult(new Payload("hello", 42));
        }

        var first = await sut.ExecuteAsync(ctx, userId: 7, "demo.op", "key-A",
            Action, p => Results.Ok(p), NullLogger.Instance, CancellationToken.None);
        var second = await sut.ExecuteAsync(ctx, userId: 7, "demo.op", "key-A",
            Action, p => Results.Ok(p), NullLogger.Instance, CancellationToken.None);

        calls.Should().Be(1);
        StatusOf(first).Should().Be(StatusCodes.Status200OK);
        StatusOf(second).Should().Be(StatusCodes.Status200OK);

        ValueOf(first).Should().BeOfType<Payload>().Which.Should().BeEquivalentTo(new Payload("hello", 42));
        ValueOf(second).Should().BeOfType<Payload>().Which.Should().BeEquivalentTo(new Payload("hello", 42));
    }

    [Fact]
    public async Task Replay_PreservesCustomStatusCode_422()
    {
        await using var db = CreateDb();
        var (sut, _) = CreateSut(db);
        var ctx = new DefaultHttpContext();

        Task<Payload> Action(CancellationToken ct) =>
            Task.FromResult(new Payload("requires goal", 422));

        IResult Mapper(Payload p) =>
            p.Code == 422 ? Results.UnprocessableEntity(p) : Results.Ok(p);

        var first = await sut.ExecuteAsync(ctx, userId: 9, "planner.generate", "key-422",
            Action, Mapper, NullLogger.Instance, CancellationToken.None);
        var second = await sut.ExecuteAsync(ctx, userId: 9, "planner.generate", "key-422",
            Action, Mapper, NullLogger.Instance, CancellationToken.None);

        StatusOf(first).Should().Be(StatusCodes.Status422UnprocessableEntity);
        StatusOf(second).Should().Be(StatusCodes.Status422UnprocessableEntity);

        var replayed = ValueOf(second).Should().BeOfType<Payload>().Subject;
        replayed.Code.Should().Be(422);
        replayed.Message.Should().Be("requires goal");
    }

    [Fact]
    public async Task Replay_WithCorruptCachedBody_FallsBackToSafe200Envelope()
    {
        await using var db = CreateDb();
        var (sut, cmd) = CreateSut(db);
        var ctx = new DefaultHttpContext();

        var acquired = await cmd.AcquireAsync(userId: 5, "op.corrupt", "key-bad", CancellationToken.None);
        await cmd.CompleteAsync(acquired.Execution.Id, "{not-valid-json", CancellationToken.None);

        var calls = 0;
        Task<Payload> Action(CancellationToken ct)
        {
            calls++;
            return Task.FromResult(new Payload("never", 1));
        }

        var result = await sut.ExecuteAsync(ctx, userId: 5, "op.corrupt", "key-bad",
            Action, p => Results.Ok(p), NullLogger.Instance, CancellationToken.None);

        calls.Should().Be(0);
        StatusOf(result).Should().Be(StatusCodes.Status200OK);

        var value = ValueOf(result);
        value.Should().NotBeNull();
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        json.Should().Contain("Command already processed.");
    }
}

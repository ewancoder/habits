﻿using System.ComponentModel;
using System.Text.Json.Serialization;
using Habits.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateSlimBuilder(args);
var isDebug = false;
#if DEBUG
isDebug = true;
#endif

var config = TyrHostConfiguration.Default(
    builder.Configuration,
    "Habits",
    isDebug: isDebug);

await builder.ConfigureTyrApplicationBuilderAsync(config);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = SerializerContext.Default;
    options.SerializerOptions.Encoder = null; // Needed for fast path.
    //options.SerializerOptions.DefaultBufferSize = 16_000_000; // Probably needed for fast path.
});

var db = await Repository.LoadAsync();
builder.Services.AddSingleton(db);
builder.Services.AddHostedService<DataSaverHostedService>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<HabitsApp>>();
app.ConfigureTyrApplication(config, logger);
logger.LogInformation("Starting the application");

var apis = app.MapGroup("/").RequireAuthorization();
var habitsGroup = apis.MapGroup("/api/habits")
    .WithTags("Habits");

var authGroup = apis.MapGroup("/api/auth").WithTags("Authentication");
authGroup.MapPost("/logout", async (HttpResponse _, HttpContext context) =>
{
    await context.SignOutAsync();
    context.Response.Cookies.Delete("AuthInfo");
})
    .WithSummary("Logout")
    .WithDescription("Signs out and deletes session cookie.");

habitsGroup.MapGet("/", (User user) =>
{
    return db.GetHabitsForUser(user.UserId);
})
    .WithSummary("Get all habits")
    .WithDescription("Gets all habits for current user.");

habitsGroup.MapGet("/{habitId}", Results<NotFound, Ok<Habit>> ([Description("Identifier of the habit.")]string habitId, User user) =>
{
    var habits = db.GetHabitsForUser(user.UserId);

    var habit = habits.Find(habit => habit.Name == habitId);
    if (habit is null)
        return TypedResults.NotFound();

    return TypedResults.Ok(habit);
})
    .WithSummary("Get a single habit")
    .WithDescription("Gets a single habit for current user.");

habitsGroup.MapPost("/", Results<NotFound, BadRequest<string>, Created<Created>> ([Description("A new habit information")]CreateHabit body, User user) =>
{
    var habits = db.GetHabitsForUser(user.UserId);

    if (habits.Exists(habit => habit.Name == body.Name))
        return TypedResults.BadRequest("Habit with this name already exists.");

    var habit = new Habit
    {
        Name = body.Name,
        LengthDays = body.LengthDays,
        Days = []
    };

    db.AddHabit(user.UserId, habit);
    db.MarkNeedToSave();
    return TypedResults.Created("/api/habits", new Created(habit.Name));
})
    .WithSummary("Create a new habit")
    .WithDescription("Creates a new habit for current user.");

habitsGroup.MapPut("/{id}", Results<NotFound, BadRequest<string>, Ok<Habit>> ([Description("Habit identifier. Same as the habit's name.")]string id, [Description("Habit information for an update.")]CreateHabit body, User user) =>
{
    var habits = db.GetHabitsForUser(user.UserId);

    var habit = habits.Find(habit => habit.Name == id);
    if (habit is null)
        return TypedResults.NotFound();

    if (body.Name != habit.Name && habits.Exists(habit => habit.Name == body.Name))
        return TypedResults.BadRequest("Habit with this name already exists.");

    habit.Name = body.Name;
    habit.LengthDays = body.LengthDays;
    db.MarkNeedToSave();
    return TypedResults.Ok(habit);
})
    .WithSummary("Update a habit")
    .WithDescription("Updates information of the habit.");

habitsGroup.MapPost("/{id}/days/{day}", Results<NotFound, Ok<Habit>> ([Description("Habit identifier = habit name.")]string id, [Description("Day number to mark, counted from 2020.")]int day, User user) =>
{
    var habits = db.GetHabitsForUser(user.UserId);

    var habit = habits.Find(habit => habit.Name == id);
    if (habit is null)
        return TypedResults.NotFound();

    habit.Days.Add(day);
    db.MarkNeedToSave();
    return TypedResults.Ok(habit);
})
    .WithSummary("Mark a day")
    .WithDescription("Marks a day as if we did the habit on that day.");

habitsGroup.MapDelete("/{id}/days/{day}", Results<NotFound, Ok<Habit>> ([Description("Habit identifier = habit name.")]string id, [Description("Day number to unmark, counted from 2020.")]int day, User user) =>
{
    var habits = db.GetHabitsForUser(user.UserId);

    var habit = habits.Find(habit => habit.Name == id);
    if (habit is null)
        return TypedResults.NotFound();

    habit.Days.Remove(day);
    db.MarkNeedToSave();
    return TypedResults.Ok(habit);
})
    .WithSummary("Unmark a day")
    .WithDescription("Unmarks a day as if we did not do anything on it.");

await app.RunAsync();

internal sealed record CreateHabit(
    [property: Description("Name of the habit. Should be unique.")]
    string Name,
    [property: Description("Amount of days that we don't need to do anything after we did the habit one time.")]
    int LengthDays);

public sealed class Habit
{
    [Description("Name of the habit")]
    public required string Name { get; set; }

    [Description("Amount of days that we don't need to do anything after we've done the habit one time.")]
    public required int LengthDays { get; set; }

    [Description("Marked days when we've actually done the habit.")]
    public required HashSet<int> Days { get; set; } = [];
}

[JsonSerializable(typeof(List<Habit>))]
[JsonSerializable(typeof(Created))]
[JsonSerializable(typeof(CreateHabit))]
[JsonSerializable(typeof(Dictionary<string, List<Habit>>))]
internal partial class SerializerContext : JsonSerializerContext
{
}

internal sealed record Created(
    [property: Description("Identifier of a newly created habit. It is the same as the name of the habit.")]
    string Id);

/// <summary>
/// A stub class for logging context.
/// </summary>
public sealed record HabitsApp();


using Cs2D.Server;
using Cs2D.Server.Game;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GameService>());
builder.Services.AddSignalR();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.SetIsOriginAllowed(o => new Uri(o).IsLoopback).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
        IssuerSigningKey = Jwt.Key(builder.Configuration),
    };
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var t = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(t) && ctx.HttpContext.Request.Path.StartsWithSegments("/hub")) ctx.Token = t;
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();
app.UseCors(); app.UseAuthentication(); app.UseAuthorization();

app.MapPost("/api/register", (Credentials c, UserStore s, IConfiguration cfg) =>
{
    if (c.Username is not { Length: >= 3 and <= 20 } || c.Password is not { Length: >= 6 })
        return Results.BadRequest("Username 3-20 chars, password min 6.");
    var u = s.Register(c.Username.Trim(), c.Password);
    return u == null ? Results.Conflict("Username taken.") : Results.Ok(new { token = Jwt.Create(u, cfg), name = u.Name });
});
app.MapPost("/api/login", (Credentials c, UserStore s, IConfiguration cfg) =>
{
    var u = s.Login(c.Username ?? "", c.Password ?? "");
    return u == null ? Results.Unauthorized() : Results.Ok(new { token = Jwt.Create(u, cfg), name = u.Name });
});
app.MapHub<GameHub>("/hub/game");
app.Run("http://localhost:5275");

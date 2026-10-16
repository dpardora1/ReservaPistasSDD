using SalaReservas.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ReservationService>();

var app = builder.Build();
app.MapPost("/reservations", ReservationEndpoint.Create);
app.MapDelete("/reservations/{id}", ReservationEndpoint.Cancel);

app.Run();

public partial class Program { }

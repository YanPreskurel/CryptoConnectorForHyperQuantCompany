using Bitfinex;
using ConnectorTest;
using Portfolio;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// ��������� ������ � ��������� ����� 'builder.Services'
builder.Services.AddSingleton<ITestConnector, BitfinexConnector>();
builder.Services.AddTransient<PortfolioService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.MapGet("/", context =>
{
    context.Response.Redirect("/Portfolio");
    return Task.CompletedTask;
});

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets(); // �������, ��� ���� ����� ���� ��� �����, ���� ���
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

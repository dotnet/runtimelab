using DeviceRunners.UITesting;
using DeviceRunners.VisualRunners;
using DeviceRunners.XHarness;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;

namespace BindingsGeneration.FrameworkTests;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.ConfigureUITesting()
			.UseXHarnessTestRunner(conf => conf
				.AddTestAssembly(typeof(MauiProgram).Assembly)
				.AddXunit())
			.UseVisualTestRunner(conf => conf
				.AddConsoleResultChannel()
				.AddTestAssembly(typeof(MauiProgram).Assembly)
				.AddXunit());

		builder.Logging.AddDebug();

		return builder.Build();
	}
}

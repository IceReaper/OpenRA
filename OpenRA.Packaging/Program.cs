using System;
using System.Diagnostics;
using System.IO;

foreach (var gamePath in Directory.GetDirectories("../Games"))
{
	foreach (var runtime in new[] { "linux-x64", "win-x64", "osx-x64" })
	{
		var process = Process.Start(new ProcessStartInfo
		{
			WorkingDirectory = gamePath,
			FileName = "dotnet",
			ArgumentList =
			{
				"publish",
				"--configuration", "Release",
				"--self-contained", "true",
				"-p:PublishReadyToRun=true",
				"-p:PublishSingleFile=true",
				"--runtime", runtime
			},
			UseShellExecute = false,
			RedirectStandardOutput = true,
			CreateNoWindow = true
		});

		if (process == null)
			continue;

		while (!process.StandardOutput.EndOfStream)
			Console.WriteLine(process.StandardOutput.ReadLine());

		foreach (var file in Directory.GetFiles(Path.Combine(gamePath, "bin", "Release", "net8.0", runtime, "publish"), "*.pdb"))
			File.Delete(file);

		// TODO remove
		break;
	}

	// TODO remove
	break;
}

using System;
using System.Diagnostics;
using System.IO;
using Digimarc.Shared;
using Digimarc.Shared.Classes;
using ImageRecognitionLtu.LoadTests;
using System.Configuration;

namespace IrTest
{
	class Program
	{
		const string OptTarget = "tar";
		const string OptLoop = "loop";
		private static readonly ArgumentParser Args = new ArgumentParser();

		static int Main(string[] args)
		{
			// setup and parse arguments
			Args.DescriptionPadding = 8;
			Args.AddArgument(OptTarget, "test", "test", "Target environment: 'debug', 'dev', 'test', 'labs', 'live', 'paris'");
			Args.AddArgument(OptLoop, "10", "10", "Loop count");

			Args.AddArgument("?", "This help page");
			Args.Notes.AppendLine("This tool tests Image Recognition queries");
			Args.Notes.AppendLine("");
			Args.Notes.AppendLine("Examples:");
			Args.Notes.AppendLine("IrTest -tar:test");
			Args.Notes.AppendLine("IrTest -tar:test -loop:50");

			// parse passed arguments
			if (!Args.Parse(args))
			{
				Logger.Warn("One or more invalid arguments was specified");
				return 1;
			}

			// show help if requested
			if (Args.IsSet("?") || Args.IsEmpty)
				return Args.ShowConsoleHelp();

			string env = Args.GetValue(OptTarget).ToLower();

			int tries = 10;
			if (!int.TryParse(Args.GetValue(OptLoop), out tries))
			{
				Logger.Warn("Invalid loop value specified");
				return 1;
			}

			var url = new LtuService(env).GetIrServerUrl();
			var appkey = new LtuService(env).GetApplicationKey();
			Logger.InfoFormat($"Env: {env}, Loops: {tries}");
			Logger.InfoFormat($"Url: {url}, App: {appkey}");
			Logger.InfoFormat("");
			var sw = new Stopwatch();

			sw.Start();
			for (int loop = 1; loop <= tries; loop++)
			{
				SearchImageFound(env, loop, tries);
				SearchImageNotFound(env, loop, tries);
			}
			sw.Stop();

			var ave = TimeSpan.FromTicks(sw.Elapsed.Ticks / (tries * 2));
			Logger.InfoFormat("Total elapsed time: {0}, Average: {1}", FormatTime(sw), FormatTime(ave));

			return 0;
		}

		public static void SearchImageFound(string env, int loop, int tries)
		{
			var sw = new Stopwatch();

			using (var fileStream = new MemoryStream(File.ReadAllBytes("found.png")))
			{
				try
				{
					sw.Start();
					var searchResponse = new LtuService(env).SearchByUpload(fileStream);
					sw.Stop();

					if (searchResponse.nb_results_found > 0)
						Logger.InfoFormat("Loop {0} of {1}, Found: Yes, elapsed time: {2}", loop, tries, FormatTime(sw));
					else
						Logger.WarnFormat("Loop {0} of {1}, Found: No,  elapsed time: {2}", loop, tries, FormatTime(sw));
				}
				catch (Exception ex)
				{
					var msg = $"Search failed: {ex.Message}";
					Logger.ErrorFormat(msg);
				}
			}
		}

		public static void SearchImageNotFound(string env, int loop, int tries)
		{
			var sw = new Stopwatch();

			using (var fileStream = new MemoryStream(File.ReadAllBytes("not-found.png")))
			{
				try
				{
					sw.Start();
					var searchResponse = new LtuService(env).SearchByUpload(fileStream);
					sw.Stop();

					if (searchResponse.nb_results_found == 0)
						Logger.InfoFormat("Loop {0} of {1}, Found: No, elapsed time: {2}", loop, tries, FormatTime(sw));
					else
						Logger.WarnFormat("Loop {0} of {1}, Found: Yes,  elapsed time: {2}", loop, tries, FormatTime(sw));
				}
				catch (Exception ex)
				{
					var msg = $"Search failed: {ex.Message}";
					Logger.ErrorFormat(msg);
				}
			}
		}

		/// <summary>
		/// Formats stopwatch time into a common friendly format
		/// </summary>
		private static string FormatTime(Stopwatch sw)
		{
			return FormatTime(sw.Elapsed);
		}

		/// <summary>
		/// Formats stopwatch time into a common friendly format
		/// </summary>
		private static string FormatTime(TimeSpan ts)
		{
			if (ts.TotalSeconds == 1) return "(1sec)";
			if (ts.TotalSeconds < 60) return string.Format("({0:0.##}secs)", ts.TotalSeconds);

			string mins = (ts.Minutes == 1) ? "min" : "mins";
			string secs = (ts.Seconds == 1) ? "sec" : "secs";
			return string.Format("({0}{1} {2:00}{3})", ts.Minutes, mins, ts.Seconds, secs);
		}
	}
}

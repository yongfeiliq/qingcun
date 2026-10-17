using System;
using System.IO;

internal static class CodexDiscovery
{
	internal static string FindMac(string home, string[] applications, string searchPath)
	{
		string[] array = new string[3] { "Contents/Resources/codex-cli/bin/codex", "Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex", "Contents/Resources/codex" };
		string[] array2 = applications;
		foreach (string path in array2)
		{
			string[] array3 = new string[2] { "ChatGPT.app", "Codex.app" };
			foreach (string path2 in array3)
			{
				string[] array4 = array;
				foreach (string path3 in array4)
				{
					string text = Path.Combine(path, path2, path3);
					if (File.Exists(text))
					{
						return text;
					}
				}
			}
		}
		array2 = new string[3]
		{
			"/opt/homebrew/bin/codex",
			"/usr/local/bin/codex",
			Path.Combine(home, ".local", "bin", "codex")
		};
		foreach (string text2 in array2)
		{
			if (File.Exists(text2))
			{
				return text2;
			}
		}
		array2 = (searchPath ?? "").Split(Path.PathSeparator);
		foreach (string text3 in array2)
		{
			try
			{
				string text4 = Path.Combine(text3.Trim('"'), "codex");
				if (Path.IsPathRooted(text4) && File.Exists(text4))
				{
					return text4;
				}
			}
			catch (ArgumentException)
			{
			}
		}
		return "";
	}
}

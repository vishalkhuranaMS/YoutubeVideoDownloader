using System;
using System.IO;
using System.Net;
using VideoLibrary;

class Program
{
	static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("url is null");
			return;
		}

		var url = args[0];
		var video = GetYouTubeVideo(url);

		if (video == null)
		{
			Console.WriteLine("Invalid YouTube URL or video not found.");
			return;
		}

		// TODO : avoid using video.Title in filepath name

		var filePath = Path.Combine(Directory.GetCurrentDirectory(), video.Title + ".mp4");

		using (var webClient = new WebClient())
		{
			webClient.DownloadFile(video.Uri, filePath);
		}

		Console.WriteLine($"Video downloaded to: {filePath}");
	}

	static YouTubeVideo GetYouTubeVideo(string url)
	{
		try
		{
			var youTube = YouTube.Default;
			return youTube.GetVideo(url);
		}
		catch
		{
			return null;
		}
	}
}

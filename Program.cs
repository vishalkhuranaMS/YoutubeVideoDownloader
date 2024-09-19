using System;
using System.IO;
using System.Net;
using VideoLibrary;
using Whisper.net;
using System.Threading.Tasks;
using Whisper.net.Ggml;
using System.Diagnostics;
using NAudio.Wave;
using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Microsoft.VisualBasic;
using System.Text;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;


class Program
{
	static async Task Main(string[] args)
	{
		//if (args.Length == 0)
		//{
		//	Console.WriteLine("url is null");
		//	return;
		//}

        var url = "https://www.youtube.com/watch?v=Tn6-PIqc4UM&t=70s";
        var video = GetYouTubeVideo(url);

		if (video == null)
		{
			Console.WriteLine("Invalid YouTube URL or video not found.");
			return;
		}

		// TODO : avoid using video.Title in filepath name

		var filePath = Path.Combine(Directory.GetCurrentDirectory(), "ujgupta" + ".mp4");

		using (var webClient = new WebClient())
		{
			webClient.DownloadFile(video.Uri, filePath);
		}

		Console.WriteLine($"Video downloaded to: {filePath}");

		var audioPath = Path.Combine(Directory.GetCurrentDirectory(), "ujgupta" + "_audio.wav");

		// convert video to audio
		ConvertAudioFromVideo(filePath, audioPath, "ujgupta");

		// use model to get the transcript from the audio file.

		var modelFileName = @"C:\Users\ujgupta\Downloads\ggml-base.bin";  // update your local path
		var audioFileName = Path.Combine(Directory.GetCurrentDirectory(), "ujgupta" + "16kHz_audio.wav");
		
		if (!File.Exists(modelFileName))
		{
			using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
			using var fileWriter = File.OpenWrite(modelFileName);
			await modelStream.CopyToAsync(fileWriter);
		}
	
		try
		{
			using var whisperFactory = WhisperFactory.FromPath(modelFileName);
			using var processor = whisperFactory.CreateBuilder()
				.WithLanguage("auto") 
				.Build();
			
			using var fileStream = File.OpenRead(audioFileName);
			Console.WriteLine(fileStream.ToString());

            StringBuilder transcriptBuilder = new StringBuilder();
            await foreach (var result in processor.ProcessAsync(fileStream))
			{
                transcriptBuilder.AppendLine($"{result.Start}->{result.End}: {result.Text}");
                //Console.WriteLine($"{result.Start}->{result.End}:{result.Text}");
			}

            string videoTranscript = transcriptBuilder.ToString();

            string openAIEndpoint = "";
			string openAIKey = "";

            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(openAIEndpoint), new AzureKeyCredential(openAIKey));

            var chatClient = client.GetChatClient("gpt-35-turbo");

            // Prepare the prompt for summarization
            string prompt = $"Below is a transcript of video in format starttime->endtime: transcript. Please summarize this while also combining the timeframes and The SUMMARY should look like - `starttime1->endtime1: summary1, starttime2->endtime2: summary2, starttime3->endtime3: summary3`. Here is the transcript of video:\n\n{videoTranscript}";

            // Create a chat message
            var chatMessage = ChatMessage.CreateSystemMessage(prompt);

            // Send the message and get the response
            var response = await chatClient.CompleteChatAsync(new[] { chatMessage });

			Console.WriteLine(response.Value);

        }
        catch (EntryPointNotFoundException ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
		}
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

	public static void ConvertAudioFromVideo(string inputPath, string outputPath, string title)
	{
		var inputFile = new MediaFile { Filename = inputPath };
		var outputFile = new MediaFile { Filename = outputPath };
		var conversionOptions = new ConversionOptions
		{
			AudioSampleRate = AudioSampleRate.Hz22050
		};
		using (var engine = new Engine())
		{
			engine.Convert(inputFile, outputFile, conversionOptions);
		}

		// since the model needs 16kHz sample rate, using NAusdio.wave to sample it at 16kHz
		// this generate another file, TODO : delete the previously generated file which is sampled at 22050Hz.
		using (var reader = new WaveFileReader(outputPath))
		{
			var outputFormat = new WaveFormat(16000, reader.WaveFormat.Channels);
			
			using (var resampler = new MediaFoundationResampler(reader, outputFormat))
			{
				using (var writer = new WaveFileWriter(Path.Combine(Directory.GetCurrentDirectory(), title + "16kHz_audio.wav"), outputFormat))
				{
					byte[] buffer = new byte[1024];
					int bytesRead;
					while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
					{
						writer.Write(buffer, 0, bytesRead);
					}
				}
			}
		}
					Console.WriteLine($"Audio extracted to: {outputPath}");
	}

private static async Task DownloadModel(string fileName, GgmlType ggmlType)
	{
		Console.WriteLine($"Downloading Model {fileName}");
		using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
		using var fileWriter = File.OpenWrite(fileName);
		await modelStream.CopyToAsync(fileWriter);
	}
}

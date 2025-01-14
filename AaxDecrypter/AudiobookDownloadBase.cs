﻿using System;
using System.IO;
using Dinah.Core;
using Dinah.Core.Net.Http;
using Dinah.Core.StepRunner;
using FileManager;

namespace AaxDecrypter
{
	public enum OutputFormat { M4b, Mp3 }

	public abstract class AudiobookDownloadBase
	{
		public event EventHandler<string> RetrievedTitle;
		public event EventHandler<string> RetrievedAuthors;
		public event EventHandler<string> RetrievedNarrators;
		public event EventHandler<byte[]> RetrievedCoverArt;
		public event EventHandler<DownloadProgress> DecryptProgressUpdate;
		public event EventHandler<TimeSpan> DecryptTimeRemaining;
		public event EventHandler<string> FileCreated;

		protected bool IsCanceled { get; set; }
		protected string OutputFileName { get; private set; }
		protected string CacheDir { get; }
		protected DownloadLicense DownloadLicense { get; }
		protected NetworkFileStream InputFileStream => (nfsPersister ??= OpenNetworkFileStream()).NetworkFileStream;

		// Don't give the property a 'set'. This should have to be an obvious choice; not accidental
		protected void SetOutputFileName(string newOutputFileName) => OutputFileName = newOutputFileName;

		protected abstract StepSequence Steps { get; }
		private NetworkFileStreamPersister nfsPersister;

		private string jsonDownloadState => Path.Combine(CacheDir, Path.GetFileNameWithoutExtension(OutputFileName) + ".json");
		private string tempFile => PathLib.ReplaceExtension(jsonDownloadState, ".tmp");

		public AudiobookDownloadBase(string outFileName, string cacheDirectory, DownloadLicense dlLic)
		{
			OutputFileName = ArgumentValidator.EnsureNotNullOrWhiteSpace(outFileName, nameof(outFileName));

			var outDir = Path.GetDirectoryName(OutputFileName);
			if (!Directory.Exists(outDir))
				throw new DirectoryNotFoundException($"Directory does not exist: {nameof(outDir)}");

			if (!Directory.Exists(cacheDirectory))
				throw new DirectoryNotFoundException($"Directory does not exist: {nameof(cacheDirectory)}");
			CacheDir = cacheDirectory;

			// delete file after validation is complete
			FileUtility.SaferDelete(OutputFileName);
			DownloadLicense = ArgumentValidator.EnsureNotNull(dlLic, nameof(dlLic));
		}

		public abstract void Cancel();
		protected abstract bool Step2_DownloadAudiobookAsSingleFile();
		protected abstract bool Step1_GetMetadata();

		public virtual void SetCoverArt(byte[] coverArt)
		{
			if (coverArt is null) return;

			OnRetrievedCoverArt(coverArt);
		}


		public bool Run()
		{
			var (IsSuccess, Elapsed) = Steps.Run();

			if (!IsSuccess)
			{
				Console.WriteLine("WARNING-Conversion failed");
				return false;
			}

			return true;
		}		

		protected void OnRetrievedTitle(string title) 
			=> RetrievedTitle?.Invoke(this, title);
		protected void OnRetrievedAuthors(string authors) 
			=> RetrievedAuthors?.Invoke(this, authors);
		protected void OnRetrievedNarrators(string narrators) 
			=> RetrievedNarrators?.Invoke(this, narrators);
		protected void OnRetrievedCoverArt(byte[] coverArt) 
			=> RetrievedCoverArt?.Invoke(this, coverArt);
		protected void OnDecryptProgressUpdate(DownloadProgress downloadProgress) 
			=> DecryptProgressUpdate?.Invoke(this, downloadProgress);
		protected void OnDecryptTimeRemaining(TimeSpan timeRemaining)
			=> DecryptTimeRemaining?.Invoke(this, timeRemaining);
		protected void OnFileCreated(string path)
			=> FileCreated?.Invoke(this, path);

		protected void CloseInputFileStream()
		{
			nfsPersister?.NetworkFileStream?.Close();
			nfsPersister?.Dispose();
		}

		protected bool Step3_CreateCue()
		{
			// not a critical step. its failure should not prevent future steps from running
			try
			{
				var path = PathLib.ReplaceExtension(OutputFileName, ".cue");
				path = FileUtility.GetValidFilename(path);
				File.WriteAllText(path, Cue.CreateContents(Path.GetFileName(OutputFileName), DownloadLicense.ChapterInfo));
				OnFileCreated(path);
			}
			catch (Exception ex)
			{
				Serilog.Log.Logger.Error(ex, $"{nameof(Step3_CreateCue)}. FAILED");
			}
			return !IsCanceled;
		}

		protected bool Step4_Cleanup()
		{
			FileUtility.SaferDelete(jsonDownloadState);
			FileUtility.SaferDelete(tempFile);
			return !IsCanceled;
		}

		private NetworkFileStreamPersister OpenNetworkFileStream()
		{
			if (!File.Exists(jsonDownloadState))
				return NewNetworkFilePersister();

			try
			{
				var nfsp = new NetworkFileStreamPersister(jsonDownloadState);
				// If More than ~1 hour has elapsed since getting the download url, it will expire.
				// The new url will be to the same file.
				nfsp.NetworkFileStream.SetUriForSameFile(new Uri(DownloadLicense.DownloadUrl));
				return nfsp;
			}
			catch
			{
				FileUtility.SaferDelete(jsonDownloadState);
				FileUtility.SaferDelete(tempFile);
				return NewNetworkFilePersister();
			}
		}

		private NetworkFileStreamPersister NewNetworkFilePersister()
		{
			var headers = new System.Net.WebHeaderCollection
			{
				{ "User-Agent", DownloadLicense.UserAgent }
			};

			var networkFileStream = new NetworkFileStream(tempFile, new Uri(DownloadLicense.DownloadUrl), 0, headers);
			return new NetworkFileStreamPersister(networkFileStream, jsonDownloadState);
		}
	}
}

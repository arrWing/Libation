﻿using System;

namespace FileLiberator
{
    public abstract class AudioDecodable : Processable
    {
        public event EventHandler<Action<byte[]>> RequestCoverArt;
        public event EventHandler<string> TitleDiscovered;
        public event EventHandler<string> AuthorsDiscovered;
        public event EventHandler<string> NarratorsDiscovered;
        public event EventHandler<byte[]> CoverImageDiscovered;
        public abstract void Cancel();

        protected void OnRequestCoverArt(Action<byte[]> setCoverArtDel)
		{
            Serilog.Log.Logger.Debug("Event fired {@DebugInfo}", new { Name = nameof(RequestCoverArt) });
            RequestCoverArt?.Invoke(this, setCoverArtDel);
        }

        protected void OnTitleDiscovered(string title)
		{
            Serilog.Log.Logger.Debug("Event fired {@DebugInfo}", new { Name = nameof(TitleDiscovered), Title = title });
            TitleDiscovered?.Invoke(this, title);
		}

        protected void OnAuthorsDiscovered(string authors)
		{
            Serilog.Log.Logger.Debug("Event fired {@DebugInfo}", new { Name = nameof(AuthorsDiscovered), Authors = authors });
            AuthorsDiscovered?.Invoke(this, authors);
		}

        protected void OnNarratorsDiscovered(string narrators)
		{
            Serilog.Log.Logger.Debug("Event fired {@DebugInfo}", new { Name = nameof(NarratorsDiscovered), Narrators = narrators });
            NarratorsDiscovered?.Invoke(this, narrators);
		}

        protected void OnCoverImageDiscovered(byte[] coverImage)
		{
            Serilog.Log.Logger.Debug("Event fired {@DebugInfo}", new { Name = nameof(CoverImageDiscovered), CoverImageBytes = coverImage?.Length });
            CoverImageDiscovered?.Invoke(this, coverImage);
        }
    }
}

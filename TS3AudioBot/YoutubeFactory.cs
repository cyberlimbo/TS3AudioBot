﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;

namespace TS3AudioBot
{
	class YoutubeFactory : IRessourceFactory
	{
		private WebClient wc;

		public YoutubeFactory()
		{
			wc = new WebClient();
		}

		public bool GetRessource(string url, out AudioRessource ressource)
		{
			YoutubeRessource ytRessource;
			var resultCode = ExtractURL(url, out ytRessource);
			ressource = ytRessource;
			return resultCode == YoutubeResultCode.Success;
		}

		public YoutubeResultCode ExtractURL(string ytLink, out YoutubeRessource result)
		{
			result = null;
			string resulthtml = string.Empty;
			Match matchYtId = Regex.Match(ytLink, @"(&|\?)v=([a-zA-Z0-9\-_]+)");
			if (!matchYtId.Success)
				return YoutubeResultCode.YtIdNotFound;
			string ytID = matchYtId.Groups[2].Value;

			try
			{
				resulthtml = wc.DownloadString(string.Format("http://www.youtube.com/get_video_info?video_id={0}&el=info", ytID));
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Warning, "Youtube downloadreqest failed: " + ex.Message);
				return YoutubeResultCode.NoYtConnection;
			}

			List<VideoType> videoTypes = new List<VideoType>();
			NameValueCollection dataParse = HttpUtility.ParseQueryString(resulthtml);

			string vTitle = dataParse["title"] ?? string.Empty;

			string videoDataUnsplit = dataParse["url_encoded_fmt_stream_map"];
			if (videoDataUnsplit == null)
				return YoutubeResultCode.NoFMT;
			string[] videoData = videoDataUnsplit.Split(',');

			foreach (string vdat in videoData)
			{
				NameValueCollection videoparse = HttpUtility.ParseQueryString(vdat);

				string vLink = videoparse["url"];
				if (vLink == null)
					continue;

				string vType = videoparse["type"];
				if (vType == null)
					continue;

				string vQuality = videoparse["quality"];
				if (vQuality == null)
					continue;

				VideoType vt = new VideoType();
				vt.link = vLink;
				vt.codec = GetCodec(vType);
				vt.qualitydesciption = vQuality;
				videoTypes.Add(vt);
			}

			videoDataUnsplit = dataParse["adaptive_fmts"];
			if (videoDataUnsplit == null)
				return YoutubeResultCode.NoFMTS;
			videoData = videoDataUnsplit.Split(',');

			foreach (string vdat in videoData)
			{
				NameValueCollection videoparse = HttpUtility.ParseQueryString(vdat);

				string vType = videoparse["type"];
				if (vType == null)
					continue;

				bool audioOnly = false;
				if (vType.StartsWith("video/"))
					continue;
				else if (vType.StartsWith("audio/"))
					audioOnly = true;

				string vLink = videoparse["url"];
				if (vLink == null)
					continue;

				VideoType vt = new VideoType();
				vt.codec = GetCodec(vType);
				vt.qualitydesciption = vType;
				vt.link = vLink;
				if (audioOnly)
					vt.audioOnly = true;
				else
					vt.videoOnly = true;
				videoTypes.Add(vt);
			}

			result = new YoutubeRessource(ytID, vTitle, videoTypes.AsReadOnly());
			return result.AvailableTypes.Count > 0 ? YoutubeResultCode.Success : YoutubeResultCode.NoVideosExtracted;
		}

		public YoutubeResultCode ExtractPlaylist()
		{
			//https://gdata.youtube.com/feeds/api/playlists/UU4L4Vac0HBJ8-f3LBFllMsg?alt=json
			//https://gdata.youtube.com/feeds/api/playlists/UU4L4Vac0HBJ8-f3LBFllMsg?alt=json&start-index=1&max-results=1

			//totalResults":{"\$t":(\d+)}

			//"url"\w*:\w*"(https:\/\/www\.youtube\.com\/watch\?v=[a-zA-Z0-9\-_]+&)
			return YoutubeResultCode.Success;
		}

		public void PostProcess(PlayData data, out bool abortPlay)
		{
			YoutubeRessource ytRessource = (YoutubeRessource)data.Ressource;

			var availList = ytRessource.AvailableTypes.ToList();
			int autoselectIndex = availList.FindIndex(t => t.codec == VideoCodec.M4A);
			if (autoselectIndex == -1)
				autoselectIndex = availList.FindIndex(t => t.audioOnly);
			if (autoselectIndex != -1)
			{
				ytRessource.Selected = autoselectIndex;
				abortPlay = false;
				return;
			}

			StringBuilder strb = new StringBuilder();
			strb.AppendLine("\nMultiple formats found please choose one with !f <number>");
			int count = 0;
			foreach (var videoType in ytRessource.AvailableTypes)
			{
				strb.Append("[");
				strb.Append(count++);
				strb.Append("] ");
				strb.Append(videoType.codec.ToString());
				strb.Append(" @ ");
				strb.AppendLine(videoType.qualitydesciption);
			}
			// TODO: SET RESPONSE !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
			data.Session.Write(strb.ToString());
			data.Session.UserRessource = data;
			data.Session.SetResponse(ResponseYoutube, null, false);
			abortPlay = true;
		}

		private bool ResponseYoutube(BotSession session, TextMessage tm, bool isAdmin)
		{
			string[] command = tm.Message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (command[0] != "!f")
				return false;
			if (command.Length != 2)
				return true;
			int entry;
			if (int.TryParse(command[1], out entry))
			{
				PlayData data = session.UserRessource;
				if (data == null || data.Ressource as YoutubeRessource == null)
				{
					session.Write("An unexpected error with the ytressource occured: null");
					return true;
				}
				YoutubeRessource ytRessource = (YoutubeRessource)data.Ressource;
				if (entry < 0 || entry >= ytRessource.AvailableTypes.Count)
					return true;
				ytRessource.Selected = entry;

				session.Bot.Play(data);
			}
			return true;
		}

		private VideoCodec GetCodec(string type)
		{
			string lowtype = type.ToLower();
			bool audioOnly = false;
			string codecSubStr;
			if (lowtype.StartsWith("video/"))
				codecSubStr = lowtype.Substring("video/".Length);
			else if (lowtype.StartsWith("audio/"))
			{
				codecSubStr = lowtype.Substring("audio/".Length);
				audioOnly = true;
			}
			else
				return VideoCodec.Unknown;

			string extractedCodec;
			int codecEnd;
			extractedCodec = (codecEnd = codecSubStr.IndexOf(';')) >= 0 ? codecSubStr.Substring(0, codecEnd) : codecSubStr;

			switch (extractedCodec)
			{
			case "mp4":
				if (audioOnly)
					return VideoCodec.M4A;
				return VideoCodec.MP4;
			case "x-flv":
				return VideoCodec.FLV;
			case "3gpp":
				return VideoCodec.ThreeGP;
			case "webm":
				return VideoCodec.WEBM;
			default:
				return VideoCodec.Unknown;
			}
		}

		public void Dispose()
		{
			if (wc != null)
			{
				wc.Dispose();
				wc = null;
			}
		}
	}

	class VideoType
	{
		public string link;
		public string qualitydesciption;
		public VideoCodec codec;
		public bool audioOnly = false;
		public bool videoOnly = false;

		public override string ToString()
		{
			return qualitydesciption + " @ " + codec + " - " + link;
		}
	}

	class YoutubeRessource : AudioRessource
	{
		public IList<VideoType> AvailableTypes { get; protected set; }

		public int Selected { get; set; }

		public override AudioType AudioType { get { return AudioType.Youtube; } }

		public YoutubeRessource(string link, string youtubeName, IList<VideoType> availableTypes)
			: base(link, youtubeName)
		{
			AvailableTypes = availableTypes;
			Selected = 0;
		}

		public override bool Play(Action<string> setMedia)
		{
			if (Selected < 0 && Selected >= AvailableTypes.Count)
				return false;
			setMedia(AvailableTypes[Selected].link);
			Log.Write(Log.Level.Debug, "YT Playing: {0}", AvailableTypes[Selected]);
			return true;
		}
	}

	enum VideoCodec
	{
		Unknown,
		MP4,
		M4A,
		WEBM,
		FLV,
		ThreeGP,
	}

	enum YoutubeResultCode
	{
		Success,
		YtIdNotFound,
		NoYtConnection,
		NoVideosExtracted,
		NoFMT,
		NoFMTS,
	}
}
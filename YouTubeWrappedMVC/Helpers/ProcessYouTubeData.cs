﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using YouTubeWrappedMVC.Models;

namespace YouTubeWrappedMVC.Helpers
{
    public class ProcessYouTubeData
    {
        private static Dictionary<string, VideoViewModel> pastSearchesDict = new Dictionary<string, VideoViewModel>();

        public async Task Initialise(string takeoutDataJson)
        {
            System.Diagnostics.Debug.WriteLine("Starting");
            List<HistoryVideo> historyVideos = GetHistoryFromJson(takeoutDataJson).ToList();
            System.Diagnostics.Debug.WriteLine("Fetching video data");
            Dictionary<string, VideoViewModel> videoViewModelsDict = await GetVideosFromApi(historyVideos.Take(5000).ToList());
            System.Diagnostics.Debug.WriteLine("Doing calculations");
            
            PerformCalculations(historyVideos, videoViewModelsDict);

            System.Diagnostics.Debug.WriteLine("Complete");
        }

        private static void PerformCalculations(List<HistoryVideo> historyVideos, Dictionary<string, VideoViewModel> videoViewModelsDict)
        {
            Calculations.GetHistoryContext(historyVideos);
            Calculations.HoursPerDay(historyVideos, videoViewModelsDict);
        }

        private async Task<Dictionary<string, VideoViewModel>> GetVideosFromApi(List<HistoryVideo> historyVideos)
        {
            PopulatePastSearchesDict();
            Dictionary<string, VideoViewModel> apiVideosDict = new Dictionary<string, VideoViewModel>();

            foreach (var historyVideo in historyVideos)
            {
                string id = historyVideo.GetVideoID();

                if (!apiVideosDict.ContainsKey(id))
                {
                    if (pastSearchesDict.ContainsKey(id))
                    {
                        apiVideosDict.Add(id, pastSearchesDict[id]);
                    }
                    else
                    {
                        ApiVideo apiVideo = await GetVideoDataFromApi(id);
                        if (apiVideo.Items.Length > 0)
                        {
                            VideoViewModel viewModel = VideoViewModel.FromApiVideo(apiVideo);
                            await WriteVideoViewModelToFile(viewModel);
                            apiVideosDict.Add(id, viewModel);
                            pastSearchesDict.Add(id, viewModel);
                        }

                    }
                }

            }

            return apiVideosDict;
        }

        private void PopulatePastSearchesDict()
        {
            if (pastSearchesDict.Count == 0)
            {

                using (StreamReader streamReader = new StreamReader(UriHelper.PAST_SEARCHES_FILE_URI))
                {
                    string line = streamReader.ReadLine();
                    while (line != null)
                    {
                        VideoViewModel viewModel = VideoViewModel.DeserializeObject(line);
                        pastSearchesDict.Add(viewModel.Id, viewModel);
                        line = streamReader.ReadLine();
                    }
                }
            }


        }

        private async Task WriteVideoViewModelToFile(VideoViewModel viewModel)
        {
            using (StreamWriter sw = new StreamWriter(UriHelper.PAST_SEARCHES_FILE_URI, true))
            {
                await sw.WriteLineAsync(VideoViewModel.SerializeObject(viewModel));
            }
        }

        private async Task<ApiVideo> GetVideoDataFromApi(string videoId)
        {
            Uri uri = new Uri(@"https://youtube.googleapis.com/youtube/v3/videos?part=snippet%2CcontentDetails&id=" + videoId + "&key=AIzaSyDYJH4akcKKjhWuxJKbs3dIl_56dk6masM");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            //request.UserAgent = "12345";

            string responseString = string.Empty;
            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                responseString = reader.ReadToEnd();
                ApiVideo apiVideo = JsonConvert.DeserializeObject<ApiVideo>(responseString);
                return apiVideo;

            }
        }

        private List<HistoryVideo> GetHistoryFromJson(string takeoutDataJson)
        {
            List<HistoryVideo> historyVideos;
            historyVideos = JsonConvert.DeserializeObject<List<HistoryVideo>>(takeoutDataJson);

            return historyVideos.Where(h => h.TitleUrl != null).ToList();
        }
    }
}

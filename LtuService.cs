using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Configuration;

namespace ImageRecognitionLtu.LoadTests
{
	/// <summary>
	/// Ltu image recognition service provider
	/// </summary>
	internal class LtuService
	{
		public LtuService() : this("dev")	{}
		public LtuService(string env)
		{
			_environment = env;
		}

		/// <summary>
		/// Uploads an image into the image recognition database and returns the image identifier. 
		/// </summary>
		public string UploadImage(string id, string keyword, Stream imageStream)
		{
			return new LtuServiceRetryPolicy().ExecuteMethod(() =>
			{
				// make sure stream position is at beginning if imageStream is seekable
				if (imageStream.CanSeek)
					imageStream.Position = 0;

				// image id is generated and passed to LTU
				var imageId = id;
				using (var http = GetHttpClient())
				{
					// LTU requires that the image stream be passed in multipart form content
					using (var multiPart = new MultipartFormDataContent())
					using (var content = new StreamContent(imageStream))
					{
						// add the stream content to the multipart comntent
						multiPart.Add(content, "image_content");

						// image id and the common payload path are passed as url parameters
						var path = $"ltumodify/json/AddImage?application_key={GetApplicationKey()}&image_id={imageId}&keywords={keyword}";

						using (var result = http.PostAsync(path, multiPart).Result)
						{
							// check for not enough information error
							var status = TryGetLtuStatus(result);
							if (status != null && status.code == LtuNotEnoughInformation)
								throw new Exception("Image does not contain enough information to be registed with LTU");

							VerifyLtuSuccess(result, status);
						}
					}
				}
				return imageId;
			});
		}

		/// <summary>
		/// Search for an image by upload
		/// </summary>
		public LtuSearchResponse SearchByUpload(Stream imageStream)
		{
			// make sure stream position is at beginning if imageStream is seekable
			if (imageStream.CanSeek)
				imageStream.Position = 0;

			using (var http = GetHttpClient())
			{
				// LTU requires that the image stream be passed in multipart form content
				using (var multiPart = new MultipartFormDataContent())
				using (var content = new StreamContent(imageStream))
				{
					// add the stream content to the multipart comntent
					multiPart.Add(content, "image_content");

					// image id and the common payload path are passed as url parameters
					var path = $"ltuquery/json/SearchImageByUpload?application_key={GetApplicationKey()}";

					using (var result = http.PostAsync(path, multiPart).Result)
					{
						// check for not enough information error
						var searchResponse = TryGetLtuSearchResponse(result);
						VerifyLtuSuccess(result, searchResponse.status);
						return searchResponse;
					}
				}
			}
		}

		/// <summary>
		/// Deletes an image from the image recognition database.
		/// </summary>
		public void DeleteImage(string imageId)
		{
			new LtuServiceRetryPolicy().ExecuteMethod(() =>
			{
				using (var http = GetHttpClient())
				{
					var path = $"ltumodify/json/DeleteImage?application_key={GetApplicationKey()}&image_id={imageId}";
					using (var result = http.GetAsync(path).Result)
					{
						// check for not found 
						var ltuStatus = TryGetLtuStatus(result);
						if (ltuStatus != null && ltuStatus.code == LtuImageNotFound)
							throw new Exception("Image not found");

						VerifyLtuSuccess(result, ltuStatus);
					}
				}
			});
		}

		/// <summary>
		/// Returns whether the image recognition service is available.
		/// </summary>
		public bool IsAvailable()
		{
			return new LtuServiceRetryPolicy().ExecuteMethod(() =>
			{
				using (var http = GetHttpClient())
				{
					var path = $"ltumodify/json/GetApplicationStatus?application_key={GetApplicationKey()}";
					using (var result = http.GetAsync(path).Result)
					{
						var ltuStatus = TryGetLtuStatus(result);
						return result.IsSuccessStatusCode && ltuStatus != null && ltuStatus.code == LtuSuccess;
					}
				}
			});
		}

		/// <summary>
		/// Verifies that the LTU method call was successful. On failure throws an exception and logs the error.
		/// </summary>
		private static void VerifyLtuSuccess(HttpResponseMessage response, LtuStatus ltuStatus, [CallerMemberName]string methodName = "")
		{
			if (response.IsSuccessStatusCode && ltuStatus != null && ltuStatus.code == LtuSuccess)
				return;

			// build up error message for logging
			var ltuStatusMsg = ltuStatus == null ? "Unable to parse LTU status;" : $"Ltu status code: {ltuStatus.code}, {ltuStatus.message}";

			var responseMsg = $"Http status: {response.StatusCode}, {response.ReasonPhrase}";

			var message = $"Image recognition {methodName} failed.{System.Environment.NewLine}{ltuStatusMsg}{System.Environment.NewLine}{responseMsg}";

			throw new Exception(message);
		}

		/// <summary>
		/// Attempts to deserialize the response into a LtuStatus object. Returns null if deserialization fails.
		/// </summary>
		private static LtuStatus TryGetLtuStatus(HttpResponseMessage responseMessage)
		{
			try
			{
				var responseContent = responseMessage.Content.ReadAsStringAsync().Result;
				var ltuResponse = JsonConvert.DeserializeObject<LtuResponse>(responseContent);

				// if we have a null or partial reponse, return null
				return ltuResponse?.status;
			}
			catch (Exception)
			{
				// trap any deserialization errors and return null
				return null;
			}
		}

		public static LtuSearchResponse TryGetLtuSearchResponse(HttpResponseMessage responseMessage)
		{
			var responseContent = "";
			try
			{
				responseContent = responseMessage.Content.ReadAsStringAsync().Result;
				var ltuResponse = JsonConvert.DeserializeObject<LtuSearchResponse>(responseContent);

				// if we have a null or partial reponse, return null
				return ltuResponse;
			}
			catch (Exception)
			{
				// trap any deserialization errors and return null
				throw new Exception($"Response deserialization failed. HttpStatus: {responseMessage.StatusCode}, Response content: {responseContent}");
			}
		}

		/// <summary>
		/// Returns an LTU specific HttpClient
		/// </summary>
		private HttpClient GetHttpClient()
		{
			var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri(GetIrServerUrl());
			httpClient.Timeout = TimeSpan.FromMinutes(1);
			return httpClient;
		}

		/// <summary>
		/// Returns LTU server address
		/// </summary>
		public string GetIrServerUrl()
		{
			if (_environment == "dev")
				return ConfigurationManager.AppSettings.Get("url.dev");
			if (_environment == "test")
				return ConfigurationManager.AppSettings.Get("url.test");
			if (_environment == "labs")
				return ConfigurationManager.AppSettings.Get("url.labs");
			if (_environment == "live")
				return ConfigurationManager.AppSettings.Get("url.live");
			if (_environment == "paris")
				return ConfigurationManager.AppSettings.Get("url.paris");

			throw new NotImplementedException();
		}

		/// <summary>
		/// Returns LTU application key
		/// </summary>
		public string GetApplicationKey()
		{
			if (_environment == "dev")
				return ConfigurationManager.AppSettings.Get("appkey.dev");
			if (_environment == "test")
				return ConfigurationManager.AppSettings.Get("appkey.test");
			if (_environment == "labs")
				return ConfigurationManager.AppSettings.Get("appkey.labs");
			if (_environment == "live")
				return ConfigurationManager.AppSettings.Get("appkey.live");
			if (_environment == "paris")
				return ConfigurationManager.AppSettings.Get("appkey.paris");

			throw new NotImplementedException();
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		/// <summary>
		/// LtuResponse class used in verifying method status
		/// </summary>
		internal class LtuResponse
		{
			public LtuStatus status { get; set; }
		}

		internal class LtuSearchResponse
		{
			public LtuStatus status { get; set; }
			public int nb_results_found { get; set; }
			public List<LtuImageResult> images { get; set; }
		}

		/// <summary>
		/// LtuStatus class used in verifying method status
		/// </summary>
		internal class LtuStatus
		{
			public string message { get; set; }
			public int code { get; set; }
		}

		internal class LtuImageResult
		{
			public List<string> keywords { get; set; }
			public float score { get; set; }
			public string id { get; set; }
			public string result_info { get; set; }

			public LtuImageResult()
			{
				keywords = new List<string>();
			}

		}

		private const int LtuSuccess = 0;
		private const int LtuImageNotFound = -2802;
		private const int LtuNotEnoughInformation = -1002;

		private string _environment;

	}
}

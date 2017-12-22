using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace ImageRecognitionLtu.LoadTests
{
	public class LtuServiceRetryPolicy
	{

		/// <summary>
		/// Executes the passed generic method, wrapped in exception handling logic
		/// </summary>
		/// <typeparam name="TResponse">Return data type</typeparam>
		/// <param name="webMethod">Function to invoke</param>
		public virtual TResponse ExecuteMethod<TResponse>(Func<TResponse> webMethod)
		{
			var currentRetry = 0;
			for (;;)
			{
				try
				{
					return webMethod.Invoke();
				}
				catch (Exception e)
				{
					currentRetry++;

					// re-throw the exception when over the retry count or the exception is not transient
					if (currentRetry > RetryCount || !IsTransient(e))
						throw;

					Console.WriteLine("Image recognition call being retried. Current retry: " + currentRetry + " ", e);

					// delay next call
					Thread.Sleep(currentRetry * 500);
				}
				return default(TResponse);
			}
		}

		/// <summary>
		/// Executes the passed method, wrapped in exception handling logic, with no return type
		/// </summary>
		/// <param name="webMethod">Function to invoke</param>
		public virtual void ExecuteMethod(Action webMethod)
		{
			var currentRetry = 0;
			for (;;)
			{
				try
				{
					webMethod.Invoke();
					return;
				}
				catch (Exception e)
				{
					currentRetry++;

					// re-throw the exception when over the retry count or the exception is not transient
					if (currentRetry > RetryCount || !IsTransient(e))
						throw;

					Console.WriteLine("Image recognition call being retried. Current retry: " + currentRetry + " ", e);

					// delay next call
					Thread.Sleep(currentRetry * 500);
				}
			}
		}

		private static bool IsTransient(Exception ex)
		{
			var webException = ex as WebException;
			return webException != null && _retryableStatuses.Contains(webException.Status);
		}


		private static List<WebExceptionStatus> _retryableStatuses = new List<WebExceptionStatus>
		{
			WebExceptionStatus.ConnectFailure,
			WebExceptionStatus.ConnectionClosed,
			WebExceptionStatus.Timeout,
			WebExceptionStatus.RequestCanceled
		};

		private const short RetryCount = 2;
	}
}

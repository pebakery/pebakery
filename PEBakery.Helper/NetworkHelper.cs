/*
    Copyright (C) 2019 Hajin Jang
    Licensed under MIT License.
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Helper
{
    public static class NetworkHelper
    {
        #region IsOnline
        /// <summary>
        /// Try connecting to remote website (GitHub).
        /// </summary>
        /// <returns></returns>
        public static bool IsOnline()
        {
            // Deciding which website to test is very, very troublesome.
            // Some countries are notorious for censoring web, such as Google's service.
            // Let's assume GitHub is not censored by most countries.
            using (HttpClient client = new HttpClient())
            {
                // Disguise as a normal Firefox browser.
                const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:65.0) Gecko/20100101 Firefox/65.0";
                client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

                // GET https://github.com
                const string testUrl = "https://github.com";
                Task<HttpResponseMessage> resTask = client.GetAsync(testUrl);
                resTask.Wait();
                using (HttpResponseMessage res = resTask.Result)
                {
                    try
                    {
                        res.EnsureSuccessStatusCode();
                        return true;
                    }
                    catch (HttpRequestException)
                    {
                        return false;
                    }
                }
            }
        }
        #endregion
    }

    #region HttpClientDownloader
    public class HttpClientDownloader
    {
        #region Const
        private const int DefaultBufferSize = 64 * 1024;
        #endregion

        #region Fields and Properties
        private readonly HttpClient _httpClient;
        private readonly Uri _uri;
        private readonly Stream _destStream;
        private readonly int _bufferSize;
        private readonly IProgress<(long Position, long ContentLength, TimeSpan Elapsed)> _progress;
        private readonly TimeSpan _reportInterval;
        private readonly CancellationToken? _cancelToken;

        public HttpStatusCode? StatusCode { get; private set; }
        #endregion

        #region Constructor
        public HttpClientDownloader(
            HttpClient httpClient, Uri uri, Stream destStream,
            int bufferSize = DefaultBufferSize,
            CancellationToken? cancelToken = null)
        {
            _httpClient = httpClient;
            _uri = uri;
            _destStream = destStream;
            _bufferSize = bufferSize;
            _progress = null;
            _reportInterval = TimeSpan.MaxValue;
            _cancelToken = cancelToken;
        }

        public HttpClientDownloader(
            HttpClient httpClient, Uri uri, Stream destStream,
            IProgress<(long Position, long ContentLength, TimeSpan Elapsed)> progress,
            TimeSpan reportInterval,
            CancellationToken? cancelToken = null)
        {
            _httpClient = httpClient;
            _uri = uri;
            _destStream = destStream;
            _bufferSize = DefaultBufferSize;
            _progress = progress;
            _reportInterval = reportInterval;
            _cancelToken = cancelToken;
        }

        public HttpClientDownloader(
            HttpClient httpClient, Uri uri, Stream destStream,
            int bufferSize,
            IProgress<(long Position, long ContentLength, TimeSpan Elapsed)> progress,
            TimeSpan reportInterval,
            CancellationToken? cancelToken = null)
        {
            _httpClient = httpClient;
            _uri = uri;
            _destStream = destStream;
            _bufferSize = bufferSize;
            _progress = progress;
            _reportInterval = reportInterval;
            _cancelToken = cancelToken;
        }
        #endregion

        #region DownloadAsync
        public async Task DownloadAsync()
        {
            using (HttpResponseMessage res = await _httpClient.GetAsync(_uri, HttpCompletionOption.ResponseHeadersRead))
            {
                Stopwatch watch = Stopwatch.StartNew();
                DateTime lastReport = DateTime.UtcNow;

                // Did server return success status code?
                StatusCode = res.StatusCode;
                res.EnsureSuccessStatusCode();

                // Read content length of a remote file.
                // Note) Some website such as Google Drive does not offer content length information.
                //       In such case, set content length to -1.
                long? rawContentLength = res.Content.Headers.ContentLength;
                long contentLength = rawContentLength ?? -1;

                // Track current position while downloading.
                long position = 0;

                // Read from stream
                byte[] buffer = new byte[_bufferSize];
                using (Stream srcStream = await res.Content.ReadAsStreamAsync())
                {
                    int bytesRead;
                    do
                    {
                        if (_cancelToken is CancellationToken token)
                            bytesRead = await srcStream.ReadAsync(buffer, 0, buffer.Length, token);
                        else
                            bytesRead = await srcStream.ReadAsync(buffer, 0, buffer.Length);

                        if (0 < bytesRead)
                        {
                            await _destStream.WriteAsync(buffer, 0, bytesRead);
                            position += bytesRead;
                        }
                        else
                        {
                            watch.Stop();
                        }

                        if (_progress is IProgress<(long Position, long ContentLength, TimeSpan Elapsed)> p)
                        {
                            DateTime now = DateTime.UtcNow;
                            if (_reportInterval <= now - lastReport)
                            {
                                lastReport = now;
                                p.Report((position, contentLength, watch.Elapsed));
                            }
                        }
                    }
                    while (0 < bytesRead);
                }
            }
        }
        #endregion
    }
    #endregion
}

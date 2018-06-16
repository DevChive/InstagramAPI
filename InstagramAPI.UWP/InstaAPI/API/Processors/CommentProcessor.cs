﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using InstaSharper.Classes;
using InstaSharper.Classes.Android.DeviceInfo;
using InstaSharper.Classes.Models;
using InstaSharper.Classes.ResponseWrappers;
using InstaSharper.Converters;
using InstaSharper.Converters.Json;
using InstaSharper.Helpers;
using InstaSharper.Logger;
using Newtonsoft.Json;

namespace InstaSharper.API.Processors
{
    public class CommentProcessor : ICommentProcessor
    {
        private readonly AndroidDevice _deviceInfo;
        private readonly IHttpRequestProcessor _httpRequestProcessor;
        private readonly IInstaLogger _logger;
        private readonly UserSessionData _user;

        public CommentProcessor(AndroidDevice deviceInfo, UserSessionData user,
            IHttpRequestProcessor httpRequestProcessor, IInstaLogger logger)
        {
            _deviceInfo = deviceInfo;
            _user = user;
            _httpRequestProcessor = httpRequestProcessor;
            _logger = logger;
        }

        public async Task<IResult<InstaCommentList>> GetMediaCommentsAsync(string mediaId,
            PaginationParameters paginationParameters)
        {
            try
            {
                var commentsUri = UriCreator.GetMediaCommentsUri(mediaId, paginationParameters.NextId);
                //commentsUri = new Uri(commentsUri.ToString() + "?can_support_threading=true");
                System.Diagnostics.Debug.WriteLine(commentsUri.ToString());
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, commentsUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine("json: " + json);
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaCommentList>(response, json);
                var commentListResponse = JsonConvert.DeserializeObject<InstaCommentListResponse>(json);
                var pagesLoaded = 1;

                InstaCommentList Convert(InstaCommentListResponse commentsResponse)
                {
                    return ConvertersFabric.Instance.GetCommentListConverter(commentsResponse).Convert();
                }

                while (commentListResponse.MoreComentsAvailable
                       && !string.IsNullOrEmpty(commentListResponse.NextMaxId)
                       && pagesLoaded < paginationParameters.MaximumPagesToLoad)
                {
                    var nextComments = await GetCommentListWithMaxIdAsync(mediaId, commentListResponse.NextMaxId);
                    if (!nextComments.Succeeded)
                        return Result.Fail(nextComments.Info, Convert(commentListResponse));
                    commentListResponse.NextMaxId = nextComments.Value.NextMaxId;
                    commentListResponse.MoreComentsAvailable = nextComments.Value.MoreComentsAvailable;
                    commentListResponse.Comments.AddRange(nextComments.Value.Comments);
                    pagesLoaded++;
                }

                var converter = ConvertersFabric.Instance.GetCommentListConverter(commentListResponse);
                return Result.Success(converter.Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaCommentList>(exception);
            }
        }
        public async Task<IResult</*InstaInlineCommentList*/InstaInlineCommentListResponse>> GetMediaInlineCommentsAsync(string mediaId, string targetCommentId,
    PaginationParameters paginationParameters)
        {
            try
            {
                ///api/v1/media/1794238891642842594_7922785150/comments/?target_comment_id=17949759496034241&can_support_threading=true
                var commentsUri = UriCreator.GetMediaInlineCommentsUri(mediaId, targetCommentId, paginationParameters.NextId);
                System.Diagnostics.Debug.WriteLine(commentsUri.ToString());
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, commentsUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine("json: " + json);
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaInlineCommentListResponse>(response, json);
                var commentListResponse = JsonConvert.DeserializeObject<InstaInlineCommentListResponse>(json);
                var pagesLoaded = 1;

                //InstaInlineCommentList Convert(InstaInlineCommentListResponse commentsResponse)
                //{
                //    return ConvertersFabric.Instance.GetInlineCommentListConverter(commentsResponse).Convert();
                //}

                while (commentListResponse.HasMoreHeadChildComments
                       && !string.IsNullOrEmpty(commentListResponse.NextId)
                       && pagesLoaded < paginationParameters.MaximumPagesToLoad)
                {
                    var nextComments = await GetInlineCommentListWithMaxIdAsync(mediaId, targetCommentId, commentListResponse.NextId);
                    //if (!nextComments.Succeeded)
                    //    return Result.Fail(nextComments.Info, Convert(commentListResponse));
                    if (!nextComments.Succeeded)
                        return Result.Fail(nextComments.Info, commentListResponse);
                    commentListResponse.NextId = nextComments.Value.NextId;
                    commentListResponse.HasMoreHeadChildComments = nextComments.Value.HasMoreHeadChildComments;
                    commentListResponse.ChildComments.AddRange(nextComments.Value.ChildComments);
                    pagesLoaded++;
                }
                return Result.Success(commentListResponse);
                //var converter = ConvertersFabric.Instance.GetInlineCommentListConverter(commentListResponse);
                //return Result.Success(converter.Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaInlineCommentListResponse>(exception);
            }
        }
        public async Task<IResult<InstaComment>> CommentMediaAsync(string mediaId, string text)
        {
            try
            {
                var instaUri = UriCreator.GetPostCommetUri(mediaId);
                var breadcrumb = CryptoHelper.GetCommentBreadCrumbEncoded(text);
                var fields = new Dictionary<string, string>
                {
                    {"user_breadcrumb", breadcrumb},
                    {"idempotence_token", Guid.NewGuid().ToString()},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"_uid", _user.LoggedInUser.Pk.ToString()},
                    {"_csrftoken", _user.CsrfToken},
                    {"comment_text", text},
                    {"containermodule", "comments_feed_timeline"},
                    {"radio_type", "wifi-none"}
                };
                var request =
                    HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, fields);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaComment>(response, json);
                var commentResponse = JsonConvert.DeserializeObject<InstaCommentResponse>(json,
                    new InstaCommentDataConverter());
                var converter = ConvertersFabric.Instance.GetCommentConverter(commentResponse);
                return Result.Success(converter.Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail(exception.Message, (InstaComment) null);
            }
        }

        public async Task<IResult<InstaComment>> InlineCommentMediaAsync(string mediaId, string targetCommentId, string text)
        {
            try
            {
                var instaUri = UriCreator.GetPostCommetUri(mediaId);
                var breadcrumb = CryptoHelper.GetCommentBreadCrumbEncoded(text);
                var fields = new Dictionary<string, string>
                {
                    {"user_breadcrumb", breadcrumb},
                    {"idempotence_token", Guid.NewGuid().ToString()},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"replied_to_comment_id", targetCommentId},
                    {"_uid", _user.LoggedInUser.Pk.ToString()},
                    {"_csrftoken", _user.CsrfToken},
                    {"comment_text", text},
                    {"containermodule", "comments_feed_timeline"},
                    {"radio_type", "wifi-none"}
                };
                var request =
                    HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, fields);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaComment>(response, json);
                var commentResponse = JsonConvert.DeserializeObject<InstaCommentResponse>(json,
                    new InstaCommentDataConverter());
                var converter = ConvertersFabric.Instance.GetCommentConverter(commentResponse);
                return Result.Success(converter.Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail(exception.Message, (InstaComment)null);
            }
        }

        public async Task<IResult<bool>> DeleteCommentAsync(string mediaId, string commentId)
        {
            try
            {
                var instaUri = UriCreator.GetDeleteCommetUri(mediaId, commentId);
                var fields = new Dictionary<string, string>
                {
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"_uid", _user.LoggedInUser.Pk.ToString()},
                    {"_csrftoken", _user.CsrfToken}
                };
                var request =
                    HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, fields);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                return response.StatusCode == HttpStatusCode.OK
                    ? Result.Success(true)
                    : Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail(exception.Message, false);
            }
        }

        private async Task<IResult<InstaCommentListResponse>> GetCommentListWithMaxIdAsync(string mediaId,
            string nextId)
        {
            var commentsUri = UriCreator.GetMediaCommentsUri(mediaId, nextId);
            var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, commentsUri, _deviceInfo);
            var response = await _httpRequestProcessor.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
                return Result.UnExpectedResponse<InstaCommentListResponse>(response, json);
            var comments = JsonConvert.DeserializeObject<InstaCommentListResponse>(json);
            return Result.Success(comments);
        }

        private async Task<IResult<InstaInlineCommentListResponse>> GetInlineCommentListWithMaxIdAsync(string mediaId,
            string targetCommandId,
            string nextId)
        {
            var commentsUri = UriCreator.GetMediaInlineCommentsUri(mediaId, targetCommandId, nextId);
            var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, commentsUri, _deviceInfo);
            var response = await _httpRequestProcessor.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
                return Result.UnExpectedResponse<InstaInlineCommentListResponse>(response, json);
            var comments = JsonConvert.DeserializeObject<InstaInlineCommentListResponse>(json);
            return Result.Success(comments);
        }
    }
}
﻿using System.Threading.Tasks;
using InstaAPI.Classes.Models;
using InstaSharper.Classes;
using InstaSharper.Classes.Models;

namespace InstaSharper.API.Processors
{
    public interface IStoryProcessor
    {
        Task<IResult<InstaStoryFeed>> GetStoryFeedAsync();
        Task<IResult<InstaStory>> GetUserStoryAsync(long userId);
        Task<IResult<InstaStoryMedia>> UploadStoryPhotoAsync(InstaImage image, string caption);
        Task<IResult<InstaStoryMedia>> ConfigureStoryPhotoAsync(InstaImage image, string uploadId, string caption);
        Task<IResult<InstaReelFeed>> GetUserStoryFeedAsync(long userId);
        Task<IResult<InstaReelStoryMediaViewers>> GetStoryMediaViewers(string StoryMediaId, PaginationParameters paginationParameters);
        Task<IResult<InstaSharing>> ShareStoryAsync(string reelId, string storyMediaId, string threadId, SharingType sharingType = SharingType.Video);

    }
}
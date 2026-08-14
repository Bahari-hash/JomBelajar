using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;
using TinyLang.Entities.Enums;

namespace TinyLang.Policies;

/// <summary>
/// 集中定义登录用户可以查看和学习的实时词条内容范围。
/// </summary>
public static class WordVisibilityPolicy
{
    /// <summary>
    /// 应用发布状态、内容完整性以及全部关联音频可播放性条件。
    /// </summary>
    /// <param name="query">尚未物化的词条查询。</param>
    /// <returns>仅包含当前对登录用户可见词条的可组合查询。</returns>
    public static IQueryable<Word> Apply(IQueryable<Word> query)
    {
        return query.Where(word =>
            word.Status == WordPublicationStatus.Published &&
            word.PublishedAt != null &&
            word.Senses.Any() &&
            word.Pronunciations.Any() &&
            word.Pronunciations.Count(value => value.IsDefault) == 1 &&
            word.Pronunciations.All(pronunciation =>
                pronunciation.AudioClip != null &&
                pronunciation.AudioClip.ProcessingStatus == AudioProcessingStatus.Ready &&
                pronunciation.AudioClip.PublicationStatus == AudioPublicationStatus.Published &&
                pronunciation.AudioClip.Kind == AudioClipKind.WordPronunciation) &&
            word.Senses.SelectMany(sense => sense.Examples).All(example =>
                example.AudioClipId == null ||
                (example.AudioClip != null &&
                 example.AudioClip.ProcessingStatus == AudioProcessingStatus.Ready &&
                 example.AudioClip.PublicationStatus == AudioPublicationStatus.Published &&
                 example.AudioClip.Kind == AudioClipKind.ExampleSentence)));
    }
}

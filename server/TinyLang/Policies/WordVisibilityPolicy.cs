using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;

namespace TinyLang.Policies;

/// <summary>
/// 集中定义登录用户可以查看和学习的实时词条内容范围。
/// </summary>
public static class WordVisibilityPolicy
{
    /// <summary>
    /// 应用词条至少包含一个释义的防御性条件。
    /// </summary>
    /// <param name="query">尚未物化的词条查询。</param>
    /// <returns>仅包含当前对登录用户可见词条的可组合查询。</returns>
    public static IQueryable<Word> Apply(IQueryable<Word> query)
    {
        return query.Where(word => !word.IsDeleted && word.Senses.Any());
    }
}

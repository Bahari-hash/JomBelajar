using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示用户收藏的一个全局词条。
/// </summary>
public sealed class UserWordFavorite : BaseEntity
{
    public UserWordFavorite()
    {
        Id = Guid.NewGuid();
    }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid WordId { get; set; }
    public Word? Word { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

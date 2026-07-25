using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述 RabbitMQ broker 的连接和认证配置。
/// </summary>
public sealed record RabbitMqSettings
{
    public const string SectionName = "RabbitMqSettings";

    [Required(ErrorMessage = "Rabbitmq host cannot be empty.")]
    public required string Host { get; init; }

    [Required(ErrorMessage = "Rabbitmq port cannot be empty.")]
    [Range(1, 65535, ErrorMessage = "Rabbitmq port must between {1} and {2}.")]
    public required int Port { get; init; }

    [Required(ErrorMessage = "Rabbitmq virtual machine cannot be empty.")]
    public required string VirtualHost { get; init; }

    [Required(ErrorMessage = "Rabbitmq username cannot be empty.")]
    public required string Username { get; init; }

    [Required(ErrorMessage = "Rabbitmq user password cannot be empay.")]
    public required string Password { get; init; }
}
